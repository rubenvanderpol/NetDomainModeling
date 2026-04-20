using DomainModeling.AspNetCore;
using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class FeatureJsonConverterTests
{
    [Fact]
    public void ToDomainGraph_HandlesEdge_LinksCommandHandlerAndCommandTarget()
    {
        const string json = """
            {
              "nodes": [
                { "id": "App.PlaceOrderCommand", "name": "PlaceOrderCommand", "kind": "commandHandlerTarget", "isCustom": false, "props": [] },
                { "id": "App.PlaceOrderCommandHandler", "name": "PlaceOrderCommandHandler", "kind": "commandHandler", "isCustom": false, "props": [] }
              ],
              "edges": [
                { "source": "App.PlaceOrderCommandHandler", "target": "App.PlaceOrderCommand", "kind": "Handles", "label": "" }
              ],
              "positions": {}
            }
            """;

        var graph = FeatureJsonConverter.ToDomainGraph(json, "Checkout");
        var ctx = graph.BoundedContexts.Should().ContainSingle().Subject;

        var handler = ctx.CommandHandlers.Should().ContainSingle().Subject;
        handler.Handles.Should().Equal("App.PlaceOrderCommand");

        var target = ctx.CommandHandlerTargets.Should().ContainSingle().Subject;
        target.HandledBy.Should().Equal("App.PlaceOrderCommandHandler");
    }

    [Fact]
    public void ToDomainGraph_HandlesEdge_ReversedDirection_StillLinks()
    {
        const string json = """
            {
              "nodes": [
                { "id": "App.Cmd", "name": "Cmd", "kind": "commandHandlerTarget", "isCustom": false, "props": [] },
                { "id": "App.H", "name": "H", "kind": "commandHandler", "isCustom": false, "props": [] }
              ],
              "edges": [
                { "source": "App.Cmd", "target": "App.H", "kind": "Handles", "label": "" }
              ],
              "positions": {}
            }
            """;

        var graph = FeatureJsonConverter.ToDomainGraph(json, "X");
        var ctx = graph.BoundedContexts.Should().ContainSingle().Subject;

        ctx.CommandHandlers[0].Handles.Should().Equal("App.Cmd");
        ctx.CommandHandlerTargets[0].HandledBy.Should().Equal("App.H");
    }

    [Fact]
    public void BuildCSharpRegistrations_EmitsAddTransientLines()
    {
        var graph = new FeatureGraph
        {
            BoundedContexts =
            [
                new FeatureBoundedContext
                {
                    Name = "Demo",
                    CommandHandlers =
                    [
                        new FeatureHandler
                        {
                            Name = "H",
                            FullName = "Ns.H",
                            Handles = ["Ns.Cmd"],
                        },
                    ],
                    CommandHandlerTargets = [],
                },
            ],
        };

        var code = FeatureCommandRegistrationScaffold.BuildCSharpRegistrations(graph);
        code.Should().Contain("AddTransient<ICommandHandler<global::Ns.Cmd>, global::Ns.H>();");
    }

    [Fact]
    public void FeatureLlmImplementationPrompt_IncludesFullGraphJson()
    {
        var graph = new FeatureGraph
        {
            BoundedContexts =
            [
                new FeatureBoundedContext
                {
                    Name = "Checkout",
                    Aggregates =
                    [
                        new FeatureAggregate
                        {
                            Name = "Order",
                            FullName = "App.Order",
                            Properties =
                            [
                                new FeatureProperty { Name = "Id", TypeName = "Guid" },
                            ],
                        },
                    ],
                    Relationships =
                    [
                        new FeatureRelationship
                        {
                            SourceType = "App.Order",
                            TargetType = "App.OrderPlaced",
                            Kind = RelationshipKind.Emits,
                        },
                    ],
                },
            ],
        };

        var md = FeatureLlmImplementationPrompt.BuildMarkdown(graph);
        md.Should().Contain("Implement domain feature: Checkout");
        md.ToLowerInvariant().Should().Contain("\"fullname\": \"app.order\"");
        md.Should().Contain("| `App.Order` | Emits | `App.OrderPlaced` |");
    }

    [Fact]
    public void FeatureLlmImplementationPrompt_IncludesRawFeatureJsonWhenProvided()
    {
        const string raw = """{"readOnly":true,"nodes":[],"edges":[],"positions":{}}""";
        var graph = new FeatureGraph { BoundedContexts = [new FeatureBoundedContext { Name = "X" }] };

        var md = FeatureLlmImplementationPrompt.BuildMarkdown(graph, raw);
        md.Should().Contain("## Raw feature editor JSON");
        md.Should().Contain(raw);
    }
}
