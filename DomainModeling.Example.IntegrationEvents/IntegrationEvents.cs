namespace DomainModeling.Example.IntegrationEvents;

/// <summary>
/// Base class for integration events that cross bounded-context boundaries.
/// Typically published by domain event handlers for consumption by other contexts.
/// </summary>
public abstract class IntegrationEvent
{
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct = default);
}

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
