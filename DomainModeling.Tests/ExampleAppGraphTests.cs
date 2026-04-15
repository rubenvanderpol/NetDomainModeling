using System.Reflection;
using DomainModeling.Builder;
using DomainModeling.Example.Domain;
using DomainModeling.Example.IntegrationEvents;
using DomainModeling.Graph;
using DomainModeling.Example.Shipping.Domain;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

/// <summary>
/// Regression: DomainModeling.Example and DomainModeling.Example.Shared both define types like
/// <c>Money</c> under the same namespace, so scanning both assemblies produced duplicate
/// <see cref="Type.FullName"/> values and crashed the scanner. Integration contracts live in
/// <c>DomainModeling.Example.IntegrationEvents</c> and are registered with
/// <c>WithSharedAssembly(assembly, boundedContextName)</c> so they appear under a dedicated bounded context.
/// </summary>
public class ExampleAppGraphTests
{
    private static Assembly GetSharedExampleAssembly(Assembly domainAssembly) =>
        domainAssembly.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .First(a => string.Equals(a.GetName().Name, "DomainModeling.Example.Shared", StringComparison.Ordinal));

    private static Assembly GetIntegrationEventsAssembly() => typeof(IntegrationEvent).Assembly;

    [Fact]
    public void Build_ExampleLikeConfiguration_DoesNotThrowWhenDomainAndSharedShareTypeFullNames()
    {
        var catalogDomainAssembly = typeof(Product).Assembly;
        var sharedAssembly = GetSharedExampleAssembly(catalogDomainAssembly);

        var act = () => DDDBuilder.Create(ctx => ctx
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
            .WithSharedAssembly(sharedAssembly)
            .WithSharedAssembly(GetIntegrationEventsAssembly(), "IntegrationContracts")
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(catalogDomainAssembly))
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly))
            .Build();

        act.Should().NotThrow();

        var graph = act();
        graph.BoundedContexts.Should().HaveCount(3);
        foreach (var ctx in graph.BoundedContexts)
        {
            ctx.ValueObjects.Select(v => v.FullName).Should().OnlyHaveUniqueItems();
            ctx.Entities.Select(e => e.FullName).Should().OnlyHaveUniqueItems();
            ctx.Aggregates.Select(a => a.FullName).Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void Build_PlaceOrderCommandHandler_HasReferencesEdge_ToOrderAggregate_ForPlaceInvocation()
    {
        var catalogDomainAssembly = typeof(Product).Assembly;
        var sharedAssembly = GetSharedExampleAssembly(catalogDomainAssembly);

        var graph = DDDBuilder.Create(ctx => ctx
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
            .WithSharedAssembly(sharedAssembly)
            .WithSharedAssembly(GetIntegrationEventsAssembly(), "IntegrationContracts")
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(catalogDomainAssembly))
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly))
            .Build();

        var catalog = graph.BoundedContexts.Single(c => c.Name == "Catalog");
        catalog.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.References &&
            r.SourceType.Contains("PlaceOrderCommandHandler", StringComparison.Ordinal) &&
            r.TargetType.EndsWith(".Order", StringComparison.Ordinal) &&
            r.Label != null &&
            r.Label.Contains("Place", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_PlaceOrderCommandHandler_HasReferencesEdge_ToOrderRepository_ForIRepositoryDependency()
    {
        var catalogDomainAssembly = typeof(Product).Assembly;
        var sharedAssembly = GetSharedExampleAssembly(catalogDomainAssembly);

        var graph = DDDBuilder.Create(ctx => ctx
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
            .WithSharedAssembly(sharedAssembly)
            .WithSharedAssembly(GetIntegrationEventsAssembly(), "IntegrationContracts")
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(catalogDomainAssembly))
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly))
            .Build();

        var catalog = graph.BoundedContexts.Single(c => c.Name == "Catalog");
        catalog.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.References &&
            r.Label == "uses repository" &&
            r.SourceType.Contains("PlaceOrderCommandHandler", StringComparison.Ordinal) &&
            r.TargetType.Contains("OrderRepository", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ProductPriceChangedHandler_LinksToRegisterCustomerCommand_AndHandler()
    {
        var graph = BuildExampleGraph();
        var catalog = graph.BoundedContexts.Single(c => c.Name == "Catalog");

        var target = catalog.CommandHandlerTargets.Single(t => t.Name == "RegisterCustomerCommand");
        target.HandledBy.Should().Contain(h => h.Contains("ProductPriceChangedHandler", StringComparison.Ordinal));

        catalog.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.Handles &&
            r.SourceType.Contains("ProductPriceChangedHandler", StringComparison.Ordinal) &&
            r.TargetType.Contains("RegisterCustomerCommand", StringComparison.Ordinal) &&
            r.Label == "creates command");

        catalog.Relationships.Should().Contain(r =>
            r.Kind == RelationshipKind.References &&
            r.SourceType.Contains("ProductPriceChangedHandler", StringComparison.Ordinal) &&
            r.TargetType.Contains("RegisterCustomerCommandHandler", StringComparison.Ordinal));
    }

    private static DomainGraph BuildExampleGraph()
    {
        var catalogDomainAssembly = typeof(Product).Assembly;
        var sharedAssembly = GetSharedExampleAssembly(catalogDomainAssembly);

        return DDDBuilder.Create(ctx => ctx
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
            .WithSharedAssembly(sharedAssembly)
            .WithSharedAssembly(GetIntegrationEventsAssembly(), "IntegrationContracts")
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(catalogDomainAssembly))
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly))
            .Build();
    }

    [Fact]
    public void Build_IntegrationContractsContext_OwnsIntegrationEventsAndCrossContextCommands()
    {
        var graph = BuildExampleGraph();

        var catalog = graph.BoundedContexts.Single(c => c.Name == "Catalog");
        var shipping = graph.BoundedContexts.Single(c => c.Name == "Shipping");
        var contracts = graph.BoundedContexts.Single(c => c.Name == "IntegrationContracts");

        catalog.IntegrationEvents.Should().BeEmpty();
        shipping.IntegrationEvents.Should().BeEmpty();

        contracts.IntegrationEvents.Should().Contain(e => e.Name == "OrderPlacedIntegrationEvent");
        contracts.IntegrationEvents.Should().Contain(e => e.Name == "ShipmentDispatchedIntegrationEvent");

        var partnerCmd = typeof(PartnerNotificationCommand).FullName!;
        catalog.CommandHandlerTargets.Should().NotContain(t => t.FullName == partnerCmd);
        shipping.CommandHandlerTargets.Should().NotContain(t => t.FullName == partnerCmd);
        contracts.CommandHandlerTargets.Should().Contain(t => t.FullName == partnerCmd);
    }

    [Fact]
    public void Build_IntegrationEventPublishAndHandle_MergedOntoContractsContext()
    {
        var graph = BuildExampleGraph();
        var contracts = graph.BoundedContexts.Single(c => c.Name == "IntegrationContracts");
        var evt = contracts.IntegrationEvents.Single(e => e.Name == "OrderPlacedIntegrationEvent");

        evt.EmittedBy.Should().Contain(h => h.Contains("OrderPlacedHandler", StringComparison.Ordinal));
        evt.HandledBy.Should().Contain(h => h.Contains("OrderPlacedIntegrationHandler", StringComparison.Ordinal));
    }
}
