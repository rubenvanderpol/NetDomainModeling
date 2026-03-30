using DomainModeling.Example.Domain;
using MediatR;

namespace DomainModeling.Example.Application;

/// <summary>
/// Application command to mark an order as shipped (invokes <see cref="Order.Ship"/> on the aggregate).
/// </summary>
public sealed record ShipOrderCommand(Guid OrderId) : IRequest<ShipOrderResult>;

public sealed record ShipOrderResult(bool Success, string? Error);

public sealed class ShipOrderCommandHandler(IRepository<Order> orders, IPublisher publisher)
    : IRequestHandler<ShipOrderCommand, ShipOrderResult>
{
    public async Task<ShipOrderResult> Handle(ShipOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return new ShipOrderResult(false, "Order not found.");

        order.Ship();
        await orders.UpdateAsync(order, cancellationToken).ConfigureAwait(false);
        await publisher.Publish(new OrderShippedNotification(order.Id), cancellationToken).ConfigureAwait(false);

        return new ShipOrderResult(true, null);
    }
}

/// <summary>
/// Side effects after an order has been shipped (application-level reaction; complements domain <see cref="OrderShippedEvent"/>).
/// </summary>
public sealed record OrderShippedNotification(Guid OrderId) : INotification;

public sealed class OrderShippedNotificationHandler : INotificationHandler<OrderShippedNotification>
{
    public Task Handle(OrderShippedNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
