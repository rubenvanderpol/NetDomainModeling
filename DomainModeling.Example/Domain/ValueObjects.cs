namespace DomainModeling.Example.Domain;

// ─── Value Objects ───────────────────────────────────────────────

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
