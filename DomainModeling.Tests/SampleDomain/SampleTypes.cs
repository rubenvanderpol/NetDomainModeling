// A pretend domain project's base classes and concrete types.
// These simulate what a REAL project might look like —
// the DomainModeling library knows nothing about them until
// the fluent API tells it how to identify them.

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
#pragma warning disable CS0067 // unused event

namespace DomainModeling.Tests.SampleDomain;

// ─── Base classes (the project's own DDD building blocks) ────────

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class BaseAggregateRoot : BaseEntity
{
    private readonly List<BaseDomainEvent> _events = [];
    public IReadOnlyCollection<BaseDomainEvent> Events => _events.AsReadOnly();
    protected void Raise(BaseDomainEvent @event) => _events.Add(@event);
}

public abstract class BaseDomainEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public abstract class BaseIntegrationEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public abstract class BaseValueObject;

public interface IEventHandler<in TEvent> where TEvent : BaseDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : BaseIntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

public interface ICommandHandler<in TCommand>
{
    Task HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}

public interface IRepository<T> where T : BaseAggregateRoot;

// ─── Value Objects ───────────────────────────────────────────────

public sealed class Address : BaseValueObject
{
    public required string Street { get; init; }
    public required string City { get; init; }
    public required string ZipCode { get; init; }
}

public sealed class Money : BaseValueObject
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "EUR";
}

// ─── Domain Events ───────────────────────────────────────────────

public sealed class OrderPlacedEvent : BaseDomainEvent
{
    public Guid OrderId { get; init; }
}

public sealed class OrderShippedEvent : BaseDomainEvent
{
    public Guid OrderId { get; init; }
}

public sealed class CustomerCreatedEvent : BaseDomainEvent
{
    public Guid CustomerId { get; init; }
}

public sealed class InvoiceCreatedEvent : BaseDomainEvent
{
    public Guid InvoiceId { get; init; }
}

public sealed class EntityDeletedEvent<TEntity> : BaseDomainEvent where TEntity : BaseEntity;

// ─── Integration Events ───────────────────────────────────────────

public sealed class OrderPlacedIntegrationEvent : BaseIntegrationEvent
{
    public Guid OrderId { get; init; }
}

public sealed class CustomerRegisteredIntegrationEvent : BaseIntegrationEvent
{
    public Guid CustomerId { get; init; }
}

// ─── Sub Types (not registered as domain types, but used as properties) ──────

public sealed class ContactInfo
{
    public required string Phone { get; init; }
    public required string Fax { get; init; }
}

// ─── Entities ────────────────────────────────────────────────────

public class OrderLine : BaseEntity
{
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public Money Price { get; init; } = new();
}

// ─── Aggregates ──────────────────────────────────────────────────

/// <summary>
/// The primary sales order aggregate used in scanner tests.
/// <domain>emits <see cref="OrderPlacedEvent"/></domain>
/// </summary>
public class Order : BaseAggregateRoot
{
    public required Customer Customer { get; init; }
    public List<OrderLine> Lines { get; init; } = [];
    public Address? ShippingAddress { get; init; }

    public void Place()
    {
        Raise(new OrderPlacedEvent { OrderId = Id });
    }

    /// <summary>
    /// <domain>emits <see cref="OrderPlacedEvent"/></domain>
    /// </summary>
    public void PlaceFromDocumentationOnly()
    {
    }

    public void Ship()
    {
        Raise(new OrderShippedEvent { OrderId = Id });
    }

    public void Delete()
    {
        Raise(new EntityDeletedEvent<Order>());
    }
}

/// <summary>
/// <domain>emits <see cref="EntityDeletedEvent{Customer}"/></domain>
/// </summary>
public class Customer : BaseAggregateRoot
{
    public required string Name { get; init; }
    public Address? BillingAddress { get; init; }
    public ContactInfo? Contact { get; init; }

    public void Register()
    {
        Raise(new CustomerCreatedEvent { CustomerId = Id });
    }
}

public sealed class Invoice : BaseAggregateRoot
{
    public required Guid OrderId { get; init; }

    private Invoice() { }

    public static Invoice Create(Guid orderId)
    {
        var invoice = new Invoice { OrderId = orderId };
        invoice.Raise(new InvoiceCreatedEvent { InvoiceId = invoice.Id });
        return invoice;
    }
}

// ─── Handlers ────────────────────────────────────────────────────

public class OrderPlacedHandler : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent @event, CancellationToken ct = default)
    {
        // Publish integration event
        var integrationEvent = new OrderPlacedIntegrationEvent { OrderId = @event.OrderId };
        return Task.CompletedTask;
    }
}

public class SendShipmentNotificationHandler : IEventHandler<OrderShippedEvent>
{
    public Task HandleAsync(OrderShippedEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class OrderDeletedEventHandler : IEventHandler<EntityDeletedEvent<Order>>
{
    public Task HandleAsync(EntityDeletedEvent<Order> @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class CustomerDeletedEventHandler : IEventHandler<EntityDeletedEvent<Customer>>
{
    public Task HandleAsync(EntityDeletedEvent<Customer> @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>
/// Handles the integration event published by the other context.
/// </summary>
public class OrderPlacedIntegrationHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

public record PlaceOrderCommand(Guid CustomerId, List<string> Products);

/// <summary>Command DTO with no handler — used to test explicit <c>Commands(...)</c> registration on <c>DDDBuilder</c>.</summary>
public record UnassignedCommand(string Reason);

public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand>
{
    public Task HandleAsync(PlaceOrderCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
}

public record GetOrderQuery(Guid OrderId);
public record OrderDto(Guid Id, string CustomerName);

public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto>
{
    public Task<OrderDto> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
        => Task.FromResult(new OrderDto(query.OrderId, "Test"));
}

// ─── Repositories ────────────────────────────────────────────────

public class OrderRepository : IRepository<Order>;
public class CustomerRepository : IRepository<Customer>;

// ─── Name-based handler (no generic interface) ───────────────────
// Mimics the DataHub pattern: detected by NameEndsWith("EventHandler"),
// handles a domain event via method parameter, publishes an integration event.

public class PublishCustomerRegisteredWhenCreatedEventHandler
{
    public Task Handle(CustomerCreatedEvent @event)
    {
        var integrationEvent = new CustomerRegisteredIntegrationEvent { CustomerId = @event.CustomerId };
        return Task.CompletedTask;
    }
}

// Async version — compiler transforms body into a nested state machine class
public class PublishCustomerRegisteredWhenCreatedAsyncEventHandler
{
    public async Task Handle(CustomerCreatedEvent @event)
    {
        var integrationEvent = new CustomerRegisteredIntegrationEvent { CustomerId = @event.CustomerId };
        await Task.CompletedTask;
    }
}
