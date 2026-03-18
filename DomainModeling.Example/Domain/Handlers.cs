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

// ─── Command handlers ────────────────────────────────────────────

public record PlaceOrderCommand(Guid CustomerId, List<string> Products);

public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand>
{
    public Task HandleAsync(PlaceOrderCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
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
