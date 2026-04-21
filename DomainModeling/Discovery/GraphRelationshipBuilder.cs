using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Assembles <see cref="Relationship"/> edges from built nodes and IL scan results.
/// </summary>
internal static class GraphRelationshipBuilder
{
    public static List<Relationship> Build(
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

        EventGraphLinker.CrossReferenceEvents(domainEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);

        var handlerPublishedEvents = DetectHandlerPublishedIntegrationEvents(
            categories.EventHandlerTypes,
            categories.IntegrationEventTypesAll);

        EventGraphLinker.CrossReferenceEvents(integrationEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);
        EventGraphLinker.CrossReferencePublishedEvents(integrationEventNodes, handlerPublishedEvents);

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

        DomainGraphBuilder.CrossReferenceCommandHandlerTargets(commandHandlerTargetNodes, commandHandlerNodes);

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
            var published = EventEmissionScanner.DetectPublishedEvents(handlerType, integrationEventTypesAll);
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
                var target = EventGraphLinker.ResolveCanonicalEventKey(handled, registeredEventFullNames) ?? handled;
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
            foreach (var (targetFullName, methodName) in HandlerIlScanner.DetectInvocationsOnAggregates(handlerType, aggregateFullNames))
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
            foreach (var cmdFullName in HandlerIlScanner.DetectInstantiatedTypes(handlerType, commandTargetFullNames))
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

            foreach (var (targetFullName, methodName) in HandlerIlScanner.DetectInvocationsOnDeclaredTypes(handlerType, commandHandlerFullNames))
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

    private static void AddPropertyReferences(IEnumerable<EntityNode> nodes, HashSet<string> knownDomainTypes, List<Relationship> relationships)
    {
        foreach (var node in nodes)
        {
            foreach (var prop in node.Properties.Where(p => p.ReferenceTypeName is not null))
            {
                relationships.Add(new Relationship
                {
                    SourceType = node.FullName,
                    TargetType = prop.ReferenceTypeName!,
                    Kind = prop.IsCollection ? RelationshipKind.HasMany : RelationshipKind.Has,
                    Label = prop.Name
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

    private static void AddIdBasedReferences(
        IEnumerable<EntityNode> nodes,
        Dictionary<string, string> knownEntityAndAggregateNames,
        List<Relationship> relationships)
    {
        var existingRefs = new HashSet<(string source, string target)>(
            relationships.Select(r => (r.SourceType, r.TargetType)));

        foreach (var node in nodes)
        {
            foreach (var prop in node.Properties)
            {
                if (prop.ReferenceTypeName is not null) continue;

                if (prop.Name.Length <= 2 || !prop.Name.EndsWith("Id", StringComparison.Ordinal)) continue;

                var candidateName = prop.Name[..^2];
                if (!knownEntityAndAggregateNames.TryGetValue(candidateName, out var targetFullName)) continue;

                if (targetFullName == node.FullName) continue;

                if (existingRefs.Contains((node.FullName, targetFullName))) continue;

                existingRefs.Add((node.FullName, targetFullName));
                relationships.Add(new Relationship
                {
                    SourceType = node.FullName,
                    TargetType = targetFullName,
                    Kind = RelationshipKind.ReferencesById,
                    Label = prop.Name
                });
            }
        }
    }
}
