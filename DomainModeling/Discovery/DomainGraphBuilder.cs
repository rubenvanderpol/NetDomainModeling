using System.Reflection;
using DomainModeling.Builder;
using DomainModeling.Graph;

namespace DomainModeling.Discovery;

/// <summary>
/// Builds graph nodes from categorized CLR types.
/// </summary>
internal sealed class DomainGraphBuilder(BoundedContextBuilder config)
{
    public EntityNode BuildEntityNode(Type type, List<Type> eventTypes, HashSet<string> knownDomainTypes, string? layer)
    {
        var emissions = EventEmissionScanner.DetectEventEmissions(type, eventTypes);
        return new EntityNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes),
            EmittedEvents = emissions.Select(e => e.EventType).Distinct().ToList(),
            EventEmissions = emissions
        };
    }

    public AggregateNode BuildAggregateNode(Type type, List<Type> entityTypes, List<Type> eventTypes, HashSet<string> knownDomainTypes, string? layer)
    {
        var properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes);
        var emissions = EventEmissionScanner.DetectEventEmissions(type, eventTypes);

        var entityFullNames = new HashSet<string>(entityTypes.Select(e => e.FullName!));
        var childEntities = properties
            .Where(p => p.ReferenceTypeName is not null && entityFullNames.Contains(p.ReferenceTypeName))
            .Select(p => p.ReferenceTypeName!)
            .Distinct()
            .ToList();

        return new AggregateNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Properties = properties,
            Methods = GraphReflectionMapper.GetMethods(type),
            ChildEntities = childEntities,
            EmittedEvents = emissions.Select(e => e.EventType).Distinct().ToList(),
            EventEmissions = emissions
        };
    }

    public ValueObjectNode BuildValueObjectNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        return new ValueObjectNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes)
        };
    }

    public DomainEventNode BuildDomainEventNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        return new DomainEventNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes)
        };
    }

    public HandlerNode BuildHandlerNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        var handledTypes = new HashSet<string>();

        foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
        {
            foreach (var arg in iface.GetGenericArguments())
            {
                if (arg.FullName is not null)
                    handledTypes.Add(arg.FullName);
            }
        }

        if (handledTypes.Count == 0)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                foreach (var param in method.GetParameters())
                {
                    var paramFullName = param.ParameterType.FullName;
                    if (paramFullName is not null)
                        handledTypes.Add(paramFullName);
                }
            }
        }

        return new HandlerNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Handles = handledTypes.ToList()
        };
    }

    public RepositoryNode BuildRepositoryNode(Type type, List<Type> aggregateTypes, string? layer)
    {
        var aggregateNames = new HashSet<string>(aggregateTypes.Select(a => a.FullName!));

        var managedAggregate = type.GetInterfaces()
            .Where(i => i.IsGenericType)
            .SelectMany(i => i.GetGenericArguments())
            .FirstOrDefault(t => aggregateNames.Contains(t.FullName ?? ""));

        return new RepositoryNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            ManagesAggregate = managedAggregate?.FullName
        };
    }

    public DomainServiceNode BuildDomainServiceNode(Type type, string? layer)
    {
        return new DomainServiceNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
        };
    }

    public List<CommandHandlerTargetNode> DiscoverCommandHandlerTargets(
        List<Type> allTypes,
        HashSet<string> knownDomainTypes,
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<ValueObjectNode> valueObjectNodes,
        List<DomainEventNode> domainEventNodes,
        List<DomainEventNode> integrationEventNodes,
        List<HandlerNode> eventHandlerNodes,
        List<HandlerNode> commandHandlerNodes,
        List<HandlerNode> queryHandlerNodes,
        List<RepositoryNode> repositoryNodes,
        List<DomainServiceNode> domainServiceNodes)
    {
        var excluded = CollectPrimaryBuildingBlockFullNames(
            entityNodes, aggregateNodes, valueObjectNodes,
            domainEventNodes, integrationEventNodes,
            eventHandlerNodes, commandHandlerNodes, queryHandlerNodes,
            repositoryNodes, domainServiceNodes);

        var byFullName = allTypes
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var targetTypes = new HashSet<Type>();

        foreach (var handler in commandHandlerNodes)
        {
            foreach (var handledFullName in handler.Handles)
            {
                if (excluded.Contains(handledFullName)) continue;
                if (!byFullName.TryGetValue(handledFullName, out var t)) continue;
                targetTypes.Add(t);
            }
        }

        return targetTypes
            .Where(t => !config.ExternallyOwnedSharedAssemblies.Contains(t.Assembly))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => BuildCommandHandlerTargetNode(t, knownDomainTypes))
            .ToList();
    }

    public void MergeRegisteredCommands(
        List<Type> allTypes,
        HashSet<string> knownDomainTypes,
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
        List<CommandHandlerTargetNode> commandHandlerTargetNodes)
    {
        if (!config.CommandConvention.HasPredicates)
            return;

        var excluded = CollectPrimaryBuildingBlockFullNames(
            entityNodes, aggregateNodes, valueObjectNodes,
            domainEventNodes, integrationEventNodes,
            eventHandlerNodes, commandHandlerNodes, queryHandlerNodes,
            repositoryNodes, domainServiceNodes);

        var existing = new HashSet<string>(commandHandlerTargetNodes.Select(n => n.FullName), StringComparer.Ordinal);

        foreach (var t in allTypes
                     .Where(type => type.FullName is not null
                                    && config.CommandConvention.Matches(type)
                                    && !excluded.Contains(type.FullName)
                                    && !config.ExternallyOwnedSharedAssemblies.Contains(type.Assembly))
                     .OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            var fn = t.FullName!;
            if (existing.Contains(fn)) continue;
            commandHandlerTargetNodes.Add(BuildCommandHandlerTargetNode(t, knownDomainTypes));
            existing.Add(fn);
        }

        commandHandlerTargetNodes.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    public CommandHandlerTargetNode BuildCommandHandlerTargetNode(Type type, HashSet<string> knownDomainTypes) => new()
    {
        Name = TypeDisplayNames.ShortName(type),
        FullName = type.FullName!,
        Layer = config.GetLayer(type),
        Properties = GraphReflectionMapper.GetProperties(type, knownDomainTypes)
    };

    public static void CrossReferenceCommandHandlerTargets(
        List<CommandHandlerTargetNode> targets,
        List<HandlerNode> commandHandlers)
    {
        var map = targets.ToDictionary(c => c.FullName, StringComparer.Ordinal);
        foreach (var handler in commandHandlers)
        {
            foreach (var handled in handler.Handles)
            {
                if (map.TryGetValue(handled, out var node))
                    node.HandledBy.Add(handler.FullName);
            }
        }
    }

    private static HashSet<string> CollectPrimaryBuildingBlockFullNames(
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<ValueObjectNode> valueObjectNodes,
        List<DomainEventNode> domainEventNodes,
        List<DomainEventNode> integrationEventNodes,
        List<HandlerNode> eventHandlerNodes,
        List<HandlerNode> commandHandlerNodes,
        List<HandlerNode> queryHandlerNodes,
        List<RepositoryNode> repositoryNodes,
        List<DomainServiceNode> domainServiceNodes)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in entityNodes) excluded.Add(n.FullName);
        foreach (var n in aggregateNodes) excluded.Add(n.FullName);
        foreach (var n in valueObjectNodes) excluded.Add(n.FullName);
        foreach (var n in domainEventNodes) excluded.Add(n.FullName);
        foreach (var n in integrationEventNodes) excluded.Add(n.FullName);
        foreach (var n in eventHandlerNodes) excluded.Add(n.FullName);
        foreach (var n in commandHandlerNodes) excluded.Add(n.FullName);
        foreach (var n in queryHandlerNodes) excluded.Add(n.FullName);
        foreach (var n in repositoryNodes) excluded.Add(n.FullName);
        foreach (var n in domainServiceNodes) excluded.Add(n.FullName);
        return excluded;
    }
}
