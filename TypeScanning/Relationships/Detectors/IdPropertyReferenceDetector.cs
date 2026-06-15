using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships from ID-style properties (e.g. <c>OrganizationId</c> → <c>Organization</c>).
/// </summary>
public sealed class IdPropertyReferenceDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly IdPropertyReferenceOptions _options;

    public IdPropertyReferenceDetector(string relationshipName, string[] sourceCategories, IdPropertyReferenceOptions options)
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
        var suffix = _options.Suffix;
        if (string.IsNullOrWhiteSpace(suffix))
            yield break;

        var existing = new HashSet<(string Source, string Target)>();

        foreach (var source in context.GetTypesInCategories(_sourceCategories))
        {
            foreach (var prop in source.Properties)
            {
                if (prop.ReferenceTypeName is not null)
                    continue;
                if (prop.Name.Length <= suffix.Length)
                    continue;
                if (!prop.Name.EndsWith(suffix, StringComparison.Ordinal))
                    continue;

                var candidateName = prop.Name[..^suffix.Length];
                if (!context.TryResolveTypeName(candidateName, out var targetFullName, _options.PreferTargetCategories))
                    continue;
                if (targetFullName == source.FullName)
                    continue;
                if (!existing.Add((source.FullName, targetFullName)))
                    continue;

                yield return new ScannedRelationship
                {
                    Kind = _relationshipName,
                    SourceType = source.FullName,
                    TargetType = targetFullName,
                    Label = prop.Name
                };
            }
        }
    }
}
