using System.Text.Json.Serialization;

namespace DomainModeling.Graph;

/// <summary>
/// The root of the domain graph. Contains all discovered bounded contexts.
/// Serialize this to JSON to feed a frontend diagram renderer.
/// </summary>
public sealed class DomainGraph
{
    public List<BoundedContextNode> BoundedContexts { get; init; }

    internal DomainGraph(List<BoundedContextNode> boundedContexts)
    {
        BoundedContexts = boundedContexts;
    }

    /// <summary>
    /// Creates a new <see cref="DomainGraph"/> from the given bounded contexts.
    /// </summary>
    public DomainGraph(params BoundedContextNode[] boundedContexts)
    {
        BoundedContexts = [..boundedContexts];
    }

    /// <summary>
    /// Serializes the domain graph to indented JSON.
    /// </summary>
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, JsonOptions.Default);
    }
}

/// <summary>
/// Represents a bounded context with its discovered DDD building blocks.
/// </summary>
public sealed class BoundedContextNode
{
    public required string Name { get; init; }
    public List<EntityNode> Entities { get; init; } = [];
    public List<AggregateNode> Aggregates { get; init; } = [];
    public List<ValueObjectNode> ValueObjects { get; init; } = [];
    public List<DomainEventNode> DomainEvents { get; init; } = [];
    public List<DomainEventNode> IntegrationEvents { get; init; } = [];
    public List<HandlerNode> EventHandlers { get; init; } = [];
    /// <summary>
    /// Types referenced by command handlers via <see cref="RelationshipKind.Handles"/> that are not
    /// already another building block. Exposed so diagram UIs can render handler → target links (GitHub #10).
    /// </summary>
    public List<CommandHandlerTargetNode> CommandHandlerTargets { get; init; } = [];
    public List<HandlerNode> CommandHandlers { get; init; } = [];
    public List<HandlerNode> QueryHandlers { get; init; } = [];
    public List<RepositoryNode> Repositories { get; init; } = [];
    public List<DomainServiceNode> DomainServices { get; init; } = [];
    public List<SubTypeNode> SubTypes { get; init; } = [];

    /// <summary>All relationships between types in this bounded context.</summary>
    public List<Relationship> Relationships { get; init; } = [];
}

/// <summary>
/// A discovered entity.
/// </summary>
public sealed class EntityNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];

    /// <summary>Domain events this entity can raise (detected via methods / fields).</summary>
    public List<string> EmittedEvents { get; init; } = [];

    /// <summary>
    /// Domain event emissions including which method fired the event.
    /// </summary>
    public List<EventEmissionInfo> EventEmissions { get; init; } = [];
}

/// <summary>
/// A discovered aggregate root, including its child entities.
/// </summary>
public sealed class AggregateNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];
    public List<MethodInfo> Methods { get; init; } = [];
    public List<string> ChildEntities { get; init; } = [];
    public List<string> EmittedEvents { get; init; } = [];
    public List<EventEmissionInfo> EventEmissions { get; init; } = [];
}

/// <summary>
/// Represents an emitted event and the method that emitted it.
/// </summary>
public sealed class EventEmissionInfo
{
    public required string EventType { get; init; }
    public required string MethodName { get; init; }
}

/// <summary>
/// A discovered value object.
/// </summary>
public sealed class ValueObjectNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];
}

/// <summary>
/// A discovered domain event.
/// </summary>
public sealed class DomainEventNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];

    /// <summary>Types that emit this event.</summary>
    public List<string> EmittedBy { get; init; } = [];

    /// <summary>Handlers that handle this event.</summary>
    public List<string> HandledBy { get; init; } = [];
}

/// <summary>
/// A type that appears as the target of a <see cref="RelationshipKind.Handles"/> edge from a command handler.
/// </summary>
public sealed class CommandHandlerTargetNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];

    /// <summary>Command handlers that reference this type in <see cref="HandlerNode.Handles"/>.</summary>
    public List<string> HandledBy { get; init; } = [];
}

/// <summary>
/// A discovered handler (command, query, or domain event handler).
/// </summary>
public sealed class HandlerNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    /// <summary>The types this handler handles (the generic arguments).</summary>
    public List<string> Handles { get; init; } = [];
}

/// <summary>
/// A discovered repository.
/// </summary>
public sealed class RepositoryNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }

    /// <summary>The aggregate type this repository manages.</summary>
    public string? ManagesAggregate { get; init; }
}

/// <summary>
/// A discovered domain service.
/// </summary>
public sealed class DomainServiceNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    /// <summary>When <c>true</c>, indicates this type was manually created in the feature editor rather than discovered from assemblies.</summary>
    public bool IsCustom { get; init; }
}

/// <summary>
/// A discovered sub-type: a custom (non-primitive) type referenced by a property
/// on an aggregate, entity, or value object that is not itself a registered domain type.
/// </summary>
public sealed class SubTypeNode
{
    public required string Name { get; init; }
    public required string FullName { get; init; }

    /// <summary>Display name override (alias) set in the feature editor.</summary>
    public string? Alias { get; set; }

    /// <summary>Optional description (e.g. from explorer metadata).</summary>
    public string? Description { get; set; }

    /// <summary>The architectural layer this type belongs to (Domain, Application, Infrastructure).</summary>
    public string? Layer { get; init; }

    public List<PropertyInfo> Properties { get; init; } = [];
}

/// <summary>
/// Describes a public method on an aggregate.
/// </summary>
public sealed class MethodInfo
{
    public required string Name { get; init; }
    public required string ReturnTypeName { get; init; }
    public List<MethodParameterInfo> Parameters { get; init; } = [];
}

/// <summary>
/// Describes a parameter of a method.
/// </summary>
public sealed class MethodParameterInfo
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
}

/// <summary>
/// Describes a property on a domain type.
/// </summary>
public sealed class PropertyInfo
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public bool IsCollection { get; init; }

    /// <summary>If this property references another domain type, stores the full type name.</summary>
    public string? ReferenceTypeName { get; init; }
}

/// <summary>
/// A relationship between two types in the domain graph.
/// </summary>
public sealed class Relationship
{
    public required string SourceType { get; init; }
    public required string TargetType { get; init; }
    public required RelationshipKind Kind { get; init; }
    public string? Label { get; init; }
}

/// <summary>
/// The kind of relationship between domain types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelationshipKind
{
    /// <summary>Entity/Aggregate owns another entity (composition).</summary>
    Contains,

    /// <summary>Entity references another entity (association).</summary>
    References,

    /// <summary>Entity/Aggregate references another type by ID property (e.g. OrganizationId → Organization).</summary>
    ReferencesById,

    /// <summary>Entity/Aggregate emits a domain event.</summary>
    Emits,

    /// <summary>Handler handles a domain event / command / query.</summary>
    Handles,

    /// <summary>Repository manages an aggregate.</summary>
    Manages,

    /// <summary>Handler publishes an integration event.</summary>
    Publishes,

    /// <summary>Entity/Aggregate/ValueObject has a property of another domain type (single reference).</summary>
    Has,

    /// <summary>Entity/Aggregate/ValueObject has a collection property of another domain type.</summary>
    HasMany
}

/// <summary>
/// Custom metadata (alias and description) that users can assign to any domain type.
/// </summary>
public sealed class TypeMetadata
{
    public string? Alias { get; set; }
    public string? Description { get; set; }
}

internal static class JsonOptions
{
    public static readonly System.Text.Json.JsonSerializerOptions Default = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
