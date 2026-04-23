namespace DddStarter.Application.Orders;

/// <summary>
/// Command for creating a new order.
/// </summary>
public sealed record CreateOrderCommand(string OrderNumber);
