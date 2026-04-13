using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace DomainModeling.Discovery;

/// <summary>
/// Indexes <c>&lt;domain&gt;...&lt;/domain&gt;</c> tags from XML documentation comments in C# sources
/// loaded via Roslyn/MSBuild. Tags may appear anywhere in the type's doc comment (including inside
/// <c>&lt;summary&gt;</c>). Inner text should start with a link kind (e.g. <c>emits</c>) followed by
/// the referenced type name.
/// Method comments with <c>emits</c> and <c>see cref</c> are mapped to CLR type + method keys for emission discovery.
/// </summary>
internal sealed partial class RoslynDocumentationIndexer
{
    private static readonly ConcurrentDictionary<string, Lazy<RoslynDocumentationIndexer?>> Cache = new(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _clrFullNameToDomainText;
    private readonly Dictionary<string, List<string>> _methodKeyToEmittedTypeFullNames;
    private readonly Dictionary<string, List<(string CanonicalFullName, string DisplayName)>> _typeDocumentedEmissions;

    private RoslynDocumentationIndexer(
        Dictionary<string, string> clrFullNameToDomainText,
        Dictionary<string, List<string>> methodKeyToEmittedTypeFullNames,
        Dictionary<string, List<(string CanonicalFullName, string DisplayName)>> typeDocumentedEmissions)
    {
        _clrFullNameToDomainText = clrFullNameToDomainText;
        _methodKeyToEmittedTypeFullNames = methodKeyToEmittedTypeFullNames;
        _typeDocumentedEmissions = typeDocumentedEmissions;
    }

    /// <summary>
    /// Holds resolved generic cref info from <c>&lt;domain&gt;</c> tags indexed by position
    /// on a given type, so that <see cref="NormalizeDomainInnerXml"/> can render display names.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> ResolvedCrefDisplayNames = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns concatenated <c>&lt;domain&gt;</c> inner text for the type, or <c>null</c> if none.
    /// </summary>
    public string? TryGetDomainSummary(Type type)
    {
        if (type.FullName is null)
            return null;

        if (_clrFullNameToDomainText.TryGetValue(type.FullName, out var text))
            return text;

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def.FullName is not null && _clrFullNameToDomainText.TryGetValue(def.FullName, out var defText))
                return defText;
        }

        return null;
    }

    /// <summary>
    /// Returns event types documented on a type via <c>&lt;domain&gt;emits&lt;/domain&gt;</c> tags,
    /// preserving constructed generic type arguments. Each entry has the canonical CLR full name
    /// and a human-readable display name.
    /// </summary>
    public IReadOnlyList<(string CanonicalFullName, string DisplayName)> TryGetTypeDocumentedEmissions(Type type)
    {
        if (type.FullName is null)
            return [];
        if (_typeDocumentedEmissions.TryGetValue(type.FullName, out var list))
            return list;
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def.FullName is not null && _typeDocumentedEmissions.TryGetValue(def.FullName, out var defList))
                return defList;
        }
        return [];
    }

    /// <summary>
    /// Event types documented as emitted by a method via <c>&lt;domain&gt;emits&lt;/domain&gt;</c>, keyed as
    /// <c>{declaring type CLR full name}::{method name}</c> (constructors use <c>ctor</c> / <c>cctor</c>).
    /// </summary>
    public IReadOnlyList<string> TryGetDocumentedEmissions(Type declaringType, string normalizedMethodName)
    {
        if (declaringType.FullName is null)
            return [];

        var key = declaringType.FullName + "::" + normalizedMethodName;
        return _methodKeyToEmittedTypeFullNames.TryGetValue(key, out var list)
            ? list
            : [];
    }

    /// <summary>
    /// Builds an indexer from MSBuild project or solution paths, or directories containing <c>*.csproj</c>.
    /// Results are cached per distinct ordered path list.
    /// </summary>
    public static RoslynDocumentationIndexer? TryCreate(IReadOnlyList<string> sourceRoots)
    {
        if (sourceRoots.Count == 0)
            return null;

        var normalized = sourceRoots
            .Where(static p => !string.IsNullOrWhiteSpace(p))
            .Select(static p => Path.GetFullPath(p.Trim()))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static p => p, StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0)
            return null;

        var cacheKey = string.Join('\u001f', normalized);
        return Cache.GetOrAdd(cacheKey, static key => new Lazy<RoslynDocumentationIndexer?>(() =>
        {
            var paths = key.Split('\u001f', StringSplitOptions.None);
            return CreateUncached(paths);
        })).Value;
    }

    private static RoslynDocumentationIndexer? CreateUncached(string[] normalizedPaths)
    {
        var projectPaths = ExpandToProjectPaths(normalizedPaths);
        if (projectPaths.Count == 0)
            return null;

        MsBuildWorkspaceRegistration.EnsureRegistered();

        using var workspace = MSBuildWorkspace.Create();
        var typeMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var methodMap = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var typeEmissions = new Dictionary<string, List<(string, string)>>(StringComparer.Ordinal);

        foreach (var projectPath in projectPaths)
        {
            try
            {
                var project = workspace.OpenProjectAsync(projectPath).GetAwaiter().GetResult();
                var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
                if (compilation is null)
                    continue;

                IndexCompilation(compilation, typeMap, methodMap, typeEmissions);
            }
            catch
            {
                // Skip projects that fail to load (missing SDK, etc.)
            }
        }

        if (typeMap.Count == 0 && methodMap.Count == 0 && typeEmissions.Count == 0)
            TryIndexDirectoriesWithAdHocCompilation(normalizedPaths, typeMap, methodMap, typeEmissions);

        return typeMap.Count == 0 && methodMap.Count == 0 && typeEmissions.Count == 0
            ? null
            : new RoslynDocumentationIndexer(typeMap, methodMap, typeEmissions);
    }

    private static void TryIndexDirectoriesWithAdHocCompilation(
        string[] documentationRoots,
        Dictionary<string, string> typeMap,
        Dictionary<string, List<string>> methodMap,
        Dictionary<string, List<(string, string)>> typeEmissions)
    {
        var refs = GetPlatformMetadataReferences();
        if (refs.Count == 0)
            return;

        var parseOptions = CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose);

        foreach (var dir in CollectDirectoriesForAdHoc(documentationRoots))
        {
            var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            if (csFiles.Length == 0)
                continue;

            var trees = new List<SyntaxTree>(csFiles.Length);
            foreach (var file in csFiles)
            {
                var text = File.ReadAllText(file);
                trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, path: file, encoding: null));
            }

            var compilation = CSharpCompilation.Create(
                "DomainModelingDocIndex_" + Guid.NewGuid().ToString("N"),
                trees,
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            IndexCompilation(compilation, typeMap, methodMap, typeEmissions);
        }
    }

    private static List<string> CollectDirectoriesForAdHoc(IEnumerable<string> roots)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (File.Exists(root))
            {
                if (root.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    var d = Path.GetDirectoryName(root);
                    if (!string.IsNullOrEmpty(d))
                        dirs.Add(d);
                }
            }
            else if (Directory.Exists(root))
            {
                dirs.Add(root);
            }
        }

        return dirs.ToList();
    }

    private static List<MetadataReference> GetPlatformMetadataReferences()
    {
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrEmpty(trusted))
            return [];

        var list = new List<MetadataReference>();
        foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(path))
                list.Add(MetadataReference.CreateFromFile(path));
        }

        return list;
    }

    private static List<string> ExpandToProjectPaths(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    result.Add(path);
                else if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                    AddProjectsFromSolution(path, result);
            }
            else if (Directory.Exists(path))
            {
                foreach (var sln in Directory.GetFiles(path, "*.sln"))
                    AddProjectsFromSolution(sln, result);
                foreach (var csproj in Directory.GetFiles(path, "*.csproj"))
                    result.Add(csproj);
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddProjectsFromSolution(string slnPath, List<string> result)
    {
        try
        {
            var lines = File.ReadAllLines(slnPath);
            foreach (var line in lines)
            {
                var idx = line.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    continue;
                var start = line.LastIndexOf('"', idx);
                if (start < 0)
                    continue;
                var end = line.IndexOf('"', start + 1);
                if (end <= start)
                    continue;
                var rel = line[(start + 1)..end].Replace('\\', Path.DirectorySeparatorChar);
                var dir = Path.GetDirectoryName(slnPath);
                if (dir is null)
                    continue;
                var full = Path.GetFullPath(Path.Combine(dir, rel));
                if (File.Exists(full))
                    result.Add(full);
            }
        }
        catch
        {
            // ignore malformed solution files
        }
    }

    private static void IndexCompilation(
        Compilation compilation,
        Dictionary<string, string> typeMap,
        Dictionary<string, List<string>> methodMap,
        Dictionary<string, List<(string, string)>> typeEmissions)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes())
            {
                switch (node)
                {
                    case BaseMethodDeclarationSyntax methodDecl:
                    {
                        if (model.GetDeclaredSymbol(methodDecl) is not IMethodSymbol methodSym)
                            break;
                        if (methodSym.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet)
                            break;

                        var xml = methodSym.GetDocumentationCommentXml();
                        var emitted = ExtractEmittedTypesFromDocumentationXml(xml, compilation);
                        if (emitted.Count == 0)
                            break;

                        var key = DocumentationMethodKey(methodSym);
                        if (!methodMap.TryGetValue(key, out var list))
                        {
                            list = [];
                            methodMap[key] = list;
                        }

                        foreach (var e in emitted)
                        {
                            if (!list.Contains(e, StringComparer.Ordinal))
                                list.Add(e);
                        }

                        break;
                    }
                    default:
                    {
                        INamedTypeSymbol? symbol = node switch
                        {
                            TypeDeclarationSyntax typeDecl => model.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol,
                            EnumDeclarationSyntax enumDecl => model.GetDeclaredSymbol(enumDecl) as INamedTypeSymbol,
                            _ => null
                        };

                        if (symbol is not null)
                        {
                            // Resolve generic crefs first — populates ResolvedCrefDisplayNames cache
                            TryAddTypeDocumentedEmissions(typeEmissions, symbol, model, node, compilation);
                            // Then build description text, which uses the resolved display names
                            TryAddSymbol(typeMap, symbol);
                        }
                        break;
                    }
                }
            }
        }
    }

    private static string DocumentationMethodKey(IMethodSymbol method)
    {
        var typeName = ToClrMetadataFullName(method.ContainingType);
        var name = method.MetadataName switch
        {
            WellKnownMemberNames.InstanceConstructorName => "ctor",
            WellKnownMemberNames.StaticConstructorName => "cctor",
            var n => n
        };
        return typeName + "::" + name;
    }

    private static void TryAddSymbol(Dictionary<string, string> map, INamedTypeSymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml();
        var clrName = ToClrMetadataFullName(symbol);

        // Use resolved display names if available (populated by TryAddTypeDocumentedEmissions)
        ResolvedCrefDisplayNames.TryGetValue(clrName, out var crefDisplayNames);
        var domainText = ExtractDomainTags(xml, crefDisplayNames);
        if (domainText is null)
            return;

        MergeDomainText(map, clrName, domainText);

        if (symbol is { IsGenericType: true, IsUnboundGenericType: false })
            MergeDomainText(map, ToClrMetadataFullName(symbol.OriginalDefinition), domainText);
    }

    /// <summary>
    /// Walks the syntax trivia on a type declaration looking for <c>&lt;domain&gt;</c> tags containing
    /// <c>&lt;see cref="..."/&gt;</c>, resolves generic crefs to constructed types, and stores them.
    /// Resolved generic display names are cached for use in description text rendering.
    /// Emits-specific references are stored in <paramref name="typeEmissions"/> for synthetic node creation.
    /// </summary>
    private static void TryAddTypeDocumentedEmissions(
        Dictionary<string, List<(string, string)>> typeEmissions,
        INamedTypeSymbol symbol,
        SemanticModel model,
        SyntaxNode declNode,
        Compilation compilation)
    {
        var xml = symbol.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xml))
            return;

        if (!DomainTagRegex().IsMatch(xml))
            return;

        var allResolved = ExtractResolvedGenericCrefsFromDomainTags(declNode, model, compilation);
        if (allResolved.Count == 0)
            return;

        var clrName = ToClrMetadataFullName(symbol);

        // Store resolved display names for description text rendering (all link kinds)
        // Only store entries for constructed generics — non-generic crefs render fine from doc IDs
        var crefMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in allResolved.Where(e => e.CanonicalFullName != e.OpenGenericMetadataName))
            crefMap.TryAdd(entry.OpenGenericMetadataName, entry.DisplayName);
        ResolvedCrefDisplayNames[clrName] = crefMap;
        if (symbol is { IsGenericType: true, IsUnboundGenericType: false })
            ResolvedCrefDisplayNames[ToClrMetadataFullName(symbol.OriginalDefinition)] = crefMap;

        // Filter to emits-only for synthetic event node creation
        var emitsOnly = allResolved.Where(e => e.IsEmits).ToList();
        if (emitsOnly.Count > 0)
        {
            if (!typeEmissions.TryGetValue(clrName, out var list))
            {
                list = [];
                typeEmissions[clrName] = list;
            }

            foreach (var entry in emitsOnly)
            {
                if (!list.Any(e => string.Equals(e.Item1, entry.CanonicalFullName, StringComparison.Ordinal)))
                    list.Add((entry.CanonicalFullName, entry.DisplayName));
            }

            if (symbol is { IsGenericType: true, IsUnboundGenericType: false })
            {
                var defClr = ToClrMetadataFullName(symbol.OriginalDefinition);
                if (!typeEmissions.TryGetValue(defClr, out var defList))
                {
                    defList = [];
                    typeEmissions[defClr] = defList;
                }
                foreach (var entry in emitsOnly)
                {
                    if (!defList.Any(e => string.Equals(e.Item1, entry.CanonicalFullName, StringComparison.Ordinal)))
                        defList.Add((entry.CanonicalFullName, entry.DisplayName));
                }
            }
        }
    }

    /// <summary>
    /// Extracts resolved generic type references from ALL <c>&lt;domain&gt;</c> tags in the syntax trivia,
    /// using the semantic model to resolve constructed generic types. Each result includes the canonical
    /// CLR full name, display name, the open generic metadata name (for description text remapping),
    /// and whether it came from an <c>emits</c> tag.
    /// </summary>
    private static List<(string CanonicalFullName, string DisplayName, string OpenGenericMetadataName, bool IsEmits)> ExtractResolvedGenericCrefsFromDomainTags(
        SyntaxNode declNode,
        SemanticModel model,
        Compilation compilation)
    {
        var result = new List<(string, string, string, bool)>();
        var trivia = declNode.GetLeadingTrivia();

        foreach (var t in trivia)
        {
            if (t.GetStructure() is not DocumentationCommentTriviaSyntax docComment)
                continue;

            foreach (var xmlNode in docComment.DescendantNodes())
            {
                if (xmlNode is not XmlEmptyElementSyntax { Name.LocalName.Text: "see" } seeElement)
                    continue;

                var crefAttr = seeElement.Attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
                if (crefAttr is null)
                    continue;

                var domainTag = FindContainingDomainTag(seeElement);
                if (domainTag is null)
                    continue;

                var symbolInfo = model.GetSymbolInfo(crefAttr.Cref);
                var resolved = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();
                if (resolved is not INamedTypeSymbol baseType)
                    continue;

                var constructed = TryConstructGenericFromCref(crefAttr.Cref, baseType, model, compilation);
                var nt = constructed ?? baseType;

                var canonical = ToCanonicalGenericFullName(nt);
                var display = ToDisplayGenericName(nt);
                var openGenericName = ToClrMetadataFullName(nt.IsGenericType ? nt.OriginalDefinition : nt);
                var isEmits = IsDomainTagEmits(domainTag);

                if (!result.Any(e => string.Equals(e.Item1, canonical, StringComparison.Ordinal)))
                    result.Add((canonical, display, openGenericName, isEmits));
            }
        }

        return result;
    }

    private static XmlElementSyntax? FindContainingDomainTag(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is XmlElementSyntax xmlEl &&
                xmlEl.StartTag.Name.LocalName.Text.Equals("domain", StringComparison.OrdinalIgnoreCase))
                return xmlEl;
            current = current.Parent;
        }
        return null;
    }

    private static bool IsDomainTagEmits(XmlElementSyntax domainElement)
    {
        var textContent = new StringBuilder();
        foreach (var child in domainElement.Content)
        {
            if (child is XmlTextSyntax textNode)
                textContent.Append(textNode.ToString());
            else if (child is XmlEmptyElementSyntax)
                textContent.Append(' ');
        }
        var condensed = string.Join(' ', textContent.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return condensed.StartsWith("emits", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// For a generic cref like <c>EntityDeletedEvent{Customer}</c>, resolves the type arguments
    /// as concrete type symbols and constructs the closed generic type.
    /// Returns <c>null</c> if the cref is not generic or arguments cannot be resolved as real types
    /// (as opposed to type parameter names).
    /// </summary>
    private static INamedTypeSymbol? TryConstructGenericFromCref(
        CrefSyntax cref,
        INamedTypeSymbol baseType,
        SemanticModel model,
        Compilation compilation)
    {
        if (!baseType.IsGenericType)
            return null;

        var genericDef = baseType.IsDefinition ? baseType : baseType.OriginalDefinition;

        TypeArgumentListSyntax? typeArgList = null;
        foreach (var descendant in cref.DescendantNodes())
        {
            if (descendant is TypeArgumentListSyntax tal)
            {
                typeArgList = tal;
                break;
            }
        }

        if (typeArgList is null || typeArgList.Arguments.Count == 0)
            return null;

        if (typeArgList.Arguments.Count != genericDef.TypeParameters.Length)
            return null;

        var typeArgs = new ITypeSymbol[typeArgList.Arguments.Count];
        var allResolved = true;

        for (var i = 0; i < typeArgList.Arguments.Count; i++)
        {
            var argSyntax = typeArgList.Arguments[i];

            // Try semantic model first
            INamedTypeSymbol? argType = null;

            var argInfo = model.GetSymbolInfo(argSyntax);
            if (argInfo.Symbol is INamedTypeSymbol nt1)
                argType = nt1;
            else if (argInfo.CandidateSymbols.FirstOrDefault() is INamedTypeSymbol nt2)
                argType = nt2;

            if (argType is null)
            {
                var argTypeInfo = model.GetTypeInfo(argSyntax);
                if (argTypeInfo.Type is INamedTypeSymbol nt3)
                    argType = nt3;
            }

            // In doc comment crefs, type arguments may not resolve via GetSymbolInfo.
            // Fall back to finding the type by name in the compilation.
            if (argType is null && argSyntax is IdentifierNameSyntax idName)
            {
                argType = ResolveTypeByName(idName.Identifier.Text, baseType.ContainingNamespace, compilation);
            }
            else if (argType is null && argSyntax is QualifiedNameSyntax qualName)
            {
                argType = ResolveTypeByQualifiedName(qualName.ToString(), compilation);
            }

            if (argType is not null && argType.TypeKind != TypeKind.TypeParameter)
            {
                typeArgs[i] = argType;
            }
            else
            {
                allResolved = false;
                break;
            }
        }

        if (!allResolved)
            return null;

        return genericDef.Construct(typeArgs);
    }

    private static INamedTypeSymbol? ResolveTypeByName(string name, INamespaceSymbol? searchNamespace, Compilation compilation)
    {
        if (searchNamespace is not null)
        {
            var inNs = FindTypeInNamespace(searchNamespace, name);
            if (inNs is not null)
                return inNs;
        }

        return FindTypeRecursive(compilation.GlobalNamespace, name);
    }

    private static INamedTypeSymbol? ResolveTypeByQualifiedName(string qualifiedName, Compilation compilation)
    {
        var parts = qualifiedName.Split('.');
        INamespaceOrTypeSymbol current = compilation.GlobalNamespace;

        for (var i = 0; i < parts.Length; i++)
        {
            var members = current.GetMembers(parts[i]);
            if (i == parts.Length - 1)
            {
                var type = members.OfType<INamedTypeSymbol>().FirstOrDefault(t => t.TypeKind != TypeKind.TypeParameter);
                if (type is not null)
                    return type;
            }
            else
            {
                var ns = members.OfType<INamespaceSymbol>().FirstOrDefault();
                if (ns is null)
                    return null;
                current = ns;
            }
        }

        return null;
    }

    private static INamedTypeSymbol? FindTypeInNamespace(INamespaceSymbol ns, string name)
    {
        var found = ns.GetTypeMembers(name).FirstOrDefault(t => t.TypeKind != TypeKind.TypeParameter);
        if (found is not null)
            return found;

        var parent = ns.ContainingNamespace;
        if (parent is not null && !parent.IsGlobalNamespace)
            return FindTypeInNamespace(parent, name);

        return null;
    }

    private static INamedTypeSymbol? FindTypeRecursive(INamespaceSymbol ns, string name)
    {
        var found = ns.GetTypeMembers(name).FirstOrDefault(t => t.TypeKind != TypeKind.TypeParameter);
        if (found is not null)
            return found;

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            found = FindTypeRecursive(childNs, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Builds a canonical CLR full name that matches the format produced by <c>Type.FullName</c> for
    /// constructed generic types: <c>Ns.EntityDeletedEvent`1[[Ns.User, AssemblyName, ...]]</c>.
    /// For non-generic types this is equivalent to <see cref="ToClrMetadataFullName"/>.
    /// For constructed generics this uses the bracket notation matching CLR reflection.
    /// </summary>
    internal static string ToCanonicalGenericFullName(INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType || symbol.IsUnboundGenericType || symbol.TypeArguments.All(a => a is ITypeParameterSymbol))
            return ToClrMetadataFullName(symbol);

        var def = symbol.OriginalDefinition;
        var defName = ToClrMetadataFullName(def);
        var args = symbol.TypeArguments;
        var sb = new StringBuilder(defName);
        sb.Append('[');
        for (var i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[');
            if (args[i] is INamedTypeSymbol argNt)
                sb.Append(ToCanonicalGenericFullName(argNt));
            else
                sb.Append(args[i].ToDisplayString());
            sb.Append(']');
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string ToDisplayGenericName(INamedTypeSymbol symbol)
    {
        if (!symbol.IsGenericType || symbol.IsUnboundGenericType)
            return symbol.Name;

        var baseName = symbol.Name;
        var args = symbol.TypeArguments;
        return baseName + "<" + string.Join(", ", args.Select(a => a is INamedTypeSymbol nt ? ToDisplayGenericName(nt) : a.Name)) + ">";
    }

    private static void MergeDomainText(Dictionary<string, string> map, string clrFullName, string domainText)
    {
        if (map.TryGetValue(clrFullName, out var existing))
        {
            if (string.Equals(existing, domainText, StringComparison.Ordinal))
                return;
            map[clrFullName] = existing + "; " + domainText;
        }
        else
        {
            map[clrFullName] = domainText;
        }
    }

    /// <summary>
    /// CLR-style metadata name: namespaces with '.', nested types with '+', generics with `arity.
    /// Matches <see cref="Type.FullName"/> for loaded types.
    /// </summary>
    private static string ToClrMetadataFullName(INamedTypeSymbol symbol)
    {
        var parts = new Stack<string>();
        for (INamedTypeSymbol? t = symbol; t is not null; t = t.ContainingType)
            parts.Push(t.MetadataName);

        var ns = symbol.ContainingNamespace;
        var nsPrefix = ns is null || ns.IsGlobalNamespace ? "" : ns.ToDisplayString() + ".";
        return nsPrefix + string.Join("+", parts);
    }

    /// <summary>
    /// Parses <c>&lt;domain&gt;</c> blocks with link kind <c>emits</c> and resolves <c>see cref</c> to CLR type full names.
    /// </summary>
    private static List<string> ExtractEmittedTypesFromDocumentationXml(string? documentationXml, Compilation compilation)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(documentationXml))
            return result;

        foreach (Match m in DomainTagRegex().Matches(documentationXml))
        {
            var inner = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(inner))
                continue;

            try
            {
                var wrapped = "<r>" + inner + "</r>";
                var el = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
                if (!DomainTagHasEmitsLinkKind(el))
                    continue;

                foreach (var see in el.Descendants().Where(e => e.Name.LocalName.Equals("see", StringComparison.OrdinalIgnoreCase)))
                {
                    var cref = see.Attribute("cref")?.Value;
                    if (string.IsNullOrWhiteSpace(cref))
                        continue;

                    var id = cref.Length >= 2 && cref[1] == ':' ? cref : "T:" + cref;
                    foreach (var sym in DocumentationCommentId.GetSymbolsForReferenceId(id, compilation))
                    {
                        if (sym is INamedTypeSymbol nt)
                        {
                            var clr = ToClrMetadataFullName(nt);
                            if (!result.Contains(clr, StringComparer.Ordinal))
                                result.Add(clr);
                        }
                    }
                }
            }
            catch
            {
                // ignore malformed fragments
            }
        }

        return result;
    }

    private static bool DomainTagHasEmitsLinkKind(XElement domainFragmentRoot)
    {
        var sb = new StringBuilder();
        AppendPlainTextForLinkKind(domainFragmentRoot, sb);
        var condensed = string.Join(' ', sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return condensed.StartsWith("emits ", StringComparison.OrdinalIgnoreCase)
               || string.Equals(condensed, "emits", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendPlainTextForLinkKind(XElement el, StringBuilder sb)
    {
        foreach (var node in el.Nodes())
        {
            switch (node)
            {
                case XText t:
                    sb.Append(t.Value);
                    break;
                case XElement child when child.Name.LocalName.Equals("see", StringComparison.OrdinalIgnoreCase):
                    sb.Append(' ');
                    break;
                case XElement child:
                    AppendPlainTextForLinkKind(child, sb);
                    break;
            }
        }
    }

    private static string? ExtractDomainTags(string? documentationXml, Dictionary<string, string>? crefDisplayNames = null)
    {
        if (string.IsNullOrWhiteSpace(documentationXml))
            return null;

        var matches = DomainTagRegex().Matches(documentationXml);
        if (matches.Count == 0)
            return null;

        var segments = new List<string>(matches.Count);
        foreach (Match m in matches)
        {
            var raw = m.Groups[1].Value;
            var normalized = NormalizeDomainInnerXml(raw, crefDisplayNames);
            if (normalized.Length > 0)
                segments.Add(normalized);
        }

        return segments.Count == 0 ? null : string.Join("; ", segments);
    }

    /// <summary>
    /// Strips XML doc tags inside &lt;domain&gt; and collapses whitespace so
    /// <c>emits &lt;see cref="T:Ns.Event"/&gt;</c> becomes a readable line.
    /// When <paramref name="crefDisplayNames"/> is provided, resolved generic display names
    /// are used instead of raw doc comment IDs (which lose type arguments).
    /// </summary>
    private static string NormalizeDomainInnerXml(string raw, Dictionary<string, string>? crefDisplayNames)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            var wrapped = "<r>" + raw + "</r>";
            var el = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
            var text = string.Concat(el.Nodes().Select(n => NodeToPlainText(n, crefDisplayNames))).Trim();
            return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch
        {
            return string.Join(' ', raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    private static string NodeToPlainText(XNode node, Dictionary<string, string>? crefDisplayNames)
    {
        return node switch
        {
            XText t => t.Value,
            XElement e when e.Name.LocalName.Equals("see", StringComparison.OrdinalIgnoreCase) =>
                ResolveCrefDisplayText(e.Attribute("cref")?.Value, crefDisplayNames),
            XElement e => string.Concat(e.Nodes().Select(n => NodeToPlainText(n, crefDisplayNames))),
            _ => string.Empty
        };
    }

    private static string ResolveCrefDisplayText(string? cref, Dictionary<string, string>? crefDisplayNames)
    {
        if (cref is null)
            return string.Empty;

        var stripped = StripDocIdPrefix(cref);

        // Check if we have a resolved display name for this open-generic metadata name
        if (crefDisplayNames is not null && crefDisplayNames.TryGetValue(stripped, out var displayName))
            return displayName;

        return stripped;
    }

    private static string StripDocIdPrefix(string cref)
    {
        if (cref.Length >= 2 && cref[1] == ':')
            return cref[2..];
        return cref;
    }

    [GeneratedRegex(@"<domain>\s*(.*?)\s*</domain>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DomainTagRegex();
}

/// <summary>
/// Registers MSBuild with Roslyn once per process.
/// </summary>
internal static class MsBuildWorkspaceRegistration
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
            return;
        lock (Gate)
        {
            if (_registered)
                return;
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();
            _registered = true;
        }
    }
}
