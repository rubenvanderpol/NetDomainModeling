using System.Text.Json.Serialization;
using DomainModeling.Graph;

namespace DomainModeling.AspNetCore;

/// <summary>
/// A feature-editor-friendly representation of a domain graph.
/// Uses serialization-safe types that do not shadow <c>System.Reflection</c> names.
/// Passed to feature export builders registered via
/// <see cref="DomainModelOptions.AddFeatureExport"/>.
/// </summary>
public sealed class FeatureGraph
{
    public List<FeatureBoundedContext> BoundedContexts { get; init; } = [];

    internal static FeatureGraph FromDomainGraph(DomainGraph graph) => new()
    {
        BoundedContexts = graph.BoundedContexts
            .Select(FeatureBoundedContext.FromBoundedContextNode)
            .ToList(),
    };
}

/// <summary>
/// A bounded context within a <see cref="FeatureGraph"/>.
/// </summary>
public sealed class FeatureBoundedContext
{
    public string Name { get; init; } = "";
    public List<FeatureEntity> Entities { get; init; } = [];
    public List<FeatureAggregate> Aggregates { get; init; } = [];
    public List<FeatureValueObject> ValueObjects { get; init; } = [];
    public List<FeatureDomainEvent> DomainEvents { get; init; } = [];
    public List<FeatureDomainEvent> IntegrationEvents { get; init; } = [];
    public List<FeatureHandler> EventHandlers { get; init; } = [];
    public List<FeatureHandler> CommandHandlers { get; init; } = [];
    public List<FeatureHandler> QueryHandlers { get; init; } = [];
    public List<FeatureRepository> Repositories { get; init; } = [];
    public List<FeatureDomainService> DomainServices { get; init; } = [];
    public List<FeatureSubType> SubTypes { get; init; } = [];
    public List<FeatureRelationship> Relationships { get; init; } = [];

    internal static FeatureBoundedContext FromBoundedContextNode(BoundedContextNode n) => new()
    {
        Name = n.Name,
        Entities = n.Entities.Select(FeatureEntity.FromEntityNode).ToList(),
        Aggregates = n.Aggregates.Select(FeatureAggregate.FromAggregateNode).ToList(),
        ValueObjects = n.ValueObjects.Select(FeatureValueObject.FromValueObjectNode).ToList(),
        DomainEvents = n.DomainEvents.Select(FeatureDomainEvent.FromDomainEventNode).ToList(),
        IntegrationEvents = n.IntegrationEvents.Select(FeatureDomainEvent.FromDomainEventNode).ToList(),
        EventHandlers = n.EventHandlers.Select(FeatureHandler.FromHandlerNode).ToList(),
        CommandHandlers = n.CommandHandlers.Select(FeatureHandler.FromHandlerNode).ToList(),
        QueryHandlers = n.QueryHandlers.Select(FeatureHandler.FromHandlerNode).ToList(),
        Repositories = n.Repositories.Select(FeatureRepository.FromRepositoryNode).ToList(),
        DomainServices = n.DomainServices.Select(FeatureDomainService.FromDomainServiceNode).ToList(),
        SubTypes = n.SubTypes.Select(FeatureSubType.FromSubTypeNode).ToList(),
        Relationships = n.Relationships.Select(FeatureRelationship.FromRelationship).ToList(),
    };
}

public sealed class FeatureEntity
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public List<FeatureProperty> Properties { get; init; } = [];
    public List<string> EmittedEvents { get; init; } = [];
    public List<FeatureEventEmission> EventEmissions { get; init; } = [];

    internal static FeatureEntity FromEntityNode(EntityNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        Properties = n.Properties.Select(FeatureProperty.FromGraphProperty).ToList(),
        EmittedEvents = [..n.EmittedEvents],
        EventEmissions = n.EventEmissions.Select(FeatureEventEmission.FromGraphEmission).ToList(),
    };
}

public sealed class FeatureAggregate
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public List<FeatureProperty> Properties { get; init; } = [];
    public List<FeatureMethod> Methods { get; init; } = [];
    public List<string> ChildEntities { get; init; } = [];
    public List<string> EmittedEvents { get; init; } = [];
    public List<FeatureEventEmission> EventEmissions { get; init; } = [];

    internal static FeatureAggregate FromAggregateNode(AggregateNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        Properties = n.Properties.Select(FeatureProperty.FromGraphProperty).ToList(),
        Methods = n.Methods.Select(FeatureMethod.FromGraphMethod).ToList(),
        ChildEntities = [..n.ChildEntities],
        EmittedEvents = [..n.EmittedEvents],
        EventEmissions = n.EventEmissions.Select(FeatureEventEmission.FromGraphEmission).ToList(),
    };
}

public sealed class FeatureEventEmission
{
    public string EventType { get; init; } = "";
    public string MethodName { get; init; } = "";

    internal static FeatureEventEmission FromGraphEmission(EventEmissionInfo e) => new()
    {
        EventType = e.EventType,
        MethodName = e.MethodName,
    };
}

public sealed class FeatureValueObject
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public List<FeatureProperty> Properties { get; init; } = [];

    internal static FeatureValueObject FromValueObjectNode(ValueObjectNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        Properties = n.Properties.Select(FeatureProperty.FromGraphProperty).ToList(),
    };
}

public sealed class FeatureDomainEvent
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public List<FeatureProperty> Properties { get; init; } = [];
    public List<string> EmittedBy { get; init; } = [];
    public List<string> HandledBy { get; init; } = [];

    internal static FeatureDomainEvent FromDomainEventNode(DomainEventNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        Properties = n.Properties.Select(FeatureProperty.FromGraphProperty).ToList(),
        EmittedBy = [..n.EmittedBy],
        HandledBy = [..n.HandledBy],
    };
}

public sealed class FeatureHandler
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public List<string> Handles { get; init; } = [];

    internal static FeatureHandler FromHandlerNode(HandlerNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        Handles = [..n.Handles],
    };
}

public sealed class FeatureRepository
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }
    public string? ManagesAggregate { get; init; }

    internal static FeatureRepository FromRepositoryNode(RepositoryNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
        ManagesAggregate = n.ManagesAggregate,
    };
}

public sealed class FeatureDomainService
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public bool IsCustom { get; init; }

    internal static FeatureDomainService FromDomainServiceNode(DomainServiceNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        IsCustom = n.IsCustom,
    };
}

public sealed class FeatureSubType
{
    public string Name { get; init; } = "";
    public string FullName { get; init; } = "";
    public string? Description { get; init; }
    public string? Layer { get; init; }
    public List<FeatureProperty> Properties { get; init; } = [];

    internal static FeatureSubType FromSubTypeNode(SubTypeNode n) => new()
    {
        Name = n.Name,
        FullName = n.FullName,
        Description = n.Description,
        Layer = n.Layer,
        Properties = n.Properties.Select(FeatureProperty.FromGraphProperty).ToList(),
    };
}

/// <summary>
/// A property on a feature type. Does not shadow <see cref="System.Reflection.PropertyInfo"/>.
/// </summary>
public sealed class FeatureProperty
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public bool IsCollection { get; init; }
    public string? ReferenceTypeName { get; init; }

    internal static FeatureProperty FromGraphProperty(Graph.PropertyInfo p) => new()
    {
        Name = p.Name,
        TypeName = p.TypeName,
        IsCollection = p.IsCollection,
        ReferenceTypeName = p.ReferenceTypeName,
    };
}

/// <summary>
/// A method on a feature aggregate. Does not shadow <see cref="System.Reflection.MethodInfo"/>.
/// </summary>
public sealed class FeatureMethod
{
    public string Name { get; init; } = "";
    public string ReturnTypeName { get; init; } = "";
    public List<FeatureMethodParameter> Parameters { get; init; } = [];

    internal static FeatureMethod FromGraphMethod(Graph.MethodInfo m) => new()
    {
        Name = m.Name,
        ReturnTypeName = m.ReturnTypeName,
        Parameters = m.Parameters.Select(FeatureMethodParameter.FromGraphParameter).ToList(),
    };
}

/// <summary>
/// A method parameter in a <see cref="FeatureMethod"/>.
/// </summary>
public sealed class FeatureMethodParameter
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";

    internal static FeatureMethodParameter FromGraphParameter(Graph.MethodParameterInfo p) => new()
    {
        Name = p.Name,
        TypeName = p.TypeName,
    };
}

/// <summary>
/// A relationship between two types in a feature graph.
/// </summary>
public sealed class FeatureRelationship
{
    public string SourceType { get; init; } = "";
    public string TargetType { get; init; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RelationshipKind Kind { get; init; }
    public string? Label { get; init; }

    internal static FeatureRelationship FromRelationship(Relationship r) => new()
    {
        SourceType = r.SourceType,
        TargetType = r.TargetType,
        Kind = r.Kind,
        Label = r.Label,
    };
}
