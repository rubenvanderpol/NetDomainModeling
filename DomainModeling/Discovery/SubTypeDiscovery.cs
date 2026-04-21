using System.Reflection;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Discovers unregistered types referenced from entity / aggregate / value object properties.
/// </summary>
internal static class SubTypeDiscovery
{
    public static List<SubTypeNode> Discover(
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<ValueObjectNode> valueObjectNodes,
        HashSet<string> knownDomainTypes,
        List<Type> allTypes,
        List<Relationship> relationships)
    {
        var allPropertySources = entityNodes.SelectMany(e => e.Properties)
            .Concat(aggregateNodes.SelectMany(a => a.Properties))
            .Concat(valueObjectNodes.SelectMany(v => v.Properties));

        var subTypeFullNames = allPropertySources
            .Where(p => p.ReferenceTypeName is not null && !knownDomainTypes.Contains(p.ReferenceTypeName))
            .Select(p => p.ReferenceTypeName!)
            .ToHashSet();

        var typeMap = allTypes
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!)
            .ToDictionary(g => g.Key, g => g.First());
        var subTypeNodes = new List<SubTypeNode>();
        var processed = new HashSet<string>();
        var queue = new Queue<string>(subTypeFullNames);

        while (queue.Count > 0)
        {
            var fullName = queue.Dequeue();
            if (!processed.Add(fullName)) continue;
            if (knownDomainTypes.Contains(fullName)) continue;
            if (!typeMap.TryGetValue(fullName, out var type)) continue;

            var properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes);
            subTypeNodes.Add(new SubTypeNode
            {
                Name = TypeDisplayNames.ShortName(type),
                FullName = fullName,
                Properties = properties
            });

            foreach (var prop in properties.Where(p => p.ReferenceTypeName is not null))
            {
                if (!knownDomainTypes.Contains(prop.ReferenceTypeName!) && !processed.Contains(prop.ReferenceTypeName!))
                {
                    queue.Enqueue(prop.ReferenceTypeName!);
                }

                relationships.Add(new Relationship
                {
                    SourceType = fullName,
                    TargetType = prop.ReferenceTypeName!,
                    Kind = prop.IsCollection ? RelationshipKind.HasMany : RelationshipKind.Has,
                    Label = prop.Name
                });
            }
        }

        return subTypeNodes;
    }
}
