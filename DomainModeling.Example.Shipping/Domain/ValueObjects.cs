using DomainModeling.Example.Domain;

namespace DomainModeling.Example.Shipping.Domain;

// ─── Value Objects ───────────────────────────────────────────────

/// <summary>
/// A tracking number assigned to a shipment by the carrier.
/// </summary>
public sealed class TrackingNumber : ValueObject
{
    public required string Value { get; init; }
}

/// <summary>
/// Represents the weight of a shipment.
/// </summary>
public sealed class Weight : ValueObject
{
    public decimal Kilograms { get; init; }
}
