using System.Diagnostics.CodeAnalysis;
using System.Text;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Resolves generic event CLR names to graph nodes and wires emitted/handled/published cross-references.
/// </summary>
internal static class EventGraphLinker
{
    public static void CrossReferenceEvents(
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

    public static string? ResolveCanonicalEventKey(string typeFullName, HashSet<string> registeredEventFullNames)
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

    public static void CrossReferencePublishedEvents(
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
}
