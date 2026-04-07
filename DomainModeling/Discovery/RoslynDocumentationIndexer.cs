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

    private RoslynDocumentationIndexer(
        Dictionary<string, string> clrFullNameToDomainText,
        Dictionary<string, List<string>> methodKeyToEmittedTypeFullNames)
    {
        _clrFullNameToDomainText = clrFullNameToDomainText;
        _methodKeyToEmittedTypeFullNames = methodKeyToEmittedTypeFullNames;
    }

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

        foreach (var projectPath in projectPaths)
        {
            try
            {
                var project = workspace.OpenProjectAsync(projectPath).GetAwaiter().GetResult();
                var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
                if (compilation is null)
                    continue;

                IndexCompilation(compilation, typeMap, methodMap);
            }
            catch
            {
                // Skip projects that fail to load (missing SDK, etc.)
            }
        }

        if (typeMap.Count == 0 && methodMap.Count == 0)
            TryIndexDirectoriesWithAdHocCompilation(normalizedPaths, typeMap, methodMap);

        return typeMap.Count == 0 && methodMap.Count == 0
            ? null
            : new RoslynDocumentationIndexer(typeMap, methodMap);
    }

    private static void TryIndexDirectoriesWithAdHocCompilation(
        string[] documentationRoots,
        Dictionary<string, string> typeMap,
        Dictionary<string, List<string>> methodMap)
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

            IndexCompilation(compilation, typeMap, methodMap);
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
        Dictionary<string, List<string>> methodMap)
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
                            TryAddSymbol(typeMap, symbol);
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
        var domainText = ExtractDomainTags(xml);
        if (domainText is null)
            return;

        MergeDomainText(map, ToClrMetadataFullName(symbol), domainText);

        if (symbol is { IsGenericType: true, IsUnboundGenericType: false })
            MergeDomainText(map, ToClrMetadataFullName(symbol.OriginalDefinition), domainText);
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

    private static string? ExtractDomainTags(string? documentationXml)
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
            var normalized = NormalizeDomainInnerXml(raw);
            if (normalized.Length > 0)
                segments.Add(normalized);
        }

        return segments.Count == 0 ? null : string.Join("; ", segments);
    }

    /// <summary>
    /// Strips XML doc tags inside &lt;domain&gt; and collapses whitespace so
    /// <c>emits &lt;see cref="T:Ns.Event"/&gt;</c> becomes a readable line.
    /// </summary>
    private static string NormalizeDomainInnerXml(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        try
        {
            var wrapped = "<r>" + raw + "</r>";
            var el = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
            var text = string.Concat(el.Nodes().Select(NodeToPlainText)).Trim();
            return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
        catch
        {
            return string.Join(' ', raw.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    private static string NodeToPlainText(XNode node)
    {
        return node switch
        {
            XText t => t.Value,
            XElement e when e.Name.LocalName.Equals("see", StringComparison.OrdinalIgnoreCase) =>
                e.Attribute("cref")?.Value is { } cref
                    ? StripDocIdPrefix(cref)
                    : string.Empty,
            XElement e => string.Concat(e.Nodes().Select(NodeToPlainText)),
            _ => string.Empty
        };
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
