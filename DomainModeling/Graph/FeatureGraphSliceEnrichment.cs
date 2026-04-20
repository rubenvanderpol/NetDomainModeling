namespace DomainModeling.Graph;

/// <summary>
/// Merges metadata from the full scanned <see cref="DomainGraph"/> into a feature slice graph
/// built from <c>feature.json</c>, so exports include documentation summaries and discovered
/// bounded-context names when the feature file omits them.
/// </summary>
public static class FeatureGraphSliceEnrichment
{
    public static void Apply(DomainGraph slice, DomainGraph fullGraph)
    {
        ArgumentNullException.ThrowIfNull(slice);
        ArgumentNullException.ThrowIfNull(fullGraph);

        var lookup = BuildLookup(fullGraph);
        foreach (var ctx in slice.BoundedContexts)
        {
            foreach (var n in ctx.Aggregates) ApplyTo(n, lookup);
            foreach (var n in ctx.Entities) ApplyTo(n, lookup);
            foreach (var n in ctx.ValueObjects) ApplyTo(n, lookup);
            foreach (var n in ctx.DomainEvents) ApplyTo(n, lookup);
            foreach (var n in ctx.IntegrationEvents) ApplyTo(n, lookup);
            foreach (var n in ctx.CommandHandlerTargets) ApplyTo(n, lookup);
            foreach (var n in ctx.EventHandlers) ApplyTo(n, lookup);
            foreach (var n in ctx.CommandHandlers) ApplyTo(n, lookup);
            foreach (var n in ctx.QueryHandlers) ApplyTo(n, lookup);
            foreach (var n in ctx.Repositories) ApplyTo(n, lookup);
            foreach (var n in ctx.DomainServices) ApplyTo(n, lookup);
            foreach (var n in ctx.SubTypes) ApplyTo(n, lookup);
        }
    }

    private static void ApplyTo(AggregateNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(EntityNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(ValueObjectNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(DomainEventNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(CommandHandlerTargetNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(HandlerNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(RepositoryNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(DomainServiceNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private static void ApplyTo(SubTypeNode n, IReadOnlyDictionary<string, DiscoveryInfo> lookup)
    {
        if (!lookup.TryGetValue(n.FullName, out var d)) return;
        if (string.IsNullOrWhiteSpace(n.Description) && !string.IsNullOrWhiteSpace(d.Description))
            n.Description = d.Description;
        if (string.IsNullOrWhiteSpace(n.BoundedContextName) && !string.IsNullOrWhiteSpace(d.BoundedContextName))
            n.BoundedContextName = d.BoundedContextName;
    }

    private readonly record struct DiscoveryInfo(string? Description, string? BoundedContextName);

    private static Dictionary<string, DiscoveryInfo> BuildLookup(DomainGraph graph)
    {
        var d = new Dictionary<string, DiscoveryInfo>(StringComparer.Ordinal);
        foreach (var ctx in graph.BoundedContexts)
        {
            var bc = ctx.Name;
            void Add(string fullName, string? description)
            {
                if (string.IsNullOrEmpty(fullName)) return;
                d[fullName] = new DiscoveryInfo(description, bc);
            }

            foreach (var n in ctx.Aggregates) Add(n.FullName, n.Description);
            foreach (var n in ctx.Entities) Add(n.FullName, n.Description);
            foreach (var n in ctx.ValueObjects) Add(n.FullName, n.Description);
            foreach (var n in ctx.DomainEvents) Add(n.FullName, n.Description);
            foreach (var n in ctx.IntegrationEvents) Add(n.FullName, n.Description);
            foreach (var n in ctx.CommandHandlerTargets) Add(n.FullName, n.Description);
            foreach (var n in ctx.EventHandlers) Add(n.FullName, n.Description);
            foreach (var n in ctx.CommandHandlers) Add(n.FullName, n.Description);
            foreach (var n in ctx.QueryHandlers) Add(n.FullName, n.Description);
            foreach (var n in ctx.Repositories) Add(n.FullName, n.Description);
            foreach (var n in ctx.DomainServices) Add(n.FullName, n.Description);
            foreach (var n in ctx.SubTypes) Add(n.FullName, n.Description);
        }

        return d;
    }
}
