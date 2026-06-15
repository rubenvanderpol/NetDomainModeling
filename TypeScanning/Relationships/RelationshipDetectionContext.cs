using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Read-only snapshot of scan state passed to relationship detectors.
/// </summary>
public sealed class RelationshipDetectionContext
{
    public required string ContextName { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<ScannedType>> Categories { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<Type>> AllMatchedTypes { get; init; }
    public required IReadOnlyList<Type> ConcreteTypes { get; init; }
    public required HashSet<string> KnownTypeFullNames { get; init; }

    /// <summary>
    /// Returns scanned types from the given categories.
    /// </summary>
    public IEnumerable<ScannedType> GetTypesInCategories(params string[] categoryNames)
    {
        foreach (var categoryName in categoryNames)
        {
            if (Categories.TryGetValue(categoryName, out var types))
            {
                foreach (var type in types)
                    yield return type;
            }
        }
    }

    /// <summary>
    /// Returns full type names for types in the given categories.
    /// </summary>
    public HashSet<string> GetFullNamesInCategories(params string[] categoryNames)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in GetTypesInCategories(categoryNames))
            names.Add(type.FullName);
        return names;
    }

    /// <summary>
    /// Resolves a short type name to a full name, preferring categories in order when names collide.
    /// </summary>
    public bool TryResolveTypeName(string shortName, out string fullName, params string[] preferCategories)
    {
        foreach (var categoryName in preferCategories)
        {
            if (!Categories.TryGetValue(categoryName, out var types))
                continue;

            foreach (var type in types)
            {
                if (string.Equals(type.Name, shortName, StringComparison.Ordinal))
                {
                    fullName = type.FullName;
                    return true;
                }
            }
        }

        fullName = "";
        return false;
    }
}
