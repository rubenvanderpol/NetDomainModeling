using System.Reflection;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    private static void AddPropertyReferences(IEnumerable<EntityNode> nodes, HashSet<string> knownDomainTypes, List<Relationship> relationships)
    {
        foreach (var node in nodes)
        {
            foreach (var prop in node.Properties.Where(p => p.ReferenceTypeName is not null))
            {
                relationships.Add(new Relationship
                {
                    SourceType = node.FullName,
                    TargetType = prop.ReferenceTypeName!,
                    Kind = prop.IsCollection ? RelationshipKind.HasMany : RelationshipKind.Has,
                    Label = prop.Name
                });
            }
        }
    }

    private List<SubTypeNode> DiscoverSubTypes(
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

            var properties = GetProperties(type, knownDomainTypes);
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

    private static void AddIdBasedReferences(
        IEnumerable<EntityNode> nodes,
        Dictionary<string, string> knownEntityAndAggregateNames,
        List<Relationship> relationships)
    {
        var existingRefs = new HashSet<(string source, string target)>(
            relationships.Select(r => (r.SourceType, r.TargetType)));

        foreach (var node in nodes)
        {
            foreach (var prop in node.Properties)
            {
                if (prop.ReferenceTypeName is not null) continue;

                if (prop.Name.Length <= 2 || !prop.Name.EndsWith("Id", StringComparison.Ordinal)) continue;

                var candidateName = prop.Name[..^2];
                if (!knownEntityAndAggregateNames.TryGetValue(candidateName, out var targetFullName)) continue;

                if (targetFullName == node.FullName) continue;

                if (existingRefs.Contains((node.FullName, targetFullName))) continue;

                existingRefs.Add((node.FullName, targetFullName));
                relationships.Add(new Relationship
                {
                    SourceType = node.FullName,
                    TargetType = targetFullName,
                    Kind = RelationshipKind.ReferencesById,
                    Label = prop.Name
                });
            }
        }
    }

    private static List<Type> DeduplicateTypesByFullName(IEnumerable<Type> types, Dictionary<Assembly, int> assemblyOrder)
    {
        var list = types as IList<Type> ?? types.ToList();
        var withFullName = list.Where(t => t.FullName is not null).ToList();
        var withoutFullName = list.Where(t => t.FullName is null).Distinct().ToList();

        static int Order(Dictionary<Assembly, int> order, Assembly a) =>
            order.TryGetValue(a, out var i) ? i : int.MaxValue;

        var deduped = withFullName
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .Select(g => g.OrderBy(t => Order(assemblyOrder, t.Assembly)).First())
            .ToList();

        return deduped.Concat(withoutFullName).ToList();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
