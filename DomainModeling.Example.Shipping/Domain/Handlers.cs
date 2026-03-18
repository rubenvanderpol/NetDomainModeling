using DomainModeling.Example.Domain;

namespace DomainModeling.Example.Shipping.Domain;

// ─── Event handlers ──────────────────────────────────────────────

public class ShipmentDispatchedHandler : IEventHandler<ShipmentDispatchedEvent>
{
    public Task HandleAsync(ShipmentDispatchedEvent @event, CancellationToken ct = default)
    {
        // Publish integration event for other bounded contexts
        var integrationEvent = new ShipmentDispatchedIntegrationEvent
        {
            ShipmentId = @event.ShipmentId,
            OrderId = @event.OrderId
        };
        return Task.CompletedTask;
    }
}

public class ShipmentDeliveredHandler : IEventHandler<ShipmentDeliveredEvent>
{
    public Task HandleAsync(ShipmentDeliveredEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ─── Command handlers ────────────────────────────────────────────

public record DispatchShipmentCommand(Guid OrderId, string TrackingNumber);

public class DispatchShipmentCommandHandler : ICommandHandler<DispatchShipmentCommand>
{
    public Task HandleAsync(DispatchShipmentCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ─── Integration event handlers ──────────────────────────────────

/// <summary>
/// Handles the order-placed integration event from the Catalog context
/// to trigger shipment preparation.
/// </summary>
public class OrderPlacedIntegrationHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public Task HandleAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ─── Repositories ────────────────────────────────────────────────

public class ShipmentRepository : InMemoryRepository<Shipment>;
public class CarrierRepository : InMemoryRepository<Carrier>;
