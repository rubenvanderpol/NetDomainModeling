namespace DomainModeling.Example.Domain;

/// <summary>
/// Raised when a new product is added to the catalog.
/// </summary>
public sealed class ProductAddedEvent : DomainEvent
{
    public Guid ProductId { get; init; }
}

/// <summary>
/// Raised when product pricing changes.
/// </summary>
public sealed class ProductPriceChangedEvent : DomainEvent
{
    public Guid ProductId { get; init; }
    public Money OldPrice { get; init; } = new();
    public Money NewPrice { get; init; } = new();
}

/// <summary>
/// Raised when a new order is placed by a customer.
/// </summary>
public sealed class OrderPlacedEvent : DomainEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
}

/// <summary>
/// Raised when an order is shipped to the customer.
/// </summary>
public sealed class OrderShippedEvent : DomainEvent
{
    public Guid OrderId { get; init; }
}

/// <summary>
/// Raised when a new customer registers.
/// </summary>
public sealed class CustomerRegisteredEvent : DomainEvent
{
    public Guid CustomerId { get; init; }
}

/// <summary>
/// A generic domain event raised when any entity is deleted.
/// </summary>
public sealed class EntityDeletedEvent<TEntity> : DomainEvent where TEntity : Entity
{
    public Guid EntityId { get; init; }
}
