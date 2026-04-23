namespace DddStarter.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
        OrderNumber = string.Empty;
    }

    public Order(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new ArgumentException("Order number is required.", nameof(orderNumber));
        }

        Id = Guid.NewGuid();
        OrderNumber = orderNumber.Trim();
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public decimal TotalAmount => _lines.Sum(x => x.LineTotal);

    public void AddLine(string sku, int quantity, decimal unitPrice)
    {
        _lines.Add(new OrderLine(sku, quantity, unitPrice));
    }
}

public sealed class OrderLine
{
    private OrderLine()
    {
        Sku = string.Empty;
    }

    internal OrderLine(string sku, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        Sku = sku.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string Sku { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
