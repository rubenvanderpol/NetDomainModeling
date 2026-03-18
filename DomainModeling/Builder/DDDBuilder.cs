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
        => WithDomainAssembly(contextName, assembly, _ => { });

    /// <summary>
    /// Defines a bounded context by name using a domain assembly and additional configuration.
    /// </summary>
    public DDDBuilder WithDomainAssembly(string contextName, Assembly assembly, Action<BoundedContextBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contextName);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(configure);

        return WithBoundedContext(contextName, ctx =>
        {
            ctx.WithDomainAssembly(assembly);
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

        var contexts = _contextBuilders.Select(b => b.BuildContext()).ToList();
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
