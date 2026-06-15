namespace TypeScanning.Tests.SampleTypes;

public abstract class BaseEvent;

public sealed class OrderPlacedEvent : BaseEvent;

public interface IEventHandler<in T>;

public sealed class OrderPlacedHandler : IEventHandler<OrderPlacedEvent>
{
    public void Handle(OrderPlacedEvent evt) { }
}

public sealed class WrongSuffixHandler
{
    public void Handle(OrderPlacedEvent evt) { }
}

public sealed class PublishWhenCreatedHandler
{
    public void Handle(CustomerCreatedEvent evt) { }
}

public sealed class CustomerCreatedEvent : BaseEvent;

public interface ICommand;

public sealed class PlaceOrderCommand : ICommand
{
    public OrderSummary Summary { get; init; } = new();
}

public sealed class OrderSummary
{
    public string OrderId { get; init; } = "";
}

public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand>
{
    public void Handle(PlaceOrderCommand command) { }
}

public sealed class OrderWithCustomerId
{
    public string CustomerId { get; init; } = "";
}

public sealed class Customer;

public interface ICommandHandler<in T> where T : ICommand;
