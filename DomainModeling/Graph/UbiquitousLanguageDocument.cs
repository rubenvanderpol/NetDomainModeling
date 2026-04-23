namespace DomainModeling.Graph;

/// <summary>
/// Structured ubiquitous language view of a <see cref="DomainGraph"/> for UI or export.
/// </summary>
public sealed class UbiquitousLanguageDocument
{
    /// <summary>BCP 47 or custom key for the phrase set used to build this document.</summary>
    public required string Language { get; init; }

    /// <summary>Languages the host registered (for UI pickers).</summary>
    public IReadOnlyList<string> AvailableLanguages { get; init; } = [];

    /// <summary>
    /// When the client requested a bounded-context filter, lists the names that were applied (subset of the full graph).
    /// <c>null</c> when the full model is included.
    /// </summary>
    public List<string>? FilteredToBoundedContexts { get; init; }

    public required string Title { get; init; }
    public string? Introduction { get; init; }

    /// <summary>Section title for aggregates (localized).</summary>
    public required string AggregatesSectionLabel { get; init; }

    /// <summary>Section title for domain events (localized).</summary>
    public required string DomainEventsSectionLabel { get; init; }

    /// <summary>Heading for the relations list under each concept (localized).</summary>
    public required string RelationsHeadingLabel { get; init; }

    /// <summary>Prefix before CLR type short name (localized, e.g. "Type").</summary>
    public required string TypeLabelPrefix { get; init; }

    /// <summary>Message when a concept has no structural outgoing relations.</summary>
    public required string NoRelationsMessage { get; init; }

    /// <summary>Format string with <c>{0}</c> = bounded context name (Markdown <c>##</c> line).</summary>
    public required string BoundedContextMarkdownHeadingFormat { get; init; }

    /// <summary>Word before property labels in relation lines (Markdown), e.g. English "via".</summary>
    public required string RelationshipViaWord { get; init; }

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
/// Builds <see cref="UbiquitousLanguageDocument"/> from a domain graph using <see cref="UbiquitousLanguageDefinition"/> phrases.
/// </summary>
public static class UbiquitousLanguageDocumentBuilder
{
    /// <summary>
    /// Produces a structured document for APIs and renderers using the default English definition.
    /// </summary>
    public static UbiquitousLanguageDocument Build(DomainGraph graph) =>
        Build(graph, UbiquitousLanguageDefinition.CreateDefault(), language: null);

    /// <summary>
    /// Produces a structured document with optional custom definition and language key (<c>null</c> = definition default).
    /// </summary>
    public static UbiquitousLanguageDocument Build(
        DomainGraph graph,
        UbiquitousLanguageDefinition definition,
        string? language) =>
        Build(graph, definition, language, boundedContextNames: null);

    /// <summary>
    /// Produces a structured document, optionally restricted to the given bounded context names (case-insensitive).
    /// When <paramref name="boundedContextNames"/> is null or empty, the full graph is used.
    /// </summary>
    public static UbiquitousLanguageDocument Build(
        DomainGraph graph,
        UbiquitousLanguageDefinition definition,
        string? language,
        IReadOnlyList<string>? boundedContextNames)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(definition);

        var resolvedLang = string.IsNullOrWhiteSpace(language) ? definition.DefaultLanguage : language.Trim();
        var phrases = definition.ResolvePhrases(resolvedLang);
        var impl = new Impl(definition, phrases, resolvedLang);
        return impl.Build(graph, boundedContextNames);
    }

    private sealed class Impl(UbiquitousLanguageDefinition definition, UbiquitousLanguagePhrases phrases, string resolvedLang)
    {
        private readonly int _maxDepth = definition.MaxConceptDepth;

        private static readonly HashSet<RelationshipKind> StructuralOutgoingKinds =
        [
            RelationshipKind.Has,
            RelationshipKind.HasMany,
            RelationshipKind.Contains,
            RelationshipKind.References,
            RelationshipKind.ReferencesById,
        ];

        public UbiquitousLanguageDocument Build(DomainGraph graph, IReadOnlyList<string>? boundedContextNames)
        {
            List<string>? filterApplied = null;
            IEnumerable<BoundedContextNode> sourceContexts = graph.BoundedContexts;
            if (boundedContextNames is { Count: > 0 })
            {
                var wanted = new HashSet<string>(
                    boundedContextNames.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                if (wanted.Count > 0)
                {
                    sourceContexts = graph.BoundedContexts.Where(c => wanted.Contains(c.Name)).ToList();
                    filterApplied = sourceContexts.Select(c => c.Name).ToList();
                }
            }

            var contexts = new List<UbiquitousLanguageBoundedContext>();
            foreach (var ctx in sourceContexts)
                contexts.Add(BuildBoundedContext(ctx));

            return new UbiquitousLanguageDocument
            {
                Language = resolvedLang,
                AvailableLanguages = definition.LanguageKeys,
                FilteredToBoundedContexts = filterApplied,
                Title = phrases.Title,
                Introduction = phrases.Introduction,
                AggregatesSectionLabel = phrases.MarkdownSectionAggregates,
                DomainEventsSectionLabel = phrases.MarkdownSectionDomainEvents,
                RelationsHeadingLabel = phrases.MarkdownRelationsHeading,
                TypeLabelPrefix = phrases.MarkdownTypePrefix,
                NoRelationsMessage = phrases.NoRelationsFromConcept,
                BoundedContextMarkdownHeadingFormat = phrases.MarkdownBoundedContextHeadingFormat,
                RelationshipViaWord = phrases.MarkdownRelationshipViaWord,
                BoundedContexts = contexts,
            };
        }

        private UbiquitousLanguageBoundedContext BuildBoundedContext(BoundedContextNode ctx)
        {
            UbiquitousLanguageAggregateSection aggregateSection;
            if (ctx.Aggregates.Count == 0)
            {
                aggregateSection = new UbiquitousLanguageAggregateSection
                {
                    EmptyMessage = phrases.NoAggregatesInContext,
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
                    EmptyMessage = phrases.NoDomainEventsInContext,
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

        private UbiquitousLanguageConceptBlock BuildAggregateTree(
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
                KindLabel = phrases.KindAggregate,
                TypeName = aggregate.Name,
                FullName = aggregate.FullName,
                Description = string.IsNullOrWhiteSpace(aggregate.Description) ? null : aggregate.Description.Trim(),
                Relations = relations,
                LinkedConcepts = linked,
            };
        }

        private List<UbiquitousLanguageConceptBlock> BuildLinkedConcepts(
            BoundedContextNode ctx,
            string sourceFullName,
            Dictionary<string, string> displayByFullName,
            TypeMaps maps,
            HashSet<string> visited,
            int depth)
        {
            if (depth >= _maxDepth)
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
                    KindLabel = phrases.KindLabelFor(concept.InternalKind),
                    TypeName = concept.Name,
                    FullName = concept.FullName,
                    Description = string.IsNullOrWhiteSpace(concept.Description) ? null : concept.Description!.Trim(),
                    Relations = relBlock,
                    LinkedConcepts = nested,
                });
            }

            return list;
        }

        private UbiquitousLanguageRelationsBlock BuildRelationsBlock(
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
                    Phrase = phrases.RelationshipPhrase(r.Kind),
                    TargetDisplayName = TargetDisplayOnly(r.TargetType, displayByFullName),
                    TargetFullName = r.TargetType,
                    ViaLabel = string.IsNullOrWhiteSpace(r.Label) ? null : r.Label,
                })
                .ToList();

            return new UbiquitousLanguageRelationsBlock { Items = relations };
        }

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

        private readonly struct DomainConcept(string fullName, string name, string? description, string internalKind)
        {
            public string FullName { get; } = fullName;
            public string Name { get; } = name;
            public string? Description { get; } = description;
            public string InternalKind { get; } = internalKind;
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
}
