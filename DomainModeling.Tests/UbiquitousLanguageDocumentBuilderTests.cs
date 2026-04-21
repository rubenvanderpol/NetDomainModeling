using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class UbiquitousLanguageDocumentBuilderTests
{
    [Fact]
    public void FromJson_RoundTripsBoundedContextName()
    {
        var g = new DomainGraph(new BoundedContextNode { Name = "Catalog" });
        var g2 = DomainGraph.FromJson(g.ToJson());
        g2.BoundedContexts.Should().ContainSingle().Which.Name.Should().Be("Catalog");
    }

    [Fact]
    public void Build_ProducesBoundedContextWithAggregateAndEvent()
    {
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Shop",
                Aggregates =
                [
                    new AggregateNode { Name = "Cart", FullName = "Demo.Cart", Alias = "Basket", Description = "A cart." },
                ],
                DomainEvents =
                [
                    new DomainEventNode { Name = "CartCleared", FullName = "Demo.CartCleared", Description = "Fired when empty." },
                ],
            });

        var doc = UbiquitousLanguageDocumentBuilder.Build(graph);

        doc.Language.Should().Be("en");
        doc.AvailableLanguages.Should().Contain("en");
        doc.BoundedContexts.Should().ContainSingle().Which.Name.Should().Be("Shop");
        var bc = doc.BoundedContexts[0];
        bc.Aggregates.Roots.Should().ContainSingle().Which.DisplayName.Should().Be("Basket");
        bc.DomainEvents.Items.Should().ContainSingle().Which.DisplayName.Should().Be("CartCleared");
    }
}
