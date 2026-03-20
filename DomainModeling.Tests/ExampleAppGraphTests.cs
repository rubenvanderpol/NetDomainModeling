using System.Reflection;
using DomainModeling.Builder;
using DomainModeling.Example.Domain;
using DomainModeling.Graph;
using DomainModeling.Example.Shipping.Domain;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

/// <summary>
/// Regression: DomainModeling.Example and DomainModeling.Example.Shared both define types like
/// <c>Money</c> under the same namespace, so scanning both assemblies produced duplicate
/// <see cref="Type.FullName"/> values and crashed the scanner.
/// </summary>
public class ExampleAppGraphTests
{
    private static Assembly GetSharedExampleAssembly(Assembly domainAssembly) =>
        domainAssembly.GetReferencedAssemblies()
            .Select(Assembly.Load)
            .First(a => string.Equals(a.GetName().Name, "DomainModeling.Example.Shared", StringComparison.Ordinal));

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
                .Repositories(r => r.Implements(typeof(IRepository<>)))
            )
            .WithSharedAssembly(sharedAssembly)
            .WithBoundedContext("Catalog", ctx => ctx
                .WithDomainAssembly(catalogDomainAssembly))
            .WithBoundedContext("Shipping", ctx => ctx
                .WithDomainAssembly(typeof(Shipment).Assembly))
            .Build();

        act.Should().NotThrow();

        var graph = act();
        graph.BoundedContexts.Should().HaveCount(2);
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
                .Repositories(r => r.Implements(typeof(IRepository<>)))
            )
            .WithSharedAssembly(sharedAssembly)
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
}
