using DddStarter.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DddStarter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
    {
        ArgumentNullException.ThrowIfNull(configureDb);

        services.AddDbContext<Persistence.ApplicationDbContext>(configureDb);
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<Persistence.ApplicationDbContext>());

        return services;
    }
}
