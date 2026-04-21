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
        md.Should().Contain("#### Order placed");
        md.Should().Contain("Raised when checkout completes.");
    }
}
