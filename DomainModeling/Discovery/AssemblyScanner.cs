using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DomainModeling;
using DomainModeling.Builder;
using DomainModeling.Graph;
using MethodInfo = DomainModeling.Graph.MethodInfo;
using PropertyInfo = DomainModeling.Graph.PropertyInfo;

namespace DomainModeling.Discovery;

/// <summary>
/// Scans assemblies configured in a <see cref="BoundedContextBuilder"/>
/// and produces a <see cref="BoundedContextNode"/> with all discovered types
/// and their relationships.
/// </summary>
internal sealed class AssemblyScanner
{
    private readonly BoundedContextBuilder _config;
    private readonly RoslynDocumentationIndexer? _documentationIndexer;

    public AssemblyScanner(BoundedContextBuilder config)
    {
        _config = config;
        _documentationIndexer = RoslynDocumentationIndexer.TryCreate(_config.DocumentationSourceRoots);
    }

    public BoundedContextNode Scan()
    {
        var assemblies = _config.GetAllAssemblies();
        var assemblyOrder = new Dictionary<Assembly, int>();
        for (var i = 0; i < assemblies.Count; i++)
        {
            if (!assemblyOrder.ContainsKey(assemblies[i]))
                assemblyOrder[assemblies[i]] = i;
        }

        // Multiple assemblies can expose types with the same FullName (e.g. duplicated sample types
        // in DomainModeling.Example vs DomainModeling.Example.Shared). Type reference Distinct() does not
        // collapse those; pick one Type per FullName following configured assembly order.
        var allTypes = DeduplicateTypesByFullName(
            assemblies.SelectMany(SafeGetTypes).Where(t => t is { IsAbstract: false, IsInterface: false }),
            assemblyOrder);

        // Also collect abstract / interface types for generic-argument resolution
        var allExportedTypes = DeduplicateTypesByFullName(
            assemblies.SelectMany(SafeGetTypes),
            assemblyOrder);

        bool OwnedElsewhere(Type t) => _config.ExternallyOwnedSharedAssemblies.Contains(t.Assembly);

        // Categorize types based on configured conventions (types "owned" by another BC are scanned but not listed here)
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

        // Build a set of all "known domain type" full names for reference detection
        var knownDomainTypes = new HashSet<string>(
            entityTypes.Concat(aggregateTypes).Concat(valueObjectTypes)
                .Concat(domainEventTypes).Concat(integrationEventTypesAll)
                .Select(t => t.FullName!)
                .Where(n => n is not null));

        // Build nodes
        var entityNodes = entityTypes.Select(t => BuildEntityNode(t, domainEventTypes, knownDomainTypes, _config.GetLayer(t))).ToList();
        var aggregateNodes = aggregateTypes.Select(t => BuildAggregateNode(t, entityTypes, domainEventTypes, knownDomainTypes, _config.GetLayer(t))).ToList();
        var valueObjectNodes = valueObjectTypes.Select(t => BuildValueObjectNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var domainEventNodes = domainEventTypes.Select(t => BuildDomainEventNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var integrationEventNodes = integrationEventTypes.Select(t => BuildDomainEventNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();

        var eventHandlerNodes = eventHandlerTypes.Select(t => BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var commandHandlerNodes = commandHandlerTypes.Select(t => BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var queryHandlerNodes = queryHandlerTypes.Select(t => BuildHandlerNode(t, knownDomainTypes, _config.GetLayer(t))).ToList();
        var repositoryNodes = repositoryTypes.Select(t => BuildRepositoryNode(t, aggregateTypes, _config.GetLayer(t))).ToList();
        var domainServiceNodes = domainServiceTypes.Select(t => BuildDomainServiceNode(t, _config.GetLayer(t))).ToList();

        // Synthesize closed-generic domain event nodes from type-level <domain>emits</domain> documentation
        if (_documentationIndexer is not null)
        {
            MergeSyntheticGenericEventNodes(
                entityTypes.Concat(aggregateTypes),
                domainEventNodes,
                knownDomainTypes,
                eventHandlerNodes,
                _documentationIndexer);
        }

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

        // Build relationships
        var relationships = new List<Relationship>();

        // Aggregate → child entity (Contains)
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

        // Entity / Aggregate → emitted events
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

        // Add type-level documented emissions to entity/aggregate nodes and emit relationships
        if (_documentationIndexer is not null)
        {
            MergeTypeDocumentedEmissions(entityTypes, aggregateTypes, entityNodes, aggregateNodes, domainEventNodes, relationships, _documentationIndexer);
        }

        // Wire emittedBy / handledBy on domain event nodes
        CrossReferenceEvents(domainEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);

        // Detect which integration events are published by event handlers (IL scanning)
        var integrationEventFullNames = new HashSet<string>(integrationEventTypesAll.Select(e => e.FullName!));
        var handlerPublishedEvents = new Dictionary<string, List<string>>();
        foreach (var handlerType in eventHandlerTypes)
        {
            var published = DetectPublishedEvents(handlerType, integrationEventTypesAll);
            if (published.Count > 0)
                handlerPublishedEvents[handlerType.FullName!] = published;
        }

        // Wire emittedBy / handledBy on integration event nodes
        CrossReferenceEvents(integrationEventNodes, entityNodes, aggregateNodes, eventHandlerNodes);
        CrossReferencePublishedEvents(integrationEventNodes, handlerPublishedEvents);

        // Handler → handled type (canonical event targets so generic definitions match closed IEventHandler<T> uses)
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

        // CommandHandler → aggregate (instance method calls on aggregates, e.g. order.Place())
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

        // EventHandler → command DTO / command handler (IL scan; GitHub #49)
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

        CrossReferenceCommandHandlerTargets(commandHandlerTargetNodes, commandHandlerNodes);

        // Repository → aggregate
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

        // EventHandler → integration event (Publishes)
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

        // Property-based references between domain types
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

        foreach (var cmdTarget in commandHandlerTargetNodes)
            knownDomainTypes.Add(cmdTarget.FullName);

        // Discover sub-types: custom types referenced by properties that aren't registered domain types
        var subTypeNodes = DiscoverSubTypes(
            entityNodes, aggregateNodes, valueObjectNodes,
            knownDomainTypes, allTypes, relationships);

        // ID-based references (e.g. OrganizationId → Organization)
        // Prefer aggregates over entities when names collide
        var knownEntityAndAggregateNames = new Dictionary<string, string>();
        foreach (var t in entityTypes)
            knownEntityAndAggregateNames[t.Name] = t.FullName!;
        foreach (var t in aggregateTypes)
            knownEntityAndAggregateNames[t.Name] = t.FullName!;
        AddIdBasedReferences(entityNodes, knownEntityAndAggregateNames, relationships);
        AddIdBasedReferences(aggregateNodes.Select(a => new EntityNode
        {
            Name = a.Name,
            FullName = a.FullName,
            Properties = a.Properties
        }), knownEntityAndAggregateNames, relationships);

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

    /// <summary>
    /// Adds domain event types derived from <see cref="BoundedContextBuilder.DomainEventConvention"/> structural rules
    /// (e.g. first parameter of <c>Handle</c> on types matching a nested convention).
    /// </summary>
    private void MergeStructuralDomainEvents(
        List<Type> allTypes,
        Func<Type, bool> ownedElsewhere,
        List<Type> domainEventTypes)
    {
        var rules = _config.DomainEventConvention.StructuralRules;
        if (rules.Count == 0)
            return;

        var existing = new HashSet<string>(domainEventTypes.Select(t => t.FullName!).Where(n => n is not null), StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            foreach (var eventType in rule.EnumerateEventTypes(allTypes))
            {
                if (ownedElsewhere(eventType))
                    continue;
                var fullName = eventType.FullName;
                if (fullName is null || !existing.Add(fullName))
                    continue;
                domainEventTypes.Add(eventType);
            }
        }
    }

    // ─── Node builders ───────────────────────────────────────────────

    private EntityNode BuildEntityNode(Type type, List<Type> eventTypes, HashSet<string> knownDomainTypes, string? layer)
    {
        var emissions = DetectEventEmissions(type, eventTypes, _documentationIndexer);
        return new EntityNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            Properties = GetProperties(type, knownDomainTypes),
            EmittedEvents = emissions.Select(e => e.EventType).Distinct().ToList(),
            EventEmissions = emissions
        };
    }

    private AggregateNode BuildAggregateNode(Type type, List<Type> entityTypes, List<Type> eventTypes, HashSet<string> knownDomainTypes, string? layer)
    {
        var properties = GetProperties(type, knownDomainTypes);
        var emissions = DetectEventEmissions(type, eventTypes, _documentationIndexer);

        // Detect child entities: properties whose type (or collection element type)
        // is a known entity
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
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            Properties = properties,
            Methods = GetMethods(type),
            ChildEntities = childEntities,
            EmittedEvents = emissions.Select(e => e.EventType).Distinct().ToList(),
            EventEmissions = emissions
        };
    }

    private ValueObjectNode BuildValueObjectNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        return new ValueObjectNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            Properties = GetProperties(type, knownDomainTypes)
        };
    }

    private DomainEventNode BuildDomainEventNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        return new DomainEventNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            Properties = GetProperties(type, knownDomainTypes)
        };
    }

    private HandlerNode BuildHandlerNode(Type type, HashSet<string> knownDomainTypes, string? layer)
    {
        var handledTypes = new HashSet<string>();

        // Strategy 1: Extract generic arguments from implemented interfaces
        foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
        {
            foreach (var arg in iface.GetGenericArguments())
            {
                if (arg.FullName is not null)
                    handledTypes.Add(arg.FullName);
            }
        }

        // Strategy 2: Scan public method parameters for known domain types
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
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            Handles = handledTypes.ToList()
        };
    }

    private RepositoryNode BuildRepositoryNode(Type type, List<Type> aggregateTypes, string? layer)
    {
        var aggregateNames = new HashSet<string>(aggregateTypes.Select(a => a.FullName!));

        // Look at generic interface arguments to find which aggregate this repo manages
        var managedAggregate = type.GetInterfaces()
            .Where(i => i.IsGenericType)
            .SelectMany(i => i.GetGenericArguments())
            .FirstOrDefault(t => aggregateNames.Contains(t.FullName ?? ""));

        return new RepositoryNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Description = _documentationIndexer?.TryGetDomainSummary(type),
            ManagesAggregate = managedAggregate?.FullName
        };
    }

    private DomainServiceNode BuildDomainServiceNode(Type type, string? layer)
    {
        return new DomainServiceNode
        {
            Name = TypeDisplayNames.ShortName(type),
            FullName = type.FullName!,
            Layer = layer,
            Description = _documentationIndexer?.TryGetDomainSummary(type)
        };
    }

    /// <summary>
    /// Surfaces types that command handlers list in <see cref="HandlerNode.Handles"/> when those types
    /// are not already modeled as another building block, so "Handles" edges have diagram endpoints (GitHub #10).
    /// </summary>
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

    private List<CommandHandlerTargetNode> DiscoverCommandHandlerTargets(
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
            .Where(t => !_config.ExternallyOwnedSharedAssemblies.Contains(t.Assembly))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => BuildCommandHandlerTargetNode(t, knownDomainTypes))
            .ToList();
    }

    /// <summary>
    /// Adds command DTO types matched by <see cref="BoundedContextBuilder.Commands"/> so they appear
    /// in the graph even when no handler references them yet.
    /// </summary>
    private void MergeRegisteredCommands(
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
        if (!_config.CommandConvention.HasPredicates)
            return;

        var excluded = CollectPrimaryBuildingBlockFullNames(
            entityNodes, aggregateNodes, valueObjectNodes,
            domainEventNodes, integrationEventNodes,
            eventHandlerNodes, commandHandlerNodes, queryHandlerNodes,
            repositoryNodes, domainServiceNodes);

        var existing = new HashSet<string>(commandHandlerTargetNodes.Select(n => n.FullName), StringComparer.Ordinal);

        foreach (var t in allTypes
                     .Where(type => type.FullName is not null
                                    && _config.CommandConvention.Matches(type)
                                    && !excluded.Contains(type.FullName)
                                    && !_config.ExternallyOwnedSharedAssemblies.Contains(type.Assembly))
                     .OrderBy(type => type.Name, StringComparer.Ordinal))
        {
            var fn = t.FullName!;
            if (existing.Contains(fn)) continue;
            commandHandlerTargetNodes.Add(BuildCommandHandlerTargetNode(t, knownDomainTypes));
            existing.Add(fn);
        }

        commandHandlerTargetNodes.Sort(static (a, b) => string.CompareOrdinal(a.Name, b.Name));
    }

    private CommandHandlerTargetNode BuildCommandHandlerTargetNode(Type type, HashSet<string> knownDomainTypes) => new()
    {
        Name = TypeDisplayNames.ShortName(type),
        FullName = type.FullName!,
        Layer = _config.GetLayer(type),
        Description = _documentationIndexer?.TryGetDomainSummary(type),
        Properties = GetProperties(type, knownDomainTypes)
    };

    private static void CrossReferenceCommandHandlerTargets(
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

    // ─── Helpers ─────────────────────────────────────────────────────

    private static List<PropertyInfo> GetProperties(Type type, HashSet<string> knownDomainTypes)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == type || p.DeclaringType?.Assembly == type.Assembly)
            .Select(p =>
            {
                var (propertyTypeName, isCollection, elementType) = AnalyzePropertyType(p.PropertyType);
                var referenceType = elementType ?? p.PropertyType;
                var refFullName = referenceType.FullName;
                var isKnownDomain = refFullName is not null && knownDomainTypes.Contains(refFullName);
                var isCustomType = !isKnownDomain && refFullName is not null && IsCustomType(referenceType);

                return new PropertyInfo
                {
                    Name = p.Name,
                    TypeName = propertyTypeName,
                    IsCollection = isCollection,
                    ReferenceTypeName = isKnownDomain || isCustomType ? refFullName : null
                };
            })
            .ToList();
    }

    /// <summary>
    /// Determines whether a type is a custom (non-primitive) type that should be
    /// treated as a sub-type in the domain graph.
    /// </summary>
    private static bool IsCustomType(Type type)
    {
        // Unwrap nullable
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying.IsPrimitive) return false;
        if (underlying.IsEnum) return false;
        if (underlying == typeof(string)) return false;
        if (underlying == typeof(decimal)) return false;
        if (underlying == typeof(Guid)) return false;
        if (underlying == typeof(DateTime)) return false;
        if (underlying == typeof(DateTimeOffset)) return false;
        if (underlying == typeof(DateOnly)) return false;
        if (underlying == typeof(TimeOnly)) return false;
        if (underlying == typeof(TimeSpan)) return false;
        if (underlying == typeof(Uri)) return false;
        if (underlying == typeof(byte[])) return false;
        if (underlying == typeof(object)) return false;

        // Must be a class or struct with a namespace (not compiler-generated)
        if (underlying.FullName is null) return false;
        if (underlying.Namespace?.StartsWith("System") == true) return false;
        if (underlying.Namespace?.StartsWith("Microsoft") == true) return false;

        return true;
    }

    private static List<MethodInfo> GetMethods(Type type)
    {
        // Exclude property accessors, special runtime methods, and methods inherited from System.Object
        var objectMethods = new HashSet<string>(
            typeof(object).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name));

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && !objectMethods.Contains(m.Name))
            .Select(m => new MethodInfo
            {
                Name = m.Name,
                ReturnTypeName = TypeDisplayNames.FormatTypeReference(m.ReturnType),
                Parameters = m.GetParameters()
                    .Select(p => new MethodParameterInfo
                    {
                        Name = p.Name ?? "arg",
                        TypeName = TypeDisplayNames.FormatTypeReference(p.ParameterType),
                    })
                    .ToList()
            })
            .ToList();
    }

    private static (string TypeName, bool IsCollection, Type? ElementType) AnalyzePropertyType(Type type)
    {
        // Check for arrays
        if (type.IsArray)
        {
            var elem = type.GetElementType()!;
            return ($"{TypeDisplayNames.FormatTypeReference(elem)}[]", true, elem);
        }

        // Check for generic collections (IEnumerable<T>, ICollection<T>, List<T>, etc.)
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (args.Length == 1 && IsCollectionType(genericDef))
            {
                return ($"ICollection<{TypeDisplayNames.FormatTypeReference(args[0])}>", true, args[0]);
            }

            // Generic but not a collection (e.g. Nullable<T>)
            var argNames = string.Join(", ", args.Select(TypeDisplayNames.FormatTypeReference));
            var defName = StripGenericArity(type.Name);
            return ($"{defName}<{argNames}>", false, null);
        }

        return (TypeDisplayNames.FormatTypeReference(type), false, null);
    }

    private static bool IsCollectionType(Type genericDef)
    {
        return genericDef == typeof(IEnumerable<>)
            || genericDef == typeof(ICollection<>)
            || genericDef == typeof(IList<>)
            || genericDef == typeof(List<>)
            || genericDef == typeof(IReadOnlyCollection<>)
            || genericDef == typeof(IReadOnlyList<>)
            || genericDef == typeof(HashSet<>)
            || genericDef == typeof(ISet<>);
    }

    private static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }

    /// <summary>
    /// Detects emitted domain events and tracks the method that emitted each one.
    /// </summary>
    private static List<EventEmissionInfo> DetectEventEmissions(
        Type type,
        List<Type> eventTypes,
        RoslynDocumentationIndexer? documentationIndexer)
    {
        var eventFullNames = new HashSet<string>(eventTypes.Select(e => e.FullName!));
        var emittedByMethod = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        ScanTypeMethods(type, eventFullNames, emittedByMethod);

        // Also scan compiler-generated nested types (async state machines, lambda display classes, etc.)
        foreach (var nested in type.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length > 0)
            {
                ScanTypeMethods(
                    nested,
                    eventFullNames,
                    emittedByMethod,
                    fallbackMethodName: TryExtractCompilerGeneratedMethodName(nested.Name));
            }
        }

        if (documentationIndexer is not null)
            MergeDocumentedMethodEmissions(type, eventFullNames, emittedByMethod, documentationIndexer);

        return emittedByMethod
            .SelectMany(kvp => kvp.Value.Select(method => new EventEmissionInfo
            {
                EventType = kvp.Key,
                MethodName = method
            }))
            .OrderBy(e => e.EventType)
            .ThenBy(e => e.MethodName)
            .ToList();
    }

    private static void MergeDocumentedMethodEmissions(
        Type declaringType,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        RoslynDocumentationIndexer documentationIndexer)
    {
        if (declaringType.FullName is null)
            return;

        var allMethods = declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(declaringType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
        {
            var methodName = NormalizeMethodName(method, fallbackMethodName: null);
            foreach (var documentedEvent in documentationIndexer.TryGetDocumentedEmissions(declaringType, methodName))
            {
                var key = ResolveCanonicalEventKey(documentedEvent, eventFullNames);
                if (key is not null)
                    AddEventEmission(emittedByMethod, key, methodName);
            }
        }
    }

    /// <summary>
    /// Scans all methods and constructors of a type for event-related IL patterns.
    /// </summary>
    private static void ScanTypeMethods(
        Type type,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string? fallbackMethodName = null)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
        {
            var sourceMethodName = NormalizeMethodName(method, fallbackMethodName);
            ScanMethodBodyForEvents(method, type.Module, eventFullNames, emittedByMethod, sourceMethodName);
        }
    }

    /// <summary>
    /// Scans a method's IL body for <c>newobj</c> (0x73) instructions whose
    /// target constructor belongs to a known event type, and checks local variable types.
    /// </summary>
    private static void ScanMethodBodyForEvents(
        MethodBase method,
        Module module,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        System.Reflection.MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        // Check local variable types
        foreach (var local in body.LocalVariables)
            CheckTypeForEvents(local.LocalType, eventFullNames, emittedByMethod, sourceMethodName);

        // Scan IL for newobj (0x73) and call/callvirt that might reference event types
        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte newobj = 0x73;
        const byte call = 0x28;
        const byte callvirt = 0x6F;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] is not (newobj or call or callvirt))
                continue;

            if (i + 4 >= il.Length)
                continue;

            // Read the 4-byte metadata token (little-endian)
            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved?.DeclaringType?.FullName is { } fullName)
                {
                    var key = ResolveCanonicalEventKey(fullName, eventFullNames);
                    if (key is not null)
                        AddEventEmission(emittedByMethod, key, sourceMethodName);
                }
            }
            catch
            {
                // Token might not be a method token — skip
            }

            i += 4; // skip the 4-byte operand
        }
    }

    private static void CheckTypeForEvents(
        Type type,
        HashSet<string> eventFullNames,
        Dictionary<string, HashSet<string>> emittedByMethod,
        string sourceMethodName)
    {
        if (type.FullName is not null)
        {
            var key = ResolveCanonicalEventKey(type.FullName, eventFullNames);
            if (key is not null)
            {
                AddEventEmission(emittedByMethod, key, sourceMethodName);
                return;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var arg in type.GetGenericArguments())
                CheckTypeForEvents(arg, eventFullNames, emittedByMethod, sourceMethodName);
        }
    }

    private static void AddEventEmission(
        Dictionary<string, HashSet<string>> emittedByMethod,
        string eventType,
        string methodName)
    {
        if (!emittedByMethod.TryGetValue(eventType, out var methods))
        {
            methods = new HashSet<string>(StringComparer.Ordinal);
            emittedByMethod[eventType] = methods;
        }

        methods.Add(methodName);
    }

    private static string NormalizeMethodName(MethodBase method, string? fallbackMethodName)
    {
        var methodName = method.Name;
        if (methodName == ".ctor")
            return "ctor";
        if (methodName == ".cctor")
            return "cctor";
        if (methodName == "MoveNext" && !string.IsNullOrWhiteSpace(fallbackMethodName))
            return fallbackMethodName!;
        return methodName;
    }

    private static string? TryExtractCompilerGeneratedMethodName(string generatedTypeName)
    {
        var open = generatedTypeName.IndexOf('<');
        var close = generatedTypeName.IndexOf('>');
        if (open < 0 || close <= open + 1)
            return null;

        var methodName = generatedTypeName[(open + 1)..close];
        return string.IsNullOrWhiteSpace(methodName) ? null : methodName;
    }

    private static void CrossReferenceEvents(
        List<DomainEventNode> eventNodes,
        List<EntityNode> entities,
        List<AggregateNode> aggregates,
        List<HandlerNode> handlers)
    {
        var eventMap = eventNodes.ToDictionary(e => e.FullName);

        foreach (var entity in entities)
        {
            foreach (var evtName in entity.EmittedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(entity.FullName);
            }
        }

        foreach (var agg in aggregates)
        {
            foreach (var evtName in agg.EmittedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(agg.FullName);
            }
        }

        foreach (var handler in handlers)
        {
            foreach (var handled in handler.Handles)
            {
                if (TryResolveEventNode(eventMap, handled, out var evtNode))
                    evtNode.HandledBy.Add(handler.FullName);
            }
        }
    }

    /// <summary>
    /// For constructed generic CLR names, the open generic definition is the prefix before type arguments (<c>[[...]]</c>).
    /// Handlers and IL often use the constructed form while the graph node is the generic type definition.
    /// Also matches CLR reflection names against canonical short-form constructed generics (e.g.
    /// <c>Ns.Event`1[[Ns.User, Assembly, ...]]</c> matches <c>Ns.Event`1[[Ns.User]]</c>).
    /// Prefers a constructed generic match over the open generic definition.
    /// </summary>
    private static string? ResolveCanonicalEventKey(string typeFullName, HashSet<string> registeredEventFullNames)
    {
        if (registeredEventFullNames.Contains(typeFullName))
            return typeFullName;

        var bracket = typeFullName.IndexOf("[[", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            var canonical = ToCanonicalClosedGenericFullName(typeFullName);
            if (canonical is not null && registeredEventFullNames.Contains(canonical))
                return canonical;

            var def = typeFullName[..bracket];
            if (registeredEventFullNames.Contains(def))
                return def;
        }

        return null;
    }

    /// <summary>
    /// Converts a CLR reflection constructed generic full name (with assembly-qualified type arguments)
    /// to the short canonical form: <c>Ns.Event`1[[Ns.User]]</c>.
    /// Returns <c>null</c> if the name is not a constructed generic.
    /// </summary>
    private static string? ToCanonicalClosedGenericFullName(string clrFullName)
    {
        var outerStart = clrFullName.IndexOf("[[", StringComparison.Ordinal);
        if (outerStart < 0)
            return null;

        var prefix = clrFullName[..outerStart];
        var sb = new System.Text.StringBuilder(prefix);
        sb.Append('[');

        var i = outerStart + 1;
        var first = true;
        while (i < clrFullName.Length)
        {
            if (clrFullName[i] == '[')
            {
                if (!first) sb.Append(',');
                first = false;
                i++;
                var commaPos = clrFullName.IndexOf(',', i);
                var closeBracket = clrFullName.IndexOf(']', i);
                string argFullName;
                if (commaPos >= 0 && commaPos < closeBracket)
                    argFullName = clrFullName[i..commaPos].Trim();
                else if (closeBracket >= 0)
                    argFullName = clrFullName[i..closeBracket].Trim();
                else
                    return null;

                sb.Append('[');
                sb.Append(argFullName);
                sb.Append(']');

                var depth = 1;
                while (i < clrFullName.Length && depth > 0)
                {
                    if (clrFullName[i] == '[') depth++;
                    else if (clrFullName[i] == ']') depth--;
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static bool TryResolveEventNode(
        Dictionary<string, DomainEventNode> eventMap,
        string typeFullName,
        [NotNullWhen(true)] out DomainEventNode? node)
    {
        if (eventMap.TryGetValue(typeFullName, out node))
            return true;

        var bracket = typeFullName.IndexOf("[[", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            var canonical = ToCanonicalClosedGenericFullName(typeFullName);
            if (canonical is not null && eventMap.TryGetValue(canonical, out node))
                return true;

            if (eventMap.TryGetValue(typeFullName[..bracket], out node))
                return true;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// Wires up EmittedBy on integration event nodes for handlers that publish them.
    /// </summary>
    private static void CrossReferencePublishedEvents(
        List<DomainEventNode> integrationEventNodes,
        Dictionary<string, List<string>> handlerPublishedEvents)
    {
        var eventMap = integrationEventNodes.ToDictionary(e => e.FullName);

        foreach (var (handlerFullName, publishedEvents) in handlerPublishedEvents)
        {
            foreach (var evtName in publishedEvents)
            {
                if (TryResolveEventNode(eventMap, evtName, out var evtNode))
                    evtNode.EmittedBy.Add(handlerFullName);
            }
        }
    }

    /// <summary>
    /// Detect which integration event types a handler can publish.
    /// Uses IL scanning similar to <see cref="DetectEventEmissions(Type, List{Type}, RoslynDocumentationIndexer?)"/>.
    /// </summary>
    private static List<string> DetectPublishedEvents(Type type, List<Type> integrationEventTypes)
    {
        return DetectEventEmissions(type, integrationEventTypes, documentationIndexer: null)
            .Select(e => e.EventType)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Finds instance method calls whose declaring type is a discovered aggregate (e.g. <c>order.Place()</c>
    /// from a command handler), including inside async state machines.
    /// </summary>
    private static List<(string TargetFullName, string MethodName)> DetectInvocationsOnAggregates(
        Type handlerType,
        HashSet<string> aggregateFullNames)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<(string Target, string Method)>();

        void OnCall(string targetFullName, string methodName)
        {
            if (seen.Add((targetFullName, methodName)))
                results.Add((targetFullName, methodName));
        }

        ScanTypeForInstanceCallsOnTypes(handlerType, aggregateFullNames, OnCall);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForInstanceCallsOnTypes(nested, aggregateFullNames, OnCall);
        }

        return results;
    }

    /// <summary>
    /// Finds instance method calls whose declaring type is a known command handler type
    /// (e.g. mediator dispatch to another handler), including inside async state machines.
    /// </summary>
    private static List<(string TargetFullName, string MethodName)> DetectInvocationsOnDeclaredTypes(
        Type handlerType,
        HashSet<string> declaringTypeFullNames)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<(string Target, string Method)>();

        void OnCall(string targetFullName, string methodName)
        {
            if (seen.Add((targetFullName, methodName)))
                results.Add((targetFullName, methodName));
        }

        ScanTypeForInstanceCallsOnTypes(handlerType, declaringTypeFullNames, OnCall);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForInstanceCallsOnTypes(nested, declaringTypeFullNames, OnCall);
        }

        return results;
    }

    /// <summary>
    /// Finds <c>newobj</c> instructions that construct types in <paramref name="typeFullNames"/>
    /// (e.g. command DTOs created in an event handler).
    /// </summary>
    private static List<string> DetectInstantiatedTypes(Type handlerType, HashSet<string> typeFullNames)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void OnCtor(string typeFullName)
        {
            if (seen.Add(typeFullName))
                results.Add(typeFullName);
        }

        ScanTypeForNewObjOfTypes(handlerType, typeFullNames, OnCtor);

        foreach (var nested in handlerType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (nested.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length == 0)
                continue;

            ScanTypeForNewObjOfTypes(nested, typeFullNames, OnCtor);
        }

        return results;
    }

    private static void ScanTypeForInstanceCallsOnTypes(
        Type type,
        HashSet<string> declaringTypeFullNames,
        Action<string, string> onCall)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
            ScanMethodBodyForInstanceCallsOnTypes(method, type.Module, declaringTypeFullNames, onCall);
    }

    private static void ScanMethodBodyForInstanceCallsOnTypes(
        MethodBase method,
        Module module,
        HashSet<string> declaringTypeFullNames,
        Action<string, string> onCall)
    {
        System.Reflection.MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte call = 0x28;
        const byte callvirt = 0x6F;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] is not (call or callvirt))
                continue;

            if (i + 4 >= il.Length)
                continue;

            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved is not System.Reflection.MethodInfo mi)
                    continue;
                if (mi.IsStatic)
                    continue;
                if (string.Equals(mi.Name, ".ctor", StringComparison.Ordinal))
                    continue;
                if (mi.IsSpecialName)
                    continue;

                var decl = mi.DeclaringType;
                if (decl?.FullName is not { } declFullName)
                    continue;
                if (!declaringTypeFullNames.Contains(declFullName))
                    continue;

                onCall(declFullName, mi.Name);
            }
            catch
            {
                // Token might not be a method token
            }

            i += 4;
        }
    }

    private static void ScanTypeForNewObjOfTypes(Type type, HashSet<string> typeFullNames, Action<string> onCtor)
    {
        var allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Cast<MethodBase>()
            .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));

        foreach (var method in allMethods)
            ScanMethodBodyForNewObjOfTypes(method, type.Module, typeFullNames, onCtor);
    }

    private static void ScanMethodBodyForNewObjOfTypes(
        MethodBase method,
        Module module,
        HashSet<string> typeFullNames,
        Action<string> onCtor)
    {
        System.Reflection.MethodBody? body;
        try { body = method.GetMethodBody(); }
        catch { return; }

        if (body is null)
            return;

        var il = body.GetILAsByteArray();
        if (il is null)
            return;

        const byte newobj = 0x73;

        for (var i = 0; i < il.Length; i++)
        {
            if (il[i] != newobj)
                continue;

            if (i + 4 >= il.Length)
                continue;

            var token = il[i + 1]
                      | (il[i + 2] << 8)
                      | (il[i + 3] << 16)
                      | (il[i + 4] << 24);

            try
            {
                var resolved = module.ResolveMethod(token);
                if (resolved is not ConstructorInfo ctor)
                    continue;
                var decl = ctor.DeclaringType;
                if (decl?.FullName is not { } declFullName)
                    continue;
                if (!typeFullNames.Contains(declFullName))
                    continue;

                onCtor(declFullName);
            }
            catch
            {
                // Token might not be a method token
            }

            i += 4;
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

    /// <summary>
    /// Discovers sub-types: custom types that are referenced by properties on entities,
    /// aggregates, or value objects but are not themselves registered domain types.
    /// Also processes sub-type properties recursively.
    /// </summary>
    private List<SubTypeNode> DiscoverSubTypes(
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<ValueObjectNode> valueObjectNodes,
        HashSet<string> knownDomainTypes,
        List<Type> allTypes,
        List<Relationship> relationships)
    {
        var allPropertySources = entityNodes.SelectMany(e => e.Properties)
            .Concat(aggregateNodes.SelectMany(a => a.Properties))
            .Concat(valueObjectNodes.SelectMany(v => v.Properties));

        var subTypeFullNames = allPropertySources
            .Where(p => p.ReferenceTypeName is not null && !knownDomainTypes.Contains(p.ReferenceTypeName))
            .Select(p => p.ReferenceTypeName!)
            .ToHashSet();

        var typeMap = allTypes
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!)
            .ToDictionary(g => g.Key, g => g.First());
        var subTypeNodes = new List<SubTypeNode>();
        var processed = new HashSet<string>();
        var queue = new Queue<string>(subTypeFullNames);

        while (queue.Count > 0)
        {
            var fullName = queue.Dequeue();
            if (!processed.Add(fullName)) continue;
            if (knownDomainTypes.Contains(fullName)) continue;
            if (!typeMap.TryGetValue(fullName, out var type)) continue;

            var properties = GetProperties(type, knownDomainTypes);
            subTypeNodes.Add(new SubTypeNode
            {
                Name = TypeDisplayNames.ShortName(type),
                FullName = fullName,
                Description = _documentationIndexer?.TryGetDomainSummary(type),
                IsCustom = false,
                Properties = properties
            });

            // Discover nested sub-types from this sub-type's properties
            foreach (var prop in properties.Where(p => p.ReferenceTypeName is not null))
            {
                if (!knownDomainTypes.Contains(prop.ReferenceTypeName!) && !processed.Contains(prop.ReferenceTypeName!))
                {
                    queue.Enqueue(prop.ReferenceTypeName!);
                }

                relationships.Add(new Relationship
                {
                    SourceType = fullName,
                    TargetType = prop.ReferenceTypeName!,
                    Kind = prop.IsCollection ? RelationshipKind.HasMany : RelationshipKind.Has,
                    Label = prop.Name
                });
            }
        }

        return subTypeNodes;
    }

    private static void AddIdBasedReferences(
        IEnumerable<EntityNode> nodes,
        Dictionary<string, string> knownEntityAndAggregateNames,
        List<Relationship> relationships)
    {
        // Track existing relationship targets per source to avoid duplicates
        var existingRefs = new HashSet<(string source, string target)>(
            relationships.Select(r => (r.SourceType, r.TargetType)));

        foreach (var node in nodes)
        {
            foreach (var prop in node.Properties)
            {
                // Skip properties that already have an object reference
                if (prop.ReferenceTypeName is not null) continue;

                // Check if property name ends with "Id" and the prefix matches a known entity/aggregate
                if (prop.Name.Length <= 2 || !prop.Name.EndsWith("Id", StringComparison.Ordinal)) continue;

                var candidateName = prop.Name[..^2]; // Strip "Id" suffix
                if (!knownEntityAndAggregateNames.TryGetValue(candidateName, out var targetFullName)) continue;

                // Don't create self-references
                if (targetFullName == node.FullName) continue;

                // Don't duplicate if a relationship already exists
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

    /// <summary>
    /// Wires up type-level documented emissions (from <c>&lt;domain&gt;emits&lt;/domain&gt;</c> on type doc comments)
    /// to entity/aggregate <see cref="EntityNode.EmittedEvents"/>, <see cref="EntityNode.EventEmissions"/>,
    /// and <see cref="Relationship"/> lists.
    /// </summary>
    private static void MergeTypeDocumentedEmissions(
        List<Type> entityTypes,
        List<Type> aggregateTypes,
        List<EntityNode> entityNodes,
        List<AggregateNode> aggregateNodes,
        List<DomainEventNode> domainEventNodes,
        List<Relationship> relationships,
        RoslynDocumentationIndexer documentationIndexer)
    {
        var eventFullNames = new HashSet<string>(domainEventNodes.Select(e => e.FullName), StringComparer.Ordinal);
        var addedRelationships = new HashSet<(string Source, string Target)>();

        var allTypesByFullName = entityTypes.Concat(aggregateTypes)
            .Where(t => t.FullName is not null)
            .GroupBy(t => t.FullName!)
            .ToDictionary(g => g.Key, g => g.First());

        void ProcessEmitter(string emitterFullName, List<string> emittedEvents, List<EventEmissionInfo> eventEmissions)
        {
            if (!allTypesByFullName.TryGetValue(emitterFullName, out var type))
                return;
            var emissions = documentationIndexer.TryGetTypeDocumentedEmissions(type);
            foreach (var (canonicalFullName, _) in emissions)
            {
                if (!eventFullNames.Contains(canonicalFullName))
                    continue;
                if (!emittedEvents.Contains(canonicalFullName))
                    emittedEvents.Add(canonicalFullName);
                if (!eventEmissions.Any(e => e.EventType == canonicalFullName))
                    eventEmissions.Add(new EventEmissionInfo { EventType = canonicalFullName, MethodName = "(documented)" });
                if (addedRelationships.Add((emitterFullName, canonicalFullName)))
                {
                    relationships.Add(new Relationship
                    {
                        SourceType = emitterFullName,
                        TargetType = canonicalFullName,
                        Kind = RelationshipKind.Emits,
                        Label = "emits (documented)"
                    });
                }
            }
        }

        foreach (var entity in entityNodes)
            ProcessEmitter(entity.FullName, entity.EmittedEvents, entity.EventEmissions);
        foreach (var agg in aggregateNodes)
            ProcessEmitter(agg.FullName, agg.EmittedEvents, agg.EventEmissions);
    }

    /// <summary>
    /// Creates synthetic <see cref="DomainEventNode"/> entries for closed-generic domain events
    /// referenced via <c>&lt;domain&gt;emits&lt;/domain&gt;</c> type-level documentation, and wires up
    /// handler <see cref="HandlerNode.Handles"/> entries to prefer the closed-generic node.
    /// </summary>
    private static void MergeSyntheticGenericEventNodes(
        IEnumerable<Type> emittingTypes,
        List<DomainEventNode> domainEventNodes,
        HashSet<string> knownDomainTypes,
        List<HandlerNode> eventHandlerNodes,
        RoslynDocumentationIndexer documentationIndexer)
    {
        var existingEventFullNames = new HashSet<string>(domainEventNodes.Select(e => e.FullName), StringComparer.Ordinal);

        foreach (var type in emittingTypes)
        {
            var emissions = documentationIndexer.TryGetTypeDocumentedEmissions(type);
            if (emissions.Count == 0)
                continue;

            foreach (var (canonicalFullName, displayName) in emissions)
            {
                if (existingEventFullNames.Contains(canonicalFullName))
                    continue;

                domainEventNodes.Add(new DomainEventNode
                {
                    Name = displayName,
                    FullName = canonicalFullName,
                });
                existingEventFullNames.Add(canonicalFullName);
                knownDomainTypes.Add(canonicalFullName);
            }
        }

        // Remap handler Handles entries: if a handler handles a closed generic that now has
        // a dedicated event node, use the canonical name instead of the reflection full name.
        var eventFullNames = new HashSet<string>(existingEventFullNames, StringComparer.Ordinal);
        foreach (var handler in eventHandlerNodes)
        {
            for (var i = 0; i < handler.Handles.Count; i++)
            {
                var resolved = ResolveCanonicalEventKey(handler.Handles[i], eventFullNames);
                if (resolved is not null)
                    handler.Handles[i] = resolved;
            }
        }
    }

    /// <summary>
    /// When the same CLR full name appears in more than one scanned assembly, keep a single
    /// <see cref="Type"/> — prefer the assembly that appears earlier in <paramref name="assemblyOrder"/>.
    /// </summary>
    private static List<Type> DeduplicateTypesByFullName(IEnumerable<Type> types, Dictionary<Assembly, int> assemblyOrder)
    {
        var list = types as IList<Type> ?? types.ToList();
        var withFullName = list.Where(t => t.FullName is not null).ToList();
        var withoutFullName = list.Where(t => t.FullName is null).Distinct().ToList();

        static int Order(Dictionary<Assembly, int> order, Assembly a) =>
            order.TryGetValue(a, out var i) ? i : int.MaxValue;

        var deduped = withFullName
            .GroupBy(t => t.FullName!, StringComparer.Ordinal)
            .Select(g => g.OrderBy(t => Order(assemblyOrder, t.Assembly)).First())
            .ToList();

        return deduped.Concat(withoutFullName).ToList();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
