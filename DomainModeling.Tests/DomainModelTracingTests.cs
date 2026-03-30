using DomainModeling.AspNetCore;
using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class DomainModelTracingTests
{
    [Fact]
    public void EventTypeKey_OpenGenericDefinition_UsesDefinitionFullName()
    {
        var t = typeof(List<>);
        DomainModelTracing.EventTypeKey(t).Should().Be(t.FullName);
    }

    [Fact]
    public void ResolveHandlers_MatchesConcreteEventAndCollectsHandlers()
    {
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Ctx",
                DomainEvents =
                [
                    new DomainEventNode
                    {
                        Name = "OrderPlaced",
                        FullName = "App.OrderPlacedEvent",
                        HandledBy = ["App.OrderPlacedHandler"],
                    },
                ],
                EventHandlers =
                [
                    new HandlerNode
                    {
                        Name = "OrderPlacedHandler",
                        FullName = "App.OrderPlacedHandler",
                        Handles = ["App.OrderPlacedEvent"],
                    },
                ],
            });

        var (handlers, contexts) = DomainModelTracing.ResolveHandlers(graph, "App.OrderPlacedEvent", boundedContextFilter: null);

        handlers.Should().ContainSingle().Which.Should().Be("App.OrderPlacedHandler");
        contexts.Should().ContainSingle().Which.Should().Be("Ctx");
    }

    [Fact]
    public void ResolveHandlers_RespectsBoundedContextFilter()
    {
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "A",
                DomainEvents =
                [
                    new DomainEventNode { Name = "E", FullName = "App.E", HandledBy = ["App.H1"] },
                ],
                EventHandlers =
                [
                    new HandlerNode { Name = "H1", FullName = "App.H1", Handles = ["App.E"] },
                ],
            },
            new BoundedContextNode
            {
                Name = "B",
                DomainEvents =
                [
                    new DomainEventNode { Name = "E", FullName = "App.E", HandledBy = ["App.H2"] },
                ],
                EventHandlers =
                [
                    new HandlerNode { Name = "H2", FullName = "App.H2", Handles = ["App.E"] },
                ],
            });

        var (handlers, _) = DomainModelTracing.ResolveHandlers(graph, "App.E", boundedContextFilter: "B");

        handlers.Should().ContainSingle().Which.Should().Be("App.H2");
    }
}
