using DddStarter.Domain.Orders;

namespace DddStarter.Application.Abstractions;

public interface IApplicationDbContext
{
    void AddOrder(Order order);
    Task<Order?> FindOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
