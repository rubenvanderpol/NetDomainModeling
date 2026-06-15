using System.Reflection;
using TypeScanning.Configuration;
using TypeScanning.Model;

namespace TypeScanning.Discovery;

/// <summary>
/// Scans assemblies configured in a <see cref="ScanContext"/> and produces a <see cref="ScanResult"/>.
/// </summary>
public sealed class TypeScanner
{
    private readonly ScanContext _context;

    public TypeScanner(ScanContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public ScanResult Scan()
    {
        var assemblyOrder = AssemblyTypeLoader.BuildAssemblyOrder(_context.Assemblies);

        var allTypes = AssemblyTypeLoader.DeduplicateTypesByFullName(
            _context.Assemblies.SelectMany(AssemblyTypeLoader.SafeGetTypes)
                .Where(t => t is { IsAbstract: false, IsInterface: false }),
            assemblyOrder);

        var allExportedTypes = AssemblyTypeLoader.DeduplicateTypesByFullName(
            _context.Assemblies.SelectMany(AssemblyTypeLoader.SafeGetTypes),
            assemblyOrder);

        bool OwnedElsewhere(Type t) => _context.ExternallyOwnedAssemblies.Contains(t.Assembly);

        var categorizedTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        var allMatchedForKnownTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        var allMatchedTypes = new Dictionary<string, List<Type>>(StringComparer.Ordinal);

        foreach (var category in _context.Categories)
        {
            var allMatched = allTypes.Where(t => category.Convention.Matches(t)).ToList();
            allMatchedTypes[category.Name] = allMatched;

            var listed = allMatched
                .Where(t => ShouldList(t, category, OwnedElsewhere))
                .ToList();

            MergeStructuralTypes(allTypes, OwnedElsewhere, category, listed);

            categorizedTypes[category.Name] = listed;

            var forKnownTypes = category.Options.IncludeExternallyOwnedInKnownTypes
                ? allMatched
                : listed;
            allMatchedForKnownTypes[category.Name] = forKnownTypes;
        }

        var knownTypeFullNames = new HashSet<string>(
            allMatchedForKnownTypes.Values
                .SelectMany(types => types)
                .Select(t => t.FullName!)
                .Where(n => n is not null),
            StringComparer.Ordinal);

        var scannedByCategory = new Dictionary<string, IReadOnlyList<ScannedType>>(StringComparer.Ordinal);
        foreach (var (categoryName, types) in categorizedTypes)
        {
            scannedByCategory[categoryName] = types
                .Select(t => BuildScannedType(t, categoryName, knownTypeFullNames))
                .ToList();
        }

        var propertyRelationships = BuildPropertyRelationships(scannedByCategory);

        return new ScanResult
        {
            ContextName = _context.Name,
            Categories = scannedByCategory,
            ConcreteTypes = allTypes,
            AllExportedTypes = allExportedTypes,
            KnownTypeFullNames = knownTypeFullNames,
            AllMatchedTypes = allMatchedTypes.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<Type>)kvp.Value),
            PropertyRelationships = propertyRelationships
        };
    }

    private static bool ShouldList(Type type, TypeCategory category, Func<Type, bool> ownedElsewhere) =>
        !category.Options.ExcludeWhenExternallyOwned || !ownedElsewhere(type);

    private static void MergeStructuralTypes(
        List<Type> allTypes,
        Func<Type, bool> ownedElsewhere,
        TypeCategory category,
        List<Type> listedTypes)
    {
        var rules = category.Convention.StructuralRules;
        if (rules.Count == 0)
            return;

        var existing = new HashSet<string>(
            listedTypes.Select(t => t.FullName!).Where(n => n is not null),
            StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            foreach (var discoveredType in rule.EnumerateTypes(allTypes))
            {
                if (ownedElsewhere(discoveredType) && category.Options.ExcludeWhenExternallyOwned)
                    continue;

                var fullName = discoveredType.FullName;
                if (fullName is null || !existing.Add(fullName))
                    continue;

                listedTypes.Add(discoveredType);
            }
        }
    }

    private ScannedType BuildScannedType(Type type, string category, HashSet<string> knownTypeFullNames) => new()
    {
        ClrType = type,
        Category = category,
        Layer = _context.LayerResolver?.Invoke(type),
        Properties = ReflectionMetadata.GetProperties(type, knownTypeFullNames),
        Methods = ReflectionMetadata.GetMethods(type)
    };

    private static List<ScannedRelationship> BuildPropertyRelationships(
        IReadOnlyDictionary<string, IReadOnlyList<ScannedType>> scannedByCategory)
    {
        var relationships = new List<ScannedRelationship>();

        foreach (var types in scannedByCategory.Values)
        {
            foreach (var scannedType in types)
            {
                foreach (var prop in scannedType.Properties.Where(p => p.ReferenceTypeName is not null))
                {
                    relationships.Add(new ScannedRelationship
                    {
                        SourceType = scannedType.FullName,
                        TargetType = prop.ReferenceTypeName!,
                        Kind = prop.IsCollection ? RelationshipKind.HasMany : RelationshipKind.Has,
                        Label = prop.Name
                    });
                }
            }
        }

        return relationships;
    }
}
