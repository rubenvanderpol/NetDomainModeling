using FluentAssertions;
using TypeScanning.Configuration;
using TypeScanning.Convention;
using TypeScanning.Tests.SampleTypes;
using Xunit;

namespace TypeScanning.Tests;

public class TypeConventionBuilderTests
{
    [Fact]
    public void And_WithoutPriorRule_Throws()
    {
        var convention = new TypeConventionBuilder();

        var act = () => convention.And();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*follow a convention rule*");
    }

    [Fact]
    public void DanglingAnd_ThrowsOnMatch()
    {
        var convention = new TypeConventionBuilder();
        convention.Implements(typeof(IEventHandler<>)).And();

        var act = () => convention.Matches(typeof(OrderPlacedHandler));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*follow the last And()*");
    }

    [Fact]
    public void DoubleAnd_Throws()
    {
        var convention = new TypeConventionBuilder();

        var act = () => convention
            .Implements(typeof(IEventHandler<>))
            .And()
            .And()
            .NameEndsWith("Handler");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*before And()*");
    }

    [Fact]
    public void OrBranches_MatchAnyBranch()
    {
        var convention = new TypeConventionBuilder();
        convention
            .InheritsFrom<BaseEvent>()
            .NameEndsWith("Event");

        convention.Matches(typeof(OrderPlacedEvent)).Should().BeTrue();
        convention.Matches(typeof(OrderPlacedHandler)).Should().BeFalse();
    }

    [Fact]
    public void AndComposition_RequiresAllPredicatesInBranch()
    {
        var convention = new TypeConventionBuilder();
        convention
            .Implements(typeof(IEventHandler<>))
            .And()
            .NameEndsWith("Handler");

        convention.Matches(typeof(OrderPlacedHandler)).Should().BeTrue();
        convention.Matches(typeof(WrongSuffixHandler)).Should().BeFalse();
    }

    [Fact]
    public void Type_WithoutNestedPredicates_Throws()
    {
        var convention = new TypeConventionBuilder();

        var act = () => convention.Type(_ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one predicate*");
    }
}

public class TypeScannerTests
{
    [Fact]
    public void Scan_CategorizesTypesByConvention()
    {
        var assembly = typeof(OrderPlacedEvent).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Event", c => c.InheritsFrom<BaseEvent>())
                .WithCategory("Handler", c => c.Implements(typeof(IEventHandler<>))))
            .Build();

        var result = graph.Contexts.Single();
        result.Categories["Event"].Should().Contain(t => t.Name == "OrderPlacedEvent");
        result.Categories["Handler"].Should().Contain(t => t.Name == "OrderPlacedHandler");
    }

    [Fact]
    public void Scan_StructuralRule_DiscoversTypeFromHandleParameter()
    {
        var assembly = typeof(OrderPlacedEvent).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Event", c => c
                    .Type(t => t.NameEndsWith("Handler"))
                    .HasMethod("Handle")
                    .Parameters()
                    .First()
                    .Type()))
            .Build();

        var result = graph.Contexts.Single();
        result.Categories["Event"].Should().Contain(t => t.Name == "CustomerCreatedEvent");
    }

    [Fact]
    public void Scan_BuildsConfiguredPropertyRelationships()
    {
        var assembly = typeof(PlaceOrderCommand).Assembly;

        var graph = ScanBuilder.Create()
            .WithContext("Test", ctx => ctx
                .WithAssemblies(assembly)
                .WithCategory("Command", c => c.Implements(typeof(ICommand)))
                .WithCategory("Summary", c => c.NameEndsWith("Summary"))
                .WithRelationship("owns", r => r
                    .FromCategories("Command")
                    .PropertyReference()))
            .Build();

        var result = graph.Contexts.Single();
        result.Relationships.Should().Contain(r =>
            r.Kind == "owns"
            && r.SourceType.Contains("PlaceOrderCommand")
            && r.TargetType.Contains("OrderSummary")
            && r.Label == "Summary");
    }

    [Fact]
    public void Build_ThrowsWhenNoContexts()
    {
        var act = () => ScanBuilder.Create().Build();
        act.Should().Throw<InvalidOperationException>();
    }
}

public class TypeDisplayNamesTests
{
    [Fact]
    public void ShortName_FormatsNestedAndGenericTypes()
    {
        TypeDisplayNames.ShortName(typeof(Dictionary<,>)).Should().Be("Dictionary");
        TypeDisplayNames.ShortName(typeof(List<string>)).Should().Be("List<String>");
    }

    [Fact]
    public void FormatTypeReference_UsesCSharpKeywords()
    {
        TypeDisplayNames.FormatTypeReference(typeof(string)).Should().Be("string");
        TypeDisplayNames.FormatTypeReference(typeof(int)).Should().Be("int");
    }
}
