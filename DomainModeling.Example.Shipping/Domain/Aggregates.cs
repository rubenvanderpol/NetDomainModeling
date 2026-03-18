using DomainModeling.Example.Domain;

namespace DomainModeling.Example.Shipping.Domain;

// ─── Entities ────────────────────────────────────────────────────

/// <summary>
/// A single parcel within a shipment.
/// </summary>
public class Parcel : Entity
{
    public required string Label { get; init; }
    public Weight Weight { get; init; } = new();
}

// ─── Aggregates ──────────────────────────────────────────────────

/// <summary>
/// A shipment dispatched to fulfill an order.
/// </summary>
public class Shipment : AggregateRoot
{
    public Guid OrderId { get; init; }
    public required Address Destination { get; init; }
    public TrackingNumber? TrackingNumber { get; private set; }
    public List<Parcel> Parcels { get; init; } = [];

    public void Dispatch(string trackingNumber)
    {
        TrackingNumber = new TrackingNumber { Value = trackingNumber };
        Raise(new ShipmentDispatchedEvent { ShipmentId = Id, OrderId = OrderId });
    }

    public void Deliver()
    {
        Raise(new ShipmentDeliveredEvent { ShipmentId = Id, OrderId = OrderId });
    }
}

/// <summary>
/// A carrier responsible for transporting shipments.
/// </summary>
public class Carrier : AggregateRoot
{
    public required string Name { get; init; }
    public required string Code { get; init; }
}
