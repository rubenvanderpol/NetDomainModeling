namespace DomainModeling.Example.Domain;

// ─── Base classes ────────────────────────────────────────────────

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class AggregateRoot : Entity
{
    private readonly List<DomainEvent> _events = [];
    public IReadOnlyCollection<DomainEvent> Events => _events.AsReadOnly();
    protected void Raise(DomainEvent @event) => _events.Add(@event);
}

public abstract class DomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Base class for integration events that cross bounded-context boundaries.
/// Typically published by domain event handlers for consumption by other contexts.
/// </summary>
public abstract class IntegrationEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public abstract class ValueObject;

// ─── Handler contracts ───────────────────────────────────────────

public interface IEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IRepository<T> where T : AggregateRoot
{
    Task AddAsync(T aggregate, CancellationToken ct = default);
    Task UpdateAsync(T aggregate, CancellationToken ct = default);
    Task DeleteAsync(T aggregate, CancellationToken ct = default);
}

// ─── Shared Value Objects ────────────────────────────────────────

/// <summary>
/// Represents a monetary amount with its currency.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "EUR";
}

/// <summary>
/// A physical or mailing address.
/// </summary>
public sealed class Address : ValueObject
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string Country { get; init; }
    public required string PostalCode { get; init; }
}

/// <summary>
/// An email address value object with basic validation.
/// </summary>
public sealed class EmailAddress : ValueObject
{
    public required string Value { get; init; }
}

// ─── Integration Events ──────────────────────────────────────────

/// <summary>
/// Published when an order has been placed, for consumption by the Shipping context.
/// </summary>
public sealed class OrderPlacedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
}

/// <summary>
/// Published when a shipment has been dispatched, for consumption by the Sales context.
/// </summary>
public sealed class ShipmentDispatchedIntegrationEvent : IntegrationEvent
{
    public Guid ShipmentId { get; init; }
    public Guid OrderId { get; init; }
}

// ─── Shared repository base ─────────────────────────────────────

public class InMemoryRepository<T> : IRepository<T> where T : AggregateRoot
{
    private readonly Dictionary<Guid, T> _store = new();

    public Task AddAsync(T aggregate, CancellationToken ct = default)
    {
        _store[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T aggregate, CancellationToken ct = default)
    {
        _store[aggregate.Id] = aggregate;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T aggregate, CancellationToken ct = default)
    {
        _store.Remove(aggregate.Id);
        return Task.CompletedTask;
    }
}
