using DomainModeling.Example.IntegrationEvents;

namespace DomainModeling.Example.Domain;

// ─── Event handlers ──────────────────────────────────────────────

public class OrderPlacedHandler : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent @event, CancellationToken ct = default)
    {
        // Publish integration event for other bounded contexts
        var integrationEvent = new OrderPlacedIntegrationEvent
        {
            OrderId = @event.OrderId,
            CustomerId = @event.CustomerId
        };
        return Task.CompletedTask;
    }
}

/// <summary>
/// Example of an event handler that issues another command: the domain explorer shows
/// <c>Handles</c> edges to the command DTO and <c>References</c> to the command handler (see GitHub #49).
/// </summary>
public class ProductPriceChangedHandler : IEventHandler<ProductPriceChangedEvent>
{
    private readonly RegisterCustomerCommandHandler _registerCustomer = new();

    public Task HandleAsync(ProductPriceChangedEvent @event, CancellationToken ct = default)
    {
        // Illustrative follow-up command (e.g. notify marketing when pricing changes)
        var cmd = new RegisterCustomerCommand("Price watcher", "pricing@example.com");
        return _registerCustomer.HandleAsync(cmd, ct);
    }
}

public class SendShipmentNotificationHandler : IEventHandler<OrderShippedEvent>
{
    public Task HandleAsync(OrderShippedEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class WelcomeEmailHandler : IEventHandler<CustomerRegisteredEvent>
{
    public Task HandleAsync(CustomerRegisteredEvent @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class CustomerDeletedHandler : IEventHandler<EntityDeletedEvent<Customer>>
{
    public Task HandleAsync(EntityDeletedEvent<Customer> @event, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ─── Command handlers ────────────────────────────────────────────

public record PlaceOrderCommand(Guid CustomerId, List<string> Products);

public class PlaceOrderCommandHandler(IRepository<Order> orders) : ICommandHandler<PlaceOrderCommand>
{
    public async Task HandleAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orders);

        var order = new Order
        {
            Customer = new Customer { Name = "MyCustomer" }
        };

        order.Place();
        await orders.AddAsync(order, ct).ConfigureAwait(false);
    }
}

public record RegisterCustomerCommand(string Name, string Email);

public class RegisterCustomerCommandHandler : ICommandHandler<RegisterCustomerCommand>
{
    public Task HandleAsync(RegisterCustomerCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
}

// ─── Repositories ────────────────────────────────────────────────

public class OrderRepository : InMemoryRepository<Order>;
public class CustomerRepository : InMemoryRepository<Customer>;
public class ProductRepository : InMemoryRepository<Product>;
