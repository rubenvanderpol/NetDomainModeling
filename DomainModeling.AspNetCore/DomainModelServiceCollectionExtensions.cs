using Microsoft.Extensions.DependencyInjection;

namespace DomainModeling.AspNetCore;

/// <summary>
/// DI registration for the domain model explorer and optional Trace view (GitHub #34).
/// </summary>
public static class DomainModelServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DomainModeling.Graph.DomainGraph"/> as a singleton.
    /// </summary>
    public static IServiceCollection AddDomainModel(this IServiceCollection services, DomainModeling.Graph.DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        services.AddSingleton(graph);
        return services;
    }

    /// <summary>
    /// Registers SignalR and <see cref="IDomainModelTraceNotifier"/> for the Trace tab.
    /// Call <see cref="DomainModelEndpointExtensions.MapDomainModel"/> with <c>EnableTraceView = true</c>
    /// and map the hub endpoint (done inside <c>MapDomainModel</c>).
    /// </summary>
    public static IServiceCollection AddDomainModelTracing(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<IDomainModelTraceNotifier, DomainModelTraceNotifier>();
        services.AddSingleton<DomainModelTraceLastNotification>();
        return services;
    }
}
