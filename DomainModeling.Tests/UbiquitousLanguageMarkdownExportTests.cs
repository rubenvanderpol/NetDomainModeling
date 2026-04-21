using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class UbiquitousLanguageMarkdownExportTests
{
    [Fact]
    public void Build_IncludesAggregateAliasDescriptionsHasAndHasManyAndDomainEvents()
    {
        var orderLineType = "Demo.OrderLine";
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Orders",
                Aggregates =
                [
                    new AggregateNode
                    {
                        Name = "Order",
                        FullName = "Demo.Order",
                        Alias = "Customer order",
                        Description = "A placed order in the system.",
                    },
                ],
                Entities =
                [
                    new EntityNode
                    {
                        Name = "OrderLine",
                        FullName = orderLineType,
                        Alias = "Line item",
                        Description = "One product line on an order.",
                    },
                ],
                DomainEvents =
                [
                    new DomainEventNode
                    {
                        Name = "OrderPlacedEvent",
                        FullName = "Demo.OrderPlacedEvent",
                        Alias = "Order placed",
                        Description = "Raised when checkout completes.",
                    },
                ],
                Relationships =
                [
                    new Relationship
                    {
                        SourceType = "Demo.Order",
                        TargetType = orderLineType,
                        Kind = RelationshipKind.HasMany,
                    },
                    new Relationship
                    {
                        SourceType = "Demo.Order",
                        TargetType = "Demo.ShipmentRef",
                        Kind = RelationshipKind.Has,
                    },
                ],
            });

        var md = UbiquitousLanguageMarkdownExport.Build(graph);

        md.Should().Contain("## Bounded context: Orders");
        md.Should().Contain("#### Customer order");
        md.Should().Contain("A placed order in the system.");
        md.Should().Contain("**has**");
        md.Should().Contain("`ShipmentRef`");
        md.Should().Contain("**has many**");
        md.Should().Contain("`Line item`");
        md.Should().Contain("_(entity)_");
        md.Should().Contain("One product line on an order.");
        md.Should().Contain("#### Order placed");
        md.Should().Contain("Raised when checkout completes.");
    }

    [Fact]
    public void Build_DepthLimit_OmitsConceptBlockBeyondFourthHopFromAggregate()
    {
        const string deepestMarker = "ONLY_AT_LEVEL_FIVE";
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Deep",
                Aggregates =
                [
                    new AggregateNode { Name = "Root", FullName = "Demo.Root", Alias = "Root agg" },
                ],
                Entities =
                [
                    new EntityNode { Name = "E1", FullName = "Demo.E1", Alias = "Hop 1" },
                    new EntityNode { Name = "E2", FullName = "Demo.E2", Alias = "Hop 2" },
                    new EntityNode { Name = "E3", FullName = "Demo.E3", Alias = "Hop 3" },
                    new EntityNode { Name = "E4", FullName = "Demo.E4", Alias = "Hop 4" },
                    new EntityNode
                    {
                        Name = "E5",
                        FullName = "Demo.E5",
                        Alias = "Hop 5",
                        Description = deepestMarker,
                    },
                ],
                Relationships =
                [
                    new Relationship { SourceType = "Demo.Root", TargetType = "Demo.E1", Kind = RelationshipKind.Has },
                    new Relationship { SourceType = "Demo.E1", TargetType = "Demo.E2", Kind = RelationshipKind.Has },
                    new Relationship { SourceType = "Demo.E2", TargetType = "Demo.E3", Kind = RelationshipKind.Has },
                    new Relationship { SourceType = "Demo.E3", TargetType = "Demo.E4", Kind = RelationshipKind.Has },
                    new Relationship { SourceType = "Demo.E4", TargetType = "Demo.E5", Kind = RelationshipKind.Has },
                ],
            });

        var md = UbiquitousLanguageMarkdownExport.Build(graph);

        md.Should().Contain("Hop 4");
        md.Should().NotContain(deepestMarker);
    }

    [Fact]
    public void Build_IncludesContainsAndReferencesByIdAndValueObject()
    {
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Shop",
                Aggregates =
                [
                    new AggregateNode
                    {
                        Name = "Cart",
                        FullName = "Demo.Cart",
                        Alias = "Shopping cart",
                        ChildEntities = ["Demo.CartLine"],
                    },
                ],
                Entities =
                [
                    new EntityNode
                    {
                        Name = "CartLine",
                        FullName = "Demo.CartLine",
                        Alias = "Cart line",
                        Description = "One row in the cart.",
                    },
                ],
                ValueObjects =
                [
                    new ValueObjectNode
                    {
                        Name = "Sku",
                        FullName = "Demo.Sku",
                        Alias = "Stock keeping unit",
                        Description = "Product identifier in catalog.",
                    },
                ],
                Relationships =
                [
                    new Relationship
                    {
                        SourceType = "Demo.Cart",
                        TargetType = "Demo.CartLine",
                        Kind = RelationshipKind.Contains,
                        Label = "contains",
                    },
                    new Relationship
                    {
                        SourceType = "Demo.CartLine",
                        TargetType = "Demo.Sku",
                        Kind = RelationshipKind.Has,
                    },
                    new Relationship
                    {
                        SourceType = "Demo.CartLine",
                        TargetType = "Demo.Product",
                        Kind = RelationshipKind.ReferencesById,
                        Label = "ProductId",
                    },
                ],
            });

        var md = UbiquitousLanguageMarkdownExport.Build(graph);

        md.Should().Contain("**contains**");
        md.Should().Contain("`Cart line`");
        md.Should().Contain("**references by id**");
        md.Should().Contain("_(via `ProductId`)_");
        md.Should().Contain("_(value object)_");
        md.Should().Contain("Stock keeping unit");
    }
}
