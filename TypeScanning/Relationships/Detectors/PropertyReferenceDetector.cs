using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships from object-reference properties on source types.
/// </summary>
public sealed class PropertyReferenceDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly PropertyReferenceOptions _options;

    public PropertyReferenceDetector(string relationshipName, string[] sourceCategories, PropertyReferenceOptions options)
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
        HashSet<string>? allowedTargets = null;
        if (_options.TargetCategories is { Length: > 0 })
            allowedTargets = context.GetFullNamesInCategories(_options.TargetCategories);

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
                if (_options.TargetMustBeKnown && !context.KnownTypeFullNames.Contains(prop.ReferenceTypeName))
                    continue;
                if (allowedTargets is not null && !allowedTargets.Contains(prop.ReferenceTypeName))
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
