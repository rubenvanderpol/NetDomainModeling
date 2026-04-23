using DddStarter.Application.Abstractions;
using DddStarter.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace DddStarter.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public void AddOrder(Order order)
    {
        Orders.Add(order);
    }

    public Task<Order?> FindOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
