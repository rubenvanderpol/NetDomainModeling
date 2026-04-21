using System.Reflection;
using DomainModeling.Builder;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Orchestrates assembly scanning: catalog types, build nodes, derive relationships and sub-types.
/// </summary>
internal sealed class DomainDiscoveryPipeline(BoundedContextBuilder config)
{
    private readonly DomainGraphBuilder _nodes = new(config);

    public BoundedContextNode Run()
    {
        var assemblies = config.GetAllAssemblies();
        var assemblyOrder = ScannedTypeCatalog.BuildAssemblyOrderIndex(assemblies);

        var allTypes = ScannedTypeCatalog.DeduplicateTypesByFullName(
            assemblies.SelectMany(ScannedTypeCatalog.SafeGetTypes).Where(t => t is { IsAbstract: false, IsInterface: false }),
            assemblyOrder);

        var categories = ScannedTypeCatalog.CategorizeDomainTypes(config, allTypes);
        var knownDomainTypes = ScannedTypeCatalog.BuildKnownDomainTypeSet(categories);

        var entityNodes = categories.EntityTypes.Select(t =>
            _nodes.BuildEntityNode(t, categories.DomainEventTypes, knownDomainTypes, config.GetLayer(t))).ToList();
        var aggregateNodes = categories.AggregateTypes.Select(t =>
            _nodes.BuildAggregateNode(t, categories.EntityTypes, categories.DomainEventTypes, knownDomainTypes, config.GetLayer(t))).ToList();
        var valueObjectNodes = categories.ValueObjectTypes.Select(t =>
            _nodes.BuildValueObjectNode(t, knownDomainTypes, config.GetLayer(t))).ToList();
        var domainEventNodes = categories.DomainEventTypes.Select(t =>
            _nodes.BuildDomainEventNode(t, knownDomainTypes, config.GetLayer(t))).ToList();
        var integrationEventNodes = categories.IntegrationEventTypes.Select(t =>
            _nodes.BuildDomainEventNode(t, knownDomainTypes, config.GetLayer(t))).ToList();

        var eventHandlerNodes = categories.EventHandlerTypes.Select(t =>
            _nodes.BuildHandlerNode(t, knownDomainTypes, config.GetLayer(t))).ToList();
        var commandHandlerNodes = categories.CommandHandlerTypes.Select(t =>
            _nodes.BuildHandlerNode(t, knownDomainTypes, config.GetLayer(t))).ToList();
        var queryHandlerNodes = categories.QueryHandlerTypes.Select(t =>
            _nodes.BuildHandlerNode(t, knownDomainTypes, config.GetLayer(t))).ToList();
        var repositoryNodes = categories.RepositoryTypes.Select(t =>
            _nodes.BuildRepositoryNode(t, categories.AggregateTypes, config.GetLayer(t))).ToList();
        var domainServiceNodes = categories.DomainServiceTypes.Select(t =>
            _nodes.BuildDomainServiceNode(t, config.GetLayer(t))).ToList();

        var commandHandlerTargetNodes = _nodes.DiscoverCommandHandlerTargets(
            allTypes,
            knownDomainTypes,
            entityNodes,
            aggregateNodes,
            valueObjectNodes,
            domainEventNodes,
            integrationEventNodes,
            eventHandlerNodes,
            commandHandlerNodes,
            queryHandlerNodes,
            repositoryNodes,
            domainServiceNodes);
        _nodes.MergeRegisteredCommands(
            allTypes,
            knownDomainTypes,
            entityNodes,
            aggregateNodes,
            valueObjectNodes,
            domainEventNodes,
            integrationEventNodes,
            eventHandlerNodes,
            commandHandlerNodes,
            queryHandlerNodes,
            repositoryNodes,
            domainServiceNodes,
            commandHandlerTargetNodes);

        var relationships = GraphRelationshipBuilder.Build(
            categories,
            entityNodes,
            aggregateNodes,
            valueObjectNodes,
            domainEventNodes,
            integrationEventNodes,
            eventHandlerNodes,
            commandHandlerNodes,
            queryHandlerNodes,
            repositoryNodes,
            domainServiceNodes,
            commandHandlerTargetNodes,
            knownDomainTypes);

        foreach (var cmdTarget in commandHandlerTargetNodes)
            knownDomainTypes.Add(cmdTarget.FullName);

        var subTypeNodes = SubTypeDiscovery.Discover(
            entityNodes, aggregateNodes, valueObjectNodes,
            knownDomainTypes, allTypes, relationships);

        return new BoundedContextNode
        {
            Name = config.Name,
            Entities = entityNodes,
            Aggregates = aggregateNodes,
            ValueObjects = valueObjectNodes,
            DomainEvents = domainEventNodes,
            IntegrationEvents = integrationEventNodes,
            EventHandlers = eventHandlerNodes,
            CommandHandlerTargets = commandHandlerTargetNodes,
            CommandHandlers = commandHandlerNodes,
            QueryHandlers = queryHandlerNodes,
            Repositories = repositoryNodes,
            DomainServices = domainServiceNodes,
            SubTypes = subTypeNodes,
            Relationships = relationships
        };
    }
}
