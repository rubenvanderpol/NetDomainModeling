namespace DomainModeling.Graph;

/// <summary>
/// Structured ubiquitous language view of a <see cref="DomainGraph"/> for UI or export.
/// </summary>
public sealed class UbiquitousLanguageDocument
{
    public required string Title { get; init; }
    public string? Introduction { get; init; }
    public List<UbiquitousLanguageBoundedContext> BoundedContexts { get; init; } = [];
}

public sealed class UbiquitousLanguageBoundedContext
{
    public required string Name { get; init; }
    public UbiquitousLanguageAggregateSection Aggregates { get; init; } = new();
    public UbiquitousLanguageDomainEventsSection DomainEvents { get; init; } = new();
}

public sealed class UbiquitousLanguageAggregateSection
{
    public string? EmptyMessage { get; init; }
    public List<UbiquitousLanguageConceptBlock> Roots { get; init; } = [];
}

public sealed class UbiquitousLanguageDomainEventsSection
{
    public string? EmptyMessage { get; init; }
    public List<UbiquitousLanguageDomainEventItem> Items { get; init; } = [];
}

/// <summary>
/// An aggregate root and its nested linked concepts (entities, value objects, sub-types, nested aggregates).
/// </summary>
public sealed class UbiquitousLanguageConceptBlock
{
    /// <summary>Depth 0 = aggregate root; deeper values are nested concepts under the same aggregate tree.</summary>
    public int Depth { get; init; }

    public required string DisplayName { get; init; }
    public required string KindLabel { get; init; }
    public required string TypeName { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
    public UbiquitousLanguageRelationsBlock Relations { get; init; } = new();
    public List<UbiquitousLanguageConceptBlock> LinkedConcepts { get; init; } = [];
}

public sealed class UbiquitousLanguageRelationsBlock
{
    public List<UbiquitousLanguageRelationItem> Items { get; init; } = [];
}

public sealed class UbiquitousLanguageRelationItem
{
    public required RelationshipKind Kind { get; init; }
    public required string Phrase { get; init; }
    public required string TargetDisplayName { get; init; }
    public required string TargetFullName { get; init; }
    public string? ViaLabel { get; init; }
}

public sealed class UbiquitousLanguageDomainEventItem
{
    public required string DisplayName { get; init; }
    public required string TypeName { get; init; }
    public required string FullName { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// Builds <see cref="UbiquitousLanguageDocument"/> from a domain graph (same rules as Markdown export).
/// </summary>
public static class UbiquitousLanguageDocumentBuilder
{
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
    /// Produces a structured document for APIs and renderers.
    /// </summary>
    public static UbiquitousLanguageDocument Build(DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var contexts = new List<UbiquitousLanguageBoundedContext>();
        foreach (var ctx in graph.BoundedContexts)
            contexts.Add(BuildBoundedContext(ctx));

        return new UbiquitousLanguageDocument
        {
            Title = "Ubiquitous language",
            Introduction =
                "Generated from the domain model: aggregates with linked entities, value objects, and sub-types (structural relations, limited depth), then domain events — grouped by bounded context.",
            BoundedContexts = contexts,
        };
    }

    private static UbiquitousLanguageBoundedContext BuildBoundedContext(BoundedContextNode ctx)
    {
        UbiquitousLanguageAggregateSection aggregateSection;
        if (ctx.Aggregates.Count == 0)
        {
            aggregateSection = new UbiquitousLanguageAggregateSection
            {
                EmptyMessage = "No aggregates in this bounded context.",
            };
        }
        else
        {
            var displayByFullName = BuildDisplayLookup(ctx);
            var typeMaps = TypeMaps.ForContext(ctx);
            var roots = new List<UbiquitousLanguageConceptBlock>();
            foreach (var aggregate in ctx.Aggregates.OrderBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase))
                roots.Add(BuildAggregateTree(ctx, aggregate, displayByFullName, typeMaps));

            aggregateSection = new UbiquitousLanguageAggregateSection { Roots = roots };
        }

        UbiquitousLanguageDomainEventsSection eventsSection;
        if (ctx.DomainEvents.Count == 0)
        {
            eventsSection = new UbiquitousLanguageDomainEventsSection
            {
                EmptyMessage = "No domain events in this bounded context.",
            };
        }
        else
        {
            var items = ctx.DomainEvents
                .OrderBy(e => DisplayName(e), StringComparer.OrdinalIgnoreCase)
                .Select(e => new UbiquitousLanguageDomainEventItem
                {
                    DisplayName = DisplayName(e),
                    TypeName = e.Name,
                    FullName = e.FullName,
                    Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Trim(),
                })
                .ToList();
            eventsSection = new UbiquitousLanguageDomainEventsSection { Items = items };
        }

        return new UbiquitousLanguageBoundedContext
        {
            Name = ctx.Name,
            Aggregates = aggregateSection,
            DomainEvents = eventsSection,
        };
    }

    private static UbiquitousLanguageConceptBlock BuildAggregateTree(
        BoundedContextNode ctx,
        AggregateNode aggregate,
        Dictionary<string, string> displayByFullName,
        TypeMaps maps)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { aggregate.FullName };
        var relations = BuildRelationsBlock(ctx, aggregate.FullName, displayByFullName);
        var linked = BuildLinkedConcepts(ctx, aggregate.FullName, displayByFullName, maps, visited, depth: 1);

        return new UbiquitousLanguageConceptBlock
        {
            Depth = 0,
            DisplayName = DisplayName(aggregate),
            KindLabel = "aggregate",
            TypeName = aggregate.Name,
            FullName = aggregate.FullName,
            Description = string.IsNullOrWhiteSpace(aggregate.Description) ? null : aggregate.Description.Trim(),
            Relations = relations,
            LinkedConcepts = linked,
        };
    }

    private static List<UbiquitousLanguageConceptBlock> BuildLinkedConcepts(
        BoundedContextNode ctx,
        string sourceFullName,
        Dictionary<string, string> displayByFullName,
        TypeMaps maps,
        HashSet<string> visited,
        int depth)
    {
        if (depth >= MaxConceptDepth)
            return [];

        var relations = ctx.Relationships
            .Where(r =>
                string.Equals(r.SourceType, sourceFullName, StringComparison.Ordinal) &&
                StructuralOutgoingKinds.Contains(r.Kind))
            .OrderBy(r => r.Kind)
            .ThenBy(r => TargetDisplayOnly(r.TargetType, displayByFullName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = new List<UbiquitousLanguageConceptBlock>();
        foreach (var r in relations)
        {
            if (!maps.TryGetConcept(r.TargetType, out var concept))
                continue;
            if (visited.Contains(r.TargetType))
                continue;

            visited.Add(r.TargetType);
            var relBlock = BuildRelationsBlock(ctx, r.TargetType, displayByFullName);
            var nested = BuildLinkedConcepts(ctx, r.TargetType, displayByFullName, maps, visited, depth + 1);

            list.Add(new UbiquitousLanguageConceptBlock
            {
                Depth = depth,
                DisplayName = displayByFullName.TryGetValue(concept.FullName, out var d) ? d : concept.Name,
                KindLabel = concept.KindLabel,
                TypeName = concept.Name,
                FullName = concept.FullName,
                Description = string.IsNullOrWhiteSpace(concept.Description) ? null : concept.Description!.Trim(),
                Relations = relBlock,
                LinkedConcepts = nested,
            });
        }

        return list;
    }

    private static UbiquitousLanguageRelationsBlock BuildRelationsBlock(
        BoundedContextNode ctx,
        string sourceFullName,
        Dictionary<string, string> displayByFullName)
    {
        var relations = ctx.Relationships
            .Where(r =>
                string.Equals(r.SourceType, sourceFullName, StringComparison.Ordinal) &&
                StructuralOutgoingKinds.Contains(r.Kind))
            .OrderBy(r => r.Kind)
            .ThenBy(r => TargetDisplayOnly(r.TargetType, displayByFullName), StringComparer.OrdinalIgnoreCase)
            .Select(r => new UbiquitousLanguageRelationItem
            {
                Kind = r.Kind,
                Phrase = RelationshipPhrase(r.Kind),
                TargetDisplayName = TargetDisplayOnly(r.TargetType, displayByFullName),
                TargetFullName = r.TargetType,
                ViaLabel = string.IsNullOrWhiteSpace(r.Label) ? null : r.Label,
            })
            .ToList();

        return new UbiquitousLanguageRelationsBlock { Items = relations };
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

    private static string TargetDisplayOnly(string targetFullName, Dictionary<string, string> displayByFullName)
    {
        if (displayByFullName.TryGetValue(targetFullName, out var label))
            return label;
        return targetFullName.Split('.').LastOrDefault() ?? targetFullName;
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
