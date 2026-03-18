using DomainModeling.Example.Domain;

namespace DomainModeling.Example.Shipping.Domain;

/// <summary>
/// Raised when a shipment is dispatched to the carrier.
/// </summary>
public sealed class ShipmentDispatchedEvent : DomainEvent
{
    public Guid ShipmentId { get; init; }
    public Guid OrderId { get; init; }
}

/// <summary>
/// Raised when a shipment has been delivered to the customer.
/// </summary>
public sealed class ShipmentDeliveredEvent : DomainEvent
{
    public Guid ShipmentId { get; init; }
    public Guid OrderId { get; init; }
}
