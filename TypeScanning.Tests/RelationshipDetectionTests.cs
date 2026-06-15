using FluentAssertions;
using TypeScanning.Configuration;
using TypeScanning.Relationships;
using TypeScanning.Tests.SampleTypes;
using Xunit;

namespace TypeScanning.Tests;

public class RelationshipDetectionTests
{
    [Fact]
    public void WithRelationship_UsesConsumerDefinedKindName()
    {
        var assembly = typeof(PlaceOrderCommand).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Command", c => c.Implements(typeof(ICommand)))
                .WithCategory("Summary", c => c.NameEndsWith("Summary"))
                .WithRelationship("references", r => r
                    .FromCategories("Command")
                    .PropertyReference(o => o.ExcludeCollections = true)))
            .Build();

        graph.Contexts.Single().Relationships
            .Should().OnlyContain(r => r.Kind == "references");
    }

    [Fact]
    public void IdPropertyReference_DetectsReferencesById()
    {
        var assembly = typeof(OrderWithCustomerId).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Order", c => c.Where(t => t.Name == nameof(OrderWithCustomerId)))
                .WithCategory("Customer", c => c.NameEndsWith("Customer"))
                .WithRelationship("by-id", r => r
                    .FromCategories("Order")
                    .IdPropertyReference(o => o.PreferTargetCategories = ["Customer"])))
            .Build();

        graph.Contexts.Single().Relationships.Should().Contain(r =>
            r.Kind == "by-id"
            && r.SourceType.Contains("OrderWithCustomerId")
            && r.TargetType.Contains("Customer")
            && r.Label == "CustomerId");
    }

    [Fact]
    public void InterfaceGenericArguments_DetectsHandledTypes()
    {
        var assembly = typeof(OrderPlacedHandler).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Event", c => c.InheritsFrom<BaseEvent>())
                .WithCategory("Handler", c => c.Implements(typeof(IEventHandler<>)))
                .WithRelationship("handles", r => r
                    .FromCategories("Handler")
                    .InterfaceGenericArguments()))
            .Build();

        graph.Contexts.Single().Relationships.Should().Contain(r =>
            r.Kind == "handles"
            && r.SourceType.Contains("OrderPlacedHandler")
            && r.TargetType.Contains("OrderPlacedEvent"));
    }

    [Fact]
    public void CustomDetector_IsSupported()
    {
        var assembly = typeof(OrderPlacedEvent).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Event", c => c.InheritsFrom<BaseEvent>())
                .WithRelationship("linked", r => r
                    .FromCategories("Event")
                    .DetectUsing(ctx => ctx.GetTypesInCategories("Event")
                        .Select(t => new Model.ScannedRelationship
                        {
                            Kind = "linked",
                            SourceType = t.FullName,
                            TargetType = t.FullName,
                            Label = "self"
                        }))))
            .Build();

        graph.Contexts.Single().Relationships.Should().Contain(r => r.Kind == "linked" && r.Label == "self");
    }

    [Fact]
    public void WithRelationship_WithoutStrategy_Throws()
    {
        var act = () => ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(typeof(OrderPlacedEvent).Assembly)
                .WithCategory("Event", c => c.InheritsFrom<BaseEvent>())
                .WithRelationship("broken", r => r.FromCategories("Event")))
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no detection strategy*");
    }
}
