namespace DomainModeling.Builder;

using System.Reflection;

/// <summary>
/// Entry point for the DDD modeling fluent API.
/// Use this to describe how another project implements DDD,
/// then build a domain graph from its assemblies.
/// </summary>
public sealed class DDDBuilder
{
    private readonly List<BoundedContextBuilder> _contextBuilders = [];
    private readonly Action<BoundedContextBuilder>? _sharedConfiguration;
    private readonly List<Assembly> _sharedAssemblies = [];
    private readonly List<(string BoundedContextName, Assembly Assembly)> _namedSharedAssemblies = [];

    private DDDBuilder(Action<BoundedContextBuilder>? sharedConfiguration = null)
    {
        _sharedConfiguration = sharedConfiguration;
    }

    /// <summary>
    /// Creates a new DDD builder.
    /// </summary>
    public static DDDBuilder Create() => new();

    /// <summary>
    /// Creates a new DDD builder with shared configuration that is applied to all bounded contexts.
    /// </summary>
    public static DDDBuilder Create(Action<BoundedContextBuilder> configure) => new(configure);

    /// <summary>
    /// Define a bounded context by name, configuring its assemblies and type conventions.
    /// Shared configuration (if any) is applied before the per-context configuration.
    /// </summary>
    public DDDBuilder WithBoundedContext(string name, Action<BoundedContextBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new BoundedContextBuilder(name);
        builder.SharedAssemblies.AddRange(_sharedAssemblies);
        _sharedConfiguration?.Invoke(builder);
        configure(builder);
        _contextBuilders.Add(builder);
        return this;
    }

    /// <summary>
    /// Defines a bounded context by name using a domain assembly.
    /// </summary>
    public DDDBuilder WithDomainAssembly(string contextName, Assembly assembly)
        => WithDomainAssembly(contextName, assembly, scanAssemblyForDocumentation: false, _ => { });

    /// <summary>
    /// Defines a bounded context by name using a domain assembly and optional documentation discovery
    /// (see <see cref="BoundedContextBuilder.WithDomainAssembly(Assembly, bool)"/>).
    /// </summary>
    public DDDBuilder WithDomainAssembly(string contextName, Assembly assembly, bool scanAssemblyForDocumentation)
        => WithDomainAssembly(contextName, assembly, scanAssemblyForDocumentation, _ => { });

    /// <summary>
    /// Defines a bounded context by name using a domain assembly and additional configuration.
    /// </summary>
    public DDDBuilder WithDomainAssembly(string contextName, Assembly assembly, Action<BoundedContextBuilder> configure)
        => WithDomainAssembly(contextName, assembly, scanAssemblyForDocumentation: false, configure);

    /// <summary>
    /// Defines a bounded context by name using a domain assembly, optional documentation discovery, and additional configuration.
    /// </summary>
    public DDDBuilder WithDomainAssembly(
        string contextName,
        Assembly assembly,
        bool scanAssemblyForDocumentation,
        Action<BoundedContextBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(configure);

        return WithBoundedContext(contextName, ctx =>
        {
            ctx.WithDomainAssembly(assembly, scanAssemblyForDocumentation);
            configure(ctx);
        });
    }

    /// <summary>
    /// Defines multiple bounded contexts by context name and domain assembly.
    /// </summary>
    public DDDBuilder WithDomainAssemblies(params (string ContextName, Assembly Assembly)[] contexts)
        => WithDomainAssemblies(_ => { }, contexts);

    /// <summary>
    /// Defines multiple bounded contexts by context name and domain assembly, applying
    /// shared additional configuration to each context.
    /// </summary>
    public DDDBuilder WithDomainAssemblies(
        Action<BoundedContextBuilder> configure,
        params (string ContextName, Assembly Assembly)[] contexts)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(contexts);

        foreach (var (contextName, assembly) in contexts)
        {
            WithDomainAssembly(contextName, assembly, configure);
        }

        return this;
    }

    /// <summary>
    /// Adds a shared assembly that will be scanned by all bounded contexts.
    /// Useful for assemblies containing integration events or other cross-cutting types.
    /// </summary>
    public DDDBuilder WithSharedAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _sharedAssemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Registers a shared assembly that every explicit bounded context still scans for discovery
    /// (e.g. integration-event publish/handle wiring), while integration events and command DTOs
    /// from that assembly are listed only under <paramref name="boundedContextName"/>.
    /// A synthetic bounded context with that name is appended when <see cref="Build"/> runs.
    /// </summary>
    public DDDBuilder WithSharedAssembly(Assembly assembly, string boundedContextName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundedContextName);
        _namedSharedAssemblies.Add((boundedContextName.Trim(), assembly));
        return this;
    }

    /// <summary>
    /// Applies shared configuration to all bounded contexts currently defined on this builder.
    /// This allows declaring multiple contexts first, then configuring conventions once.
    /// </summary>
    public DDDBuilder ConfigureBoundedContexts(Action<BoundedContextBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        foreach (var contextBuilder in _contextBuilders)
        {
            configure(contextBuilder);
        }

        return this;
    }

    /// <summary>
    /// Builds the domain graph by scanning all configured bounded contexts.
    /// </summary>
    public Graph.DomainGraph Build()
    {
        if (_contextBuilders.Count == 0)
            throw new InvalidOperationException("At least one bounded context must be configured.");

        var namedContextNames = _namedSharedAssemblies.Select(x => x.BoundedContextName).Distinct(StringComparer.Ordinal).ToList();
        foreach (var name in namedContextNames)
        {
            if (_contextBuilders.Any(b => string.Equals(b.Name, name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Bounded context name '{name}' is already used by WithBoundedContext; choose a different name for WithSharedAssembly(..., boundedContextName).");
            }
        }

        var distinctOwnedAssemblies = _namedSharedAssemblies.Select(x => x.Assembly).Distinct().ToList();
        foreach (var builder in _contextBuilders)
        {
            foreach (var asm in distinctOwnedAssemblies)
                builder.ExternallyOwnedSharedAssemblies.Add(asm);
        }

        var syntheticBuilders = new List<BoundedContextBuilder>();
        foreach (var name in namedContextNames)
        {
            var owned = _namedSharedAssemblies
                .Where(x => string.Equals(x.BoundedContextName, name, StringComparison.Ordinal))
                .Select(x => x.Assembly)
                .Distinct()
                .ToList();

            var sb = new BoundedContextBuilder(name);
            _sharedConfiguration?.Invoke(sb);
            foreach (var asm in owned)
                sb.WithAssembly(asm);
            syntheticBuilders.Add(sb);
        }

        var allBuilders = new List<BoundedContextBuilder>(_contextBuilders);
        allBuilders.AddRange(syntheticBuilders);

        var contexts = allBuilders.Select(b => b.BuildContext()).ToList();
        CrossReferenceIntegrationEvents(contexts);
        return new Graph.DomainGraph(contexts);
    }

    /// <summary>
    /// After all bounded contexts are scanned independently, cross-reference integration
    /// events across contexts. This ensures that if Context A publishes an integration event
    /// and Context B handles it, both contexts' event nodes and relationship lists reflect
    /// the full picture.
    /// </summary>
    private static void CrossReferenceIntegrationEvents(List<Graph.BoundedContextNode> contexts)
    {
        if (contexts.Count < 2) return;

        // Collect all publishers and handlers for each integration event across all contexts
        var globalEmittedBy = new Dictionary<string, HashSet<string>>();
        var globalHandledBy = new Dictionary<string, HashSet<string>>();

        foreach (var ctx in contexts)
        {
            foreach (var evt in ctx.IntegrationEvents)
            {
                if (!globalEmittedBy.ContainsKey(evt.FullName))
                    globalEmittedBy[evt.FullName] = [];
                if (!globalHandledBy.ContainsKey(evt.FullName))
                    globalHandledBy[evt.FullName] = [];

                foreach (var e in evt.EmittedBy) globalEmittedBy[evt.FullName].Add(e);
                foreach (var h in evt.HandledBy) globalHandledBy[evt.FullName].Add(h);
            }
        }

        // Publishers/handlers often live in other assemblies than the integration contract assembly,
        // so Publishes/Handles edges may exist even when this context has no integration-event node.
        var integrationEventNames = new HashSet<string>(
            contexts.SelectMany(c => c.IntegrationEvents).Select(e => e.FullName),
            StringComparer.Ordinal);

        foreach (var ctx in contexts)
        {
            foreach (var rel in ctx.Relationships)
            {
                if (!integrationEventNames.Contains(rel.TargetType))
                    continue;

                if (rel.Kind == Graph.RelationshipKind.Publishes)
                {
                    if (!globalEmittedBy.ContainsKey(rel.TargetType))
                        globalEmittedBy[rel.TargetType] = [];
                    globalEmittedBy[rel.TargetType].Add(rel.SourceType);
                }
                else if (rel.Kind == Graph.RelationshipKind.Handles)
                {
                    if (!globalHandledBy.ContainsKey(rel.TargetType))
                        globalHandledBy[rel.TargetType] = [];
                    globalHandledBy[rel.TargetType].Add(rel.SourceType);
                }
            }
        }

        // Propagate cross-context references back into each context's integration event nodes
        // and add missing cross-context relationships
        foreach (var ctx in contexts)
        {
            var existingRels = new HashSet<(string Source, string Target, Graph.RelationshipKind Kind)>(
                ctx.Relationships.Select(r => (r.SourceType, r.TargetType, r.Kind)));

            foreach (var evt in ctx.IntegrationEvents)
            {
                // Merge global EmittedBy/HandledBy into this node
                if (globalEmittedBy.TryGetValue(evt.FullName, out var allEmitters))
                {
                    foreach (var emitter in allEmitters)
                    {
                        if (!evt.EmittedBy.Contains(emitter))
                            evt.EmittedBy.Add(emitter);
                    }
                }

                if (globalHandledBy.TryGetValue(evt.FullName, out var allHandlers))
                {
                    foreach (var handler in allHandlers)
                    {
                        if (!evt.HandledBy.Contains(handler))
                            evt.HandledBy.Add(handler);

                        // Add a Handles relationship if the handler exists in this context
                        var rel = (handler, evt.FullName, Graph.RelationshipKind.Handles);
                        if (!existingRels.Contains(rel) && ctx.EventHandlers.Any(h => h.FullName == handler))
                        {
                            ctx.Relationships.Add(new Graph.Relationship
                            {
                                SourceType = handler,
                                TargetType = evt.FullName,
                                Kind = Graph.RelationshipKind.Handles,
                                Label = "handles"
                            });
                            existingRels.Add(rel);
                        }
                    }
                }

                // Add Publishes relationships for emitters that exist in this context
                foreach (var emitter in evt.EmittedBy)
                {
                    var rel = (emitter, evt.FullName, Graph.RelationshipKind.Publishes);
                    if (!existingRels.Contains(rel) && ctx.EventHandlers.Any(h => h.FullName == emitter))
                    {
                        ctx.Relationships.Add(new Graph.Relationship
                        {
                            SourceType = emitter,
                            TargetType = evt.FullName,
                            Kind = Graph.RelationshipKind.Publishes,
                            Label = "publishes"
                        });
                        existingRels.Add(rel);
                    }
                }
            }
        }
    }
}
