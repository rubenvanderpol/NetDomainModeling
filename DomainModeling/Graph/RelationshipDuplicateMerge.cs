namespace DomainModeling.Graph;

/// <summary>
/// Collapses duplicate diagram links that share the same source type, target type, and
/// <see cref="RelationshipKind"/> (GitHub #26). Applies to every relationship kind.
/// </summary>
internal static class RelationshipDuplicateMerge
{
    /// <summary>
    /// Merges relationships that share the same source, target, and kind, combining distinct
    /// non-empty labels (sorted) into one edge.
    /// </summary>
    public static List<Relationship> MergeDuplicateOutgoingLinks(IReadOnlyList<Relationship> relationships)
    {
        var groups = new Dictionary<(string Source, string Target, RelationshipKind Kind), List<Relationship>>();

        foreach (var r in relationships)
        {
            var key = (r.SourceType, r.TargetType, r.Kind);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(r);
        }

        var mergedKeys = new HashSet<(string, string, RelationshipKind)>();
        var result = new List<Relationship>(relationships.Count);

        foreach (var r in relationships)
        {
            var key = (r.SourceType, r.TargetType, r.Kind);
            if (!mergedKeys.Add(key))
                continue;

            var group = groups[key];
            if (group.Count == 1)
            {
                result.Add(group[0]);
                continue;
            }

            var labelParts = group
                .Select(x => x.Label)
                .Where(static s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static s => s, StringComparer.Ordinal)
                .ToList();

            result.Add(new Relationship
            {
                SourceType = r.SourceType,
                TargetType = r.TargetType,
                Kind = r.Kind,
                Label = labelParts.Count > 0 ? string.Join(", ", labelParts) : null
            });
        }

        return result;
    }
}
