using DddStarter.Application.Abstractions;
using DddStarter.Domain.Orders;

namespace DddStarter.Application.Orders;

public sealed class OrderApplicationService
{
    private readonly IApplicationDbContext _dbContext;

    public OrderApplicationService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> CreateOrderAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = new Order(command.OrderNumber);
        _dbContext.AddOrder(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return order.Id;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.FindOrderByIdAsync(id, cancellationToken);
    }
}
