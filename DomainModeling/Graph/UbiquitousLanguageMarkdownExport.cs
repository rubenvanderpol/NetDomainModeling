using System.Text;

namespace DomainModeling.Graph;

/// <summary>
/// Builds a Markdown document that summarizes the ubiquitous language for an entire <see cref="DomainGraph"/>:
/// bounded contexts with aggregates (descriptions and structural relations), recursively linked entities,
/// value objects, and sub-types up to a fixed depth, then domain events.
/// </summary>
public static class UbiquitousLanguageMarkdownExport
{
    /// <summary>Maximum number of hops from an aggregate root into linked domain concepts (entities, value objects, sub-types, other aggregates).</summary>
    private const int MaxConceptDepth = 4;

    private static readonly HashSet<RelationshipKind> StructuralOutgoingKinds =
    [
        RelationshipKind.Has,
        RelationshipKind.HasMany,
        RelationshipKind.Contains,
        RelationshipKind.References,
        RelationshipKind.ReferencesById,
    ];

    /// <summary>
    /// Creates Markdown suitable for download or documentation (e.g. registered via <c>AddExport</c> in ASP.NET Core).
    /// </summary>
    public static string Build(DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var sb = new StringBuilder();
        sb.AppendLine("# Ubiquitous language");
        sb.AppendLine();
        sb.AppendLine("This document is generated from the domain model. It is grouped by bounded context: aggregates with linked entities, value objects, and sub-types (structural relations, limited depth), then domain events.");
        sb.AppendLine();

        foreach (var ctx in graph.BoundedContexts)
            AppendBoundedContext(sb, ctx);

        return sb.ToString();
    }

    private static void AppendBoundedContext(StringBuilder sb, BoundedContextNode ctx)
    {
        sb.AppendLine($"## Bounded context: {ctx.Name}");
        sb.AppendLine();

        sb.AppendLine("### Aggregates");
        sb.AppendLine();

        if (ctx.Aggregates.Count == 0)
        {
            sb.AppendLine("_No aggregates in this bounded context._");
            sb.AppendLine();
        }
        else
        {
            var displayByFullName = BuildDisplayLookup(ctx);
            var typeMaps = TypeMaps.ForContext(ctx);

            foreach (var aggregate in ctx.Aggregates.OrderBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase))
                AppendAggregateWithLinkedConcepts(sb, ctx, aggregate, displayByFullName, typeMaps);
        }

        sb.AppendLine("### Domain events");
        sb.AppendLine();

        if (ctx.DomainEvents.Count == 0)
        {
            sb.AppendLine("_No domain events in this bounded context._");
            sb.AppendLine();
        }
        else
        {
            foreach (var ev in ctx.DomainEvents.OrderBy(e => DisplayName(e), StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"#### {DisplayName(ev)}");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(ev.Description))
                {
                    sb.AppendLine(ev.Description.Trim());
                    sb.AppendLine();
                }

                sb.AppendLine($"_Type:_ `{ev.Name}`");
                sb.AppendLine();
            }
        }
    }

    private static void AppendAggregateWithLinkedConcepts(
        StringBuilder sb,
        BoundedContextNode ctx,
        AggregateNode aggregate,
        Dictionary<string, string> displayByFullName,
        TypeMaps maps)
    {
        sb.AppendLine($"#### {DisplayName(aggregate)}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(aggregate.Description))
        {
            sb.AppendLine(aggregate.Description.Trim());
            sb.AppendLine();
        }

        sb.AppendLine($"_Type:_ `{aggregate.Name}`");
        sb.AppendLine();

        var visited = new HashSet<string>(StringComparer.Ordinal) { aggregate.FullName };
        AppendRelationsAndLinkedConcepts(
            sb,
            ctx,
            aggregate.FullName,
            displayByFullName,
            maps,
            visited,
            depth: 1,
            indent: "");
    }

    private static void AppendRelationsAndLinkedConcepts(
        StringBuilder sb,
        BoundedContextNode ctx,
        string sourceFullName,
        Dictionary<string, string> displayByFullName,
        TypeMaps maps,
        HashSet<string> visited,
        int depth,
        string indent)
    {
        if (depth > MaxConceptDepth)
            return;

        var relations = ctx.Relationships
            .Where(r =>
                string.Equals(r.SourceType, sourceFullName, StringComparison.Ordinal) &&
                StructuralOutgoingKinds.Contains(r.Kind))
            .OrderBy(r => r.Kind)
            .ThenBy(r => TargetLabel(r.TargetType, displayByFullName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        sb.AppendLine($"{indent}**Relations**");
        sb.AppendLine();

        if (relations.Count == 0)
        {
            sb.AppendLine($"{indent}_None from this concept._");
            sb.AppendLine();
        }
        else
        {
            foreach (var r in relations)
            {
                var phrase = RelationshipPhrase(r.Kind);
                var target = TargetLabel(r.TargetType, displayByFullName);
                var via = string.IsNullOrWhiteSpace(r.Label) ? "" : $" _(via `{r.Label}`)_";
                sb.AppendLine($"{indent}- **{phrase}** {target}{via}");
            }

            sb.AppendLine();
        }

        var childIndent = indent + "  ";
        if (depth >= MaxConceptDepth)
            return;

        foreach (var r in relations)
        {
            if (!maps.TryGetConcept(r.TargetType, out var concept))
                continue;

            if (visited.Contains(r.TargetType))
                continue;

            visited.Add(r.TargetType);
            AppendConceptBlock(sb, concept, displayByFullName, childIndent);
            AppendRelationsAndLinkedConcepts(
                sb,
                ctx,
                r.TargetType,
                displayByFullName,
                maps,
                visited,
                depth + 1,
                childIndent);
        }
    }

    private static void AppendConceptBlock(
        StringBuilder sb,
        DomainConcept concept,
        Dictionary<string, string> displayByFullName,
        string indent)
    {
        var display = displayByFullName.TryGetValue(concept.FullName, out var d) ? d : concept.Name;
        var kindLabel = concept.KindLabel;

        sb.AppendLine($"{indent}**{display}** _({kindLabel})_");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(concept.Description))
        {
            foreach (var line in concept.Description!.Trim().Split('\n'))
                sb.AppendLine($"{indent}{line.TrimEnd()}");

            sb.AppendLine();
        }

        sb.AppendLine($"{indent}_Type:_ `{concept.Name}`");
        sb.AppendLine();
    }

    private static string RelationshipPhrase(RelationshipKind kind) => kind switch
    {
        RelationshipKind.Has => "has",
        RelationshipKind.HasMany => "has many",
        RelationshipKind.Contains => "contains",
        RelationshipKind.References => "references",
        RelationshipKind.ReferencesById => "references by id",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static Dictionary<string, string> BuildDisplayLookup(BoundedContextNode ctx)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string fullName, string name, string? alias)
        {
            map[fullName] = string.IsNullOrWhiteSpace(alias) ? name : alias.Trim();
        }

        foreach (var n in ctx.Aggregates)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.Entities)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.ValueObjects)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.DomainEvents)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.IntegrationEvents)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.SubTypes)
            Add(n.FullName, n.Name, n.Alias);

        return map;
    }

    private static string TargetLabel(string targetFullName, Dictionary<string, string> displayByFullName)
    {
        if (displayByFullName.TryGetValue(targetFullName, out var label))
            return $"`{label}`";

        var shortName = targetFullName.Split('.').LastOrDefault() ?? targetFullName;
        return $"`{shortName}`";
    }

    private static string DisplayName(AggregateNode n) =>
        string.IsNullOrWhiteSpace(n.Alias) ? n.Name : n.Alias.Trim();

    private static string DisplayName(DomainEventNode n) =>
        string.IsNullOrWhiteSpace(n.Alias) ? n.Name : n.Alias.Trim();

    private readonly struct DomainConcept(string fullName, string name, string? description, string kindLabel)
    {
        public string FullName { get; } = fullName;
        public string Name { get; } = name;
        public string? Description { get; } = description;
        public string KindLabel { get; } = kindLabel;
    }

    private sealed class TypeMaps
    {
        private readonly Dictionary<string, AggregateNode> _aggregates;
        private readonly Dictionary<string, EntityNode> _entities;
        private readonly Dictionary<string, ValueObjectNode> _valueObjects;
        private readonly Dictionary<string, SubTypeNode> _subTypes;

        private TypeMaps(
            Dictionary<string, AggregateNode> aggregates,
            Dictionary<string, EntityNode> entities,
            Dictionary<string, ValueObjectNode> valueObjects,
            Dictionary<string, SubTypeNode> subTypes)
        {
            _aggregates = aggregates;
            _entities = entities;
            _valueObjects = valueObjects;
            _subTypes = subTypes;
        }

        public static TypeMaps ForContext(BoundedContextNode ctx) => new(
            ctx.Aggregates.ToDictionary(a => a.FullName, a => a, StringComparer.Ordinal),
            ctx.Entities.ToDictionary(e => e.FullName, e => e, StringComparer.Ordinal),
            ctx.ValueObjects.ToDictionary(v => v.FullName, v => v, StringComparer.Ordinal),
            ctx.SubTypes.ToDictionary(s => s.FullName, s => s, StringComparer.Ordinal));

        public bool TryGetConcept(string fullName, out DomainConcept concept)
        {
            if (_aggregates.TryGetValue(fullName, out var a))
            {
                concept = new DomainConcept(a.FullName, a.Name, a.Description, "aggregate");
                return true;
            }

            if (_entities.TryGetValue(fullName, out var e))
            {
                concept = new DomainConcept(e.FullName, e.Name, e.Description, "entity");
                return true;
            }

            if (_valueObjects.TryGetValue(fullName, out var v))
            {
                concept = new DomainConcept(v.FullName, v.Name, v.Description, "value object");
                return true;
            }

            if (_subTypes.TryGetValue(fullName, out var s))
            {
                concept = new DomainConcept(s.FullName, s.Name, s.Description, "sub-type");
                return true;
            }

            concept = default;
            return false;
        }
    }
}
