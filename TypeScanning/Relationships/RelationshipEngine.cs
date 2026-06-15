using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Runs configured relationship rules against a scan result.
/// </summary>
internal static class RelationshipEngine
{
    public static List<ScannedRelationship> DetectAll(
        RelationshipDetectionContext context,
        IReadOnlyList<RelationshipRule> rules)
    {
        var relationships = new List<ScannedRelationship>();
        var seen = new HashSet<(string Kind, string Source, string Target, string? Label)>();

        foreach (var rule in rules)
        {
            foreach (var relationship in rule.Detector.Detect(context))
            {
                var kind = string.IsNullOrWhiteSpace(relationship.Kind) ? rule.Name : relationship.Kind;
                var key = (kind, relationship.SourceType, relationship.TargetType, relationship.Label);
                if (!seen.Add(key))
                    continue;

                relationships.Add(new ScannedRelationship
                {
                    Kind = kind,
                    SourceType = relationship.SourceType,
                    TargetType = relationship.TargetType,
                    Label = relationship.Label
                });
            }
        }

        return relationships;
    }
}
