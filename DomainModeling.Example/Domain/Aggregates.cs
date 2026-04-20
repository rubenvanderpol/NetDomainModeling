namespace DomainModeling.Example.Domain;

// ─── Entities ────────────────────────────────────────────────────

/// <summary>
/// A single line item in an order.
/// </summary>
/// <domain>
/// 
/// </domain>
public class OrderLine : Entity
{
    public required string ProductName { get; init; }
    public int Quantity { get; init; }
    public Money UnitPrice { get; init; } = new();
}

// ─── Aggregates ──────────────────────────────────────────────────

/// <summary>
/// A product in the catalog, managed by the inventory team.
/// </summary>
public class Product : AggregateRoot
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public Money Price { get; set; } = new();
    

    public void TestForMe(Money newPrice)
    {
        Raise(new ProductPriceChangedEvent
        {
            ProductId = Id,
            OldPrice = Price,
            NewPrice = newPrice
        });
        AddDomainEvent(new SharedOnlyCatalogEvent());
        Price = newPrice;
    }
}

/// <summary>
/// An order placed by a customer, containing one or more order lines.
/// </summary>
public class Order : AggregateRoot
{
    public required Customer Customer { get; init; }
    public List<OrderLine> Lines { get; init; } = [];
    public Address? ShippingAddress { get; init; }

    public void Place()
    {
        Raise(new OrderPlacedEvent { OrderId = Id, CustomerId = Customer.Id });
    }

    public void Ship()
    {
        Raise(new OrderShippedEvent { OrderId = Id });
    }
}

/// <summary>
/// A registered customer who can place orders.
/// <domain>emits <see cref="EntityDeletedEvent{Customer}"/></domain>
/// </summary>
public class Customer : AggregateRoot
{
    public required string Name { get; init; }
    public EmailAddress? Email { get; init; }
    public Address? BillingAddress { get; init; }

    public void Register()
    {
        Raise(new CustomerRegisteredEvent { CustomerId = Id });
    }
}

/// <summary>
/// Example aggregate that raises a closed generic domain event via <see cref="AggregateRoot.AddDomainEvent"/>.
/// </summary>
public class Organization : AggregateRoot
{
    public required string Name { get; init; }

    /// <summary>
    /// Illustrates tracking a generic record domain event from an overridden method.
    /// </summary>
    public override void DeleteEntity()
    {
        AddDomainEvent(new EntityDeletedEvent<Organization>(this));
    }
}
