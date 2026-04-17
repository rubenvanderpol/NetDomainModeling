using System.Diagnostics.CodeAnalysis;
using System.Text;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    private static void CrossReferenceEvents(
        List<DomainEventNode> eventNodes,
        List<EntityNode> entities,
        List<AggregateNode> aggregates,
        List<HandlerNode> handlers)
    {
        var eventMap = eventNodes.ToDictionary(e => e.FullName);

        foreach (var entity in entities)
        {
            foreach (var evtName in entity.EmittedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(entity.FullName);
            }
        }

        foreach (var agg in aggregates)
        {
            foreach (var evtName in agg.EmittedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(agg.FullName);
            }
        }

        foreach (var handler in handlers)
        {
            foreach (var handled in handler.Handles)
            {
                if (TryResolveEventNode(eventMap, handled, out var evtNode))
                    evtNode.HandledBy.Add(handler.FullName);
            }
        }
    }

    private static string? ResolveCanonicalEventKey(string typeFullName, HashSet<string> registeredEventFullNames)
    {
        if (registeredEventFullNames.Contains(typeFullName))
            return typeFullName;

        var bracket = typeFullName.IndexOf("[[", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            var canonical = ToCanonicalClosedGenericFullName(typeFullName);
            if (canonical is not null && registeredEventFullNames.Contains(canonical))
                return canonical;

            var def = typeFullName[..bracket];
            if (registeredEventFullNames.Contains(def))
                return def;
        }

        return null;
    }

    private static string? ToCanonicalClosedGenericFullName(string clrFullName)
    {
        var outerStart = clrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (outerStart < 0)
            return null;

        var prefix = clrFullName[..outerStart];
        var sb = new StringBuilder(prefix);
        sb.Append('[');

        var i = outerStart + 1;
        var first = true;
        while (i < clrFullName.Length)
        {
            if (clrFullName[i] == '[')
            {
                if (!first) sb.Append(',');
                first = false;
                i++;
                var commaPos = clrFullName.IndexOf(',', i);
                var closeBracket = clrFullName.IndexOf(']', i);
                string argFullName;
                if (commaPos >= 0 && commaPos < closeBracket)
                    argFullName = clrFullName[i..commaPos].Trim();
                else if (closeBracket >= 0)
                    argFullName = clrFullName[i..closeBracket].Trim();
                else
                    return null;

                sb.Append('[');
                sb.Append(argFullName);
                sb.Append(']');

                var depth = 1;
                while (i < clrFullName.Length && depth > 0)
                {
                    if (clrFullName[i] == '[') depth++;
                    else if (clrFullName[i] == ']') depth--;
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static bool TryResolveEventNode(
        Dictionary<string, DomainEventNode> eventMap,
        string typeFullName,
        [NotNullWhen(true)] out DomainEventNode? node)
    {
        if (eventMap.TryGetValue(typeFullName, out node))
            return true;

        var bracket = typeFullName.IndexOf("[[", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            var canonical = ToCanonicalClosedGenericFullName(typeFullName);
            if (canonical is not null && eventMap.TryGetValue(canonical, out node))
                return true;

            if (eventMap.TryGetValue(typeFullName[..bracket], out node))
                return true;
        }

        node = null;
        return false;
    }

    private static void CrossReferencePublishedEvents(
        List<DomainEventNode> integrationEventNodes,
        Dictionary<string, List<string>> handlerPublishedEvents)
    {
        var eventMap = integrationEventNodes.ToDictionary(e => e.FullName);

        foreach (var (handlerFullName, publishedEvents) in handlerPublishedEvents)
        {
            foreach (var evtName in publishedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(handlerFullName);
            }
        }
    }

    private static void MergeTypeDocumentedEmissions(
        List<Type> entityTypes,
        List<Type> aggregateTypes,
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<DomainEventNode> domainEventNodes,
        List<Relationship> relationships,
        RoslynDocumentationIndexer documentationIndexer)
    {
        var eventFullNames = new HashSet<string>(domainEventNodes.Select(e => e.FullName), StringComparer.Ordinal);
        var addedRelationships = new HashSet<(string Source, string Target)>();

        var allTypesByFullName = entityTypes.Concat(aggregateTypes)
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!)
            .ToDictionary(g => g.Key, g => g.First());

        void ProcessEmitter(string emitterFullName, List<string> emittedEvents, List<EventEmissionInfo> eventEmissions)
        {
            if (!allTypesByFullName.TryGetValue(emitterFullName, out var type))
                return;
            var emissions = documentationIndexer.TryGetTypeDocumentedEmissions(type);
            foreach (var (canonicalFullName, _) in emissions)
            {
                if (!eventFullNames.Contains(canonicalFullName))
                    continue;
                if (!emittedEvents.Contains(canonicalFullName))
                    emittedEvents.Add(canonicalFullName);
                if (!eventEmissions.Any(e => e.EventType == canonicalFullName))
                    eventEmissions.Add(new EventEmissionInfo { EventType = canonicalFullName, MethodName = "(documented)" });
                if (addedRelationships.Add((emitterFullName, canonicalFullName)))
                {
                    relationships.Add(new Relationship
                    {
                        SourceType = emitterFullName,
                        TargetType = canonicalFullName,
                        Kind = RelationshipKind.Emits,
                        Label = "emits (documented)"
                    });
                }
            }
        }

        foreach (var entity in entityNodes)
            ProcessEmitter(entity.FullName, entity.EmittedEvents, entity.EventEmissions);
        foreach (var agg in aggregateNodes)
            ProcessEmitter(agg.FullName, agg.EmittedEvents, agg.EventEmissions);
    }

    private static void MergeSyntheticGenericEventNodes(
        IEnumerable<Type> emittingTypes,
        List<DomainEventNode> domainEventNodes,
        HashSet<string> knownDomainTypes,
        List<HandlerNode> eventHandlerNodes,
        RoslynDocumentationIndexer documentationIndexer)
    {
        var existingEventFullNames = new HashSet<string>(domainEventNodes.Select(e => e.FullName), StringComparer.Ordinal);

        foreach (var type in emittingTypes)
        {
            var emissions = documentationIndexer.TryGetTypeDocumentedEmissions(type);
            if (emissions.Count == 0)
                continue;

            foreach (var (canonicalFullName, displayName) in emissions)
            {
                if (existingEventFullNames.Contains(canonicalFullName))
                    continue;

                domainEventNodes.Add(new DomainEventNode
                {
                    Name = displayName,
                    FullName = canonicalFullName,
                });
                existingEventFullNames.Add(canonicalFullName);
                knownDomainTypes.Add(canonicalFullName);
            }
        }

        var eventFullNames = new HashSet<string>(existingEventFullNames, StringComparer.Ordinal);
        foreach (var handler in eventHandlerNodes)
        {
            for (var i = 0; i < handler.Handles.Count; i++)
            {
                var resolved = ResolveCanonicalEventKey(handler.Handles[i], eventFullNames);
                if (resolved is not null)
                    handler.Handles[i] = resolved;
            }
        }
    }
}
