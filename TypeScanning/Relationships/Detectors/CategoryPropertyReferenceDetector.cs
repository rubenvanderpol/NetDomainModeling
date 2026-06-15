using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships where a source type has a property referencing a type in a target category.
/// </summary>
public sealed class CategoryPropertyReferenceDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly CategoryPropertyReferenceOptions _options;

    public CategoryPropertyReferenceDetector(
        string relationshipName,
        string[] sourceCategories,
        CategoryPropertyReferenceOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipName);
        ArgumentNullException.ThrowIfNull(sourceCategories);
        ArgumentNullException.ThrowIfNull(options);
        _relationshipName = relationshipName;
        _sourceCategories = sourceCategories;
        _options = options;
    }

    public IEnumerable<ScannedRelationship> Detect(RelationshipDetectionContext context)
    {
        var targetFullNames = context.GetFullNamesInCategories(_options.TargetCategory);
        if (targetFullNames.Count == 0)
            yield break;

        foreach (var source in context.GetTypesInCategories(_sourceCategories))
        {
            foreach (var prop in source.Properties)
            {
                if (prop.ReferenceTypeName is null)
                    continue;
                if (_options.CollectionsOnly && !prop.IsCollection)
                    continue;
                if (_options.ExcludeCollections && prop.IsCollection)
                    continue;
                if (!targetFullNames.Contains(prop.ReferenceTypeName))
                    continue;

                yield return new ScannedRelationship
                {
                    Kind = _relationshipName,
                    SourceType = source.FullName,
                    TargetType = prop.ReferenceTypeName,
                    Label = prop.Name
                };
            }
        }
    }
}
