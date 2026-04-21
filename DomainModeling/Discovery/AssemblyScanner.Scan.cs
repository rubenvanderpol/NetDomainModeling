using System.Reflection;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

internal sealed partial class AssemblyScanner
{
    public BoundedContextNode Scan()
    {
        var assemblies = _config.GetAllAssemblies();
        var assemblyOrder = BuildAssemblyOrderIndex(assemblies);

        var allTypes = DeduplicateTypesByFullName(
            assemblies.SelectMany(SafeGetTypes).Where(t => t is { IsAbstract: false, IsInterface: false }),
            assemblyOrder);

        var categories = CategorizeDomainTypes(allTypes);
        var knownDomainTypes = BuildKnownDomainTypeSet(categories);

        var entityNodes = categories.EntityTypes.Select(t =>
            BuildEntityNode(t, categories.DomainEventTypes, knownDomainTypes, _config.GetLayer(t))).ToList();
        var aggregateNodes = categories.AggregateTypes.Select(t =>
            BuildAggregateNode(t, categories.EntityTypes, categories.DomainEventTypes, knownDomainTypes, _config.GetLayer(t))).ToList();
        var valueObjectNodes = categories.ValueObjectTypes.Select(t =>
            BuildValueObjectNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var domainEventNodes = categories.DomainEventTypes.Select(t =>
            BuildDomainEventNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var integrationEventNodes = categories.IntegrationEventTypes.Select(t =>
            BuildDomainEventNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();

        var eventHandlerNodes = categories.EventHandlerTypes.Select(t =>
            BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var commandHandlerNodes = categories.CommandHandlerTypes.Select(t =>
            BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var queryHandlerNodes = categories.QueryHandlerTypes.Select(t =>
            BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var repositoryNodes = categories.RepositoryTypes.Select(t =>
            BuildRepositoryNode(t, categories.AggregateTypes, _config.GetLayer(t))).ToList();
        var domainServiceNodes = categories.DomainServiceTypes.Select(t =>
            BuildDomainServiceNode(t, _config.GetLayer(t))).ToList();

        var commandHandlerTargetNodes = DiscoverCommandHandlerTargets(
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
        MergeRegisteredCommands(
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

        var relationships = BuildRelationships(
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

        var subTypeNodes = DiscoverSubTypes(
            entityNodes, aggregateNodes, valueObjectNodes,
            knownDomainTypes, allTypes, relationships);

        return new BoundedContextNode
        {
            Name = _config.Name,
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

    private static Dictionary<Assembly, int> BuildAssemblyOrderIndex(IReadOnlyList<Assembly> assemblies)
    {
        var assemblyOrder = new Dictionary<Assembly, int>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            if (!assemblyOrder.ContainsKey(assemblies[i]))
                assemblyOrder[assemblies[i]] = i;
        }

        return assemblyOrder;
    }

    private readonly record struct DomainTypeCategories(
        List<Type> EntityTypes,
        List<Type> AggregateTypes,
        List<Type> ValueObjectTypes,
        List<Type> DomainEventTypes,
        List<Type> IntegrationEventTypesAll,
        List<Type> IntegrationEventTypes,
        List<Type> EventHandlerTypes,
        List<Type> CommandHandlerTypes,
        List<Type> QueryHandlerTypes,
        List<Type> RepositoryTypes,
        List<Type> DomainServiceTypes);

    private DomainTypeCategories CategorizeDomainTypes(List<Type> allTypes)
    {
        bool OwnedElsewhere(Type t) => _config.ExternallyOwnedSharedAssemblies.Contains(t.Assembly);

        var entityTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.EntityConvention.Matches(t)).ToList();
        var aggregateTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.AggregateConvention.Matches(t)).ToList();
        var valueObjectTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.ValueObjectConvention.Matches(t)).ToList();
        var domainEventTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.DomainEventConvention.Matches(t)).ToList();
        var integrationEventTypesAll = allTypes.Where(t => _config.IntegrationEventConvention.Matches(t)).ToList();
        var integrationEventTypes = integrationEventTypesAll.Where(t => !OwnedElsewhere(t)).ToList();
        var eventHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.EventHandlerConvention.Matches(t)).ToList();
        var commandHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.CommandHandlerConvention.Matches(t)).ToList();
        var queryHandlerTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.QueryHandlerConvention.Matches(t)).ToList();
        var repositoryTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.RepositoryConvention.Matches(t)).ToList();
        var domainServiceTypes = allTypes.Where(t => !OwnedElsewhere(t) && _config.DomainServiceConvention.Matches(t)).ToList();

        MergeStructuralDomainEvents(allTypes, OwnedElsewhere, domainEventTypes);

        return new DomainTypeCategories(
            entityTypes,
            aggregateTypes,
            valueObjectTypes,
            domainEventTypes,
            integrationEventTypesAll,
            integrationEventTypes,
            eventHandlerTypes,
            commandHandlerTypes,
            queryHandlerTypes,
            repositoryTypes,
            domainServiceTypes);
    }

    private static HashSet<string> BuildKnownDomainTypeSet(DomainTypeCategories categories)
    {
        return new HashSet<string>(
            categories.EntityTypes
                .Concat(categories.AggregateTypes)
                .Concat(categories.ValueObjectTypes)
                .Concat(categories.DomainEventTypes)
                .Concat(categories.IntegrationEventTypesAll)
                .Select(t => t.FullName!)
                .Where(n => n is not null));
    }

    private List<Relationship> BuildRelationships(
        DomainTypeCategories categories,
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<ValueObjectNode> valueObjectNodes,
        List<DomainEventNode> domainEventNodes,
        List<DomainEventNode> integrationEventNodes,
        List<HandlerNode> eventHandlerNodes,
        List<HandlerNode> commandHandlerNodes,
        List<HandlerNode> queryHandlerNodes,
        List<RepositoryNode> repositoryNodes,
        List<DomainServiceNode> domainServiceNodes,
        List<CommandHandlerTargetNode> commandHandlerTargetNodes,
        HashSet<string> knownDomainTypes)
    {
        var relationships = new List<Relationship>();

        AddAggregateContainsRelationships(aggregateNodes, relationships);
        AddEntityAndAggregateEmitsRelationships(entityNodes, aggregateNodes, relationships);

        CrossReferenceEvents(domainEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);

        var handlerPublishedEvents = DetectHandlerPublishedIntegrationEvents(
            categories.EventHandlerTypes,
            categories.IntegrationEventTypesAll);

        CrossReferenceEvents(integrationEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);
        CrossReferencePublishedEvents(integrationEventNodes, handlerPublishedEvents);

        AddHandlerToEventHandlesRelationships(
            domainEventNodes,
            integrationEventNodes,
            eventHandlerNodes,
            commandHandlerNodes,
            queryHandlerNodes,
            relationships);

        AddCommandHandlerToAggregateInvocations(
            categories.CommandHandlerTypes,
            aggregateNodes,
            relationships);

        AddEventHandlerCommandRelationships(
            categories.EventHandlerTypes,
            commandHandlerTargetNodes,
            commandHandlerNodes,
            relationships);

        CrossReferenceCommandHandlerTargets(commandHandlerTargetNodes, commandHandlerNodes);

        AddRepositoryManagesRelationships(repositoryNodes, relationships);
        AddHandlerPublishesIntegrationEvents(handlerPublishedEvents, relationships);

        AddPropertyReferences(entityNodes, knownDomainTypes, relationships);
        AddPropertyReferences(aggregateNodes.Select(a => new EntityNode
        {
            Name = a.Name,
            FullName = a.FullName,
            Properties = a.Properties
        }), knownDomainTypes, relationships);
        AddPropertyReferences(valueObjectNodes.Select(v => new EntityNode
        {
            Name = v.Name,
            FullName = v.FullName,
            Properties = v.Properties
        }), knownDomainTypes, relationships);

        var knownEntityAndAggregateNames = BuildEntityAndAggregateNameMap(
            categories.EntityTypes,
            categories.AggregateTypes);
        AddIdBasedReferences(entityNodes, knownEntityAndAggregateNames, relationships);
        AddIdBasedReferences(aggregateNodes.Select(a => new EntityNode
        {
            Name = a.Name,
            FullName = a.FullName,
            Properties = a.Properties
        }), knownEntityAndAggregateNames, relationships);

        return relationships;
    }

    private static void AddAggregateContainsRelationships(
        List<AggregateNode> aggregateNodes,
        List<Relationship> relationships)
    {
        foreach (var agg in aggregateNodes)
        {
            foreach (var child in agg.ChildEntities)
            {
                relationships.Add(new Relationship
                {
                    SourceType = agg.FullName,
                    TargetType = child,
                    Kind = RelationshipKind.Contains,
                    Label = "contains"
                });
            }
        }
    }

    private static void AddEntityAndAggregateEmitsRelationships(
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<Relationship> relationships)
    {
        foreach (var entity in entityNodes)
        {
            foreach (var emission in entity.EventEmissions)
            {
                relationships.Add(new Relationship
                {
                    SourceType = entity.FullName,
                    TargetType = emission.EventType,
                    Kind = RelationshipKind.Emits,
                    Label = $"emits via {emission.MethodName}()"
                });
            }
        }

        foreach (var agg in aggregateNodes)
        {
            foreach (var emission in agg.EventEmissions)
            {
                relationships.Add(new Relationship
                {
                    SourceType = agg.FullName,
                    TargetType = emission.EventType,
                    Kind = RelationshipKind.Emits,
                    Label = $"emits via {emission.MethodName}()"
                });
            }
        }
    }

    private static Dictionary<string, List<string>> DetectHandlerPublishedIntegrationEvents(
        List<Type> eventHandlerTypes,
        List<Type> integrationEventTypesAll)
    {
        var handlerPublishedEvents = new Dictionary<string, List<string>>();
        foreach (var handlerType in eventHandlerTypes)
        {
            var published = DetectPublishedEvents(handlerType, integrationEventTypesAll);
            if (published.Count > 0)
                handlerPublishedEvents[handlerType.FullName!] = published;
        }

        return handlerPublishedEvents;
    }

    private static void AddHandlerToEventHandlesRelationships(
        List<DomainEventNode> domainEventNodes,
        List<DomainEventNode> integrationEventNodes,
        List<HandlerNode> eventHandlerNodes,
        List<HandlerNode> commandHandlerNodes,
        List<HandlerNode> queryHandlerNodes,
        List<Relationship> relationships)
    {
        var registeredEventFullNames = new HashSet<string>(
            domainEventNodes.Select(e => e.FullName).Concat(integrationEventNodes.Select(e => e.FullName)),
            StringComparer.Ordinal);
        foreach (var handler in eventHandlerNodes.Concat(commandHandlerNodes).Concat(queryHandlerNodes))
        {
            foreach (var handled in handler.Handles)
            {
                var target = ResolveCanonicalEventKey(handled, registeredEventFullNames) ?? handled;
                relationships.Add(new Relationship
                {
                    SourceType = handler.FullName,
                    TargetType = target,
                    Kind = RelationshipKind.Handles,
                    Label = "handles"
                });
            }
        }
    }

    private static void AddCommandHandlerToAggregateInvocations(
        List<Type> commandHandlerTypes,
        List<AggregateNode> aggregateNodes,
        List<Relationship> relationships)
    {
        var aggregateFullNames = new HashSet<string>(aggregateNodes.Select(a => a.FullName), StringComparer.Ordinal);
        foreach (var handlerType in commandHandlerTypes)
        {
            foreach (var (targetFullName, methodName) in DetectInvocationsOnAggregates(handlerType, aggregateFullNames))
            {
                relationships.Add(new Relationship
                {
                    SourceType = handlerType.FullName!,
                    TargetType = targetFullName,
                    Kind = RelationshipKind.References,
                    Label = $"invokes {methodName}()"
                });
            }
        }
    }

    private static void AddEventHandlerCommandRelationships(
        List<Type> eventHandlerTypes,
        List<CommandHandlerTargetNode> commandHandlerTargetNodes,
        List<HandlerNode> commandHandlerNodes,
        List<Relationship> relationships)
    {
        var commandTargetFullNames = new HashSet<string>(
            commandHandlerTargetNodes.Select(t => t.FullName),
            StringComparer.Ordinal);
        var commandHandlerFullNames = new HashSet<string>(
            commandHandlerNodes.Select(h => h.FullName),
            StringComparer.Ordinal);
        var commandTargetMap = commandHandlerTargetNodes.ToDictionary(t => t.FullName, StringComparer.Ordinal);
        foreach (var handlerType in eventHandlerTypes)
        {
            var source = handlerType.FullName!;
            foreach (var cmdFullName in DetectInstantiatedTypes(handlerType, commandTargetFullNames))
            {
                relationships.Add(new Relationship
                {
                    SourceType = source,
                    TargetType = cmdFullName,
                    Kind = RelationshipKind.Handles,
                    Label = "creates command"
                });
                if (commandTargetMap.TryGetValue(cmdFullName, out var targetNode) &&
                    !targetNode.HandledBy.Contains(source))
                    targetNode.HandledBy.Add(source);
            }

            foreach (var (targetFullName, methodName) in DetectInvocationsOnDeclaredTypes(handlerType, commandHandlerFullNames))
            {
                relationships.Add(new Relationship
                {
                    SourceType = source,
                    TargetType = targetFullName,
                    Kind = RelationshipKind.References,
                    Label = $"invokes {methodName}()"
                });
            }
        }
    }

    private static void AddRepositoryManagesRelationships(
        List<RepositoryNode> repositoryNodes,
        List<Relationship> relationships)
    {
        foreach (var repo in repositoryNodes.Where(r => r.ManagesAggregate is not null))
        {
            relationships.Add(new Relationship
            {
                SourceType = repo.FullName,
                TargetType = repo.ManagesAggregate!,
                Kind = RelationshipKind.Manages,
                Label = "manages"
            });
        }
    }

    private static void AddHandlerPublishesIntegrationEvents(
        Dictionary<string, List<string>> handlerPublishedEvents,
        List<Relationship> relationships)
    {
        foreach (var (handlerFullName, publishedEvents) in handlerPublishedEvents)
        {
            foreach (var evt in publishedEvents)
            {
                relationships.Add(new Relationship
                {
                    SourceType = handlerFullName,
                    TargetType = evt,
                    Kind = RelationshipKind.Publishes,
                    Label = "publishes"
                });
            }
        }
    }

    private static Dictionary<string, string> BuildEntityAndAggregateNameMap(
        List<Type> entityTypes,
        List<Type> aggregateTypes)
    {
        var knownEntityAndAggregateNames = new Dictionary<string, string>();
        foreach (var t in entityTypes)
            knownEntityAndAggregateNames[t.Name] = t.FullName!;
        foreach (var t in aggregateTypes)
            knownEntityAndAggregateNames[t.Name] = t.FullName!;
        return knownEntityAndAggregateNames;
    }
}
