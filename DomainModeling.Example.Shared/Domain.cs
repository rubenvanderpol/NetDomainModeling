namespace DomainModeling.Example.Domain;

// ─── Marker (optional second axis for domain event discovery) ───

/// <summary>
/// Marker for all domain events. Class events typically inherit <see cref="DomainEvent"/>; record events implement this directly (records cannot inherit non-record base classes).
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

// ─── Base classes ────────────────────────────────────────────────

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyCollection<IDomainEvent> Events => _events.AsReadOnly();

    /// <summary>
    /// Records a domain event raised by this aggregate (alias for the common <c>Raise</c> pattern).
    /// </summary>
    protected void AddDomainEvent(IDomainEvent @event) => _events.Add(@event);

    protected void Raise(DomainEvent @event) => AddDomainEvent(@event);

    /// <summary>
    /// Optional hook for deletion flows; overrides may raise <see cref="EntityDeletedEvent{TEntity}"/>.
    /// </summary>
    public virtual void DeleteEntity()
    {
    }
}

public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public abstract class ValueObject;

// ─── Handler contracts ───────────────────────────────────────────

public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IRepository<T> where T : AggregateRoot
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
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

// ─── Shared repository base ─────────────────────────────────────

public class InMemoryRepository<T> : IRepository<T> where T : AggregateRoot
{
    private readonly Dictionary<Guid, T> _store = new();

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var aggregate);
        return Task.FromResult(aggregate);
    }

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
