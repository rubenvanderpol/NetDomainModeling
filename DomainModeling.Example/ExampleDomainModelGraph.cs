using System.Reflection;
using DomainModeling.Builder;
using DomainModeling.Example.Application;
using DomainModeling.Example.Domain;
using DomainModeling.Example.IntegrationEvents;
using DomainModeling.Example.Shipping.Domain;
using DomainModeling.Graph;

namespace DomainModeling.Example;

/// <summary>
/// Builds the sample <see cref="DomainGraph"/> used by the example web app and the Aspire workbench API.
/// </summary>
public static class ExampleDomainModelGraph
{
    static Assembly SharedExampleAssembly(Assembly anyDomainAssembly) =>
        anyDomainAssembly.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .First(a => string.Equals(a.GetName().Name, "DomainModeling.Example.Shared", StringComparison.Ordinal));

    /// <summary>
    /// Creates the catalog + shipping domain graph from the example assemblies.
    /// </summary>
    public static DomainGraph Create() =>
        DDDBuilder.Create(ctx => ctx
                .Entities(e => e.InheritsFrom<Entity>())
                .Aggregates(a => a.InheritsFrom<AggregateRoot>())
                .ValueObjects(v => v.InheritsFrom<ValueObject>())
                .DomainEvents(e => e.InheritsFrom<DomainEvent>())
                .IntegrationEvents(e => e.InheritsFrom<IntegrationEvent>())
                .EventHandlers(h => h
                    .Implements(typeof(IEventHandler<>))
                    .Implements(typeof(IIntegrationEventHandler<>)))
                .CommandHandlers(h => h.Implements(typeof(ICommandHandler<>)))
                .Commands(c => c.NameEndsWith("Command"))
                .Repositories(r => r.Implements(typeof(IRepository<>)))
            )
            .WithSharedAssembly(SharedExampleAssembly(typeof(Product).Assembly))
            .WithSharedAssembly(typeof(IntegrationEvent).Assembly, "IntegrationContracts")
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(typeof(Product).Assembly)
            )
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly)
            )
            .Build();
}
