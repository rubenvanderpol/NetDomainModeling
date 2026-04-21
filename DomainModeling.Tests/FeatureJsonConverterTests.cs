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
    public void ToDomainGraph_EntityAggregateValueObjectSubType_ParseMethodsRulesAndLayer()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "Custom.E",
                  "name": "E",
                  "kind": "entity",
                  "isCustom": true,
                  "layer": "Domain",
                  "props": [],
                  "methods": [
                    { "name": "Do", "returnTypeName": "void", "parameters": [ { "name": "x", "typeName": "int" } ] }
                  ],
                  "rules": [ { "name": "Inv1", "text": "Always valid" } ]
                },
                {
                  "id": "Custom.A",
                  "name": "A",
                  "kind": "aggregate",
                  "isCustom": true,
                  "layer": "Application",
                  "props": [],
                  "methods": [ "void Foo()" ],
                  "rules": [ "Must be consistent" ]
                },
                {
                  "id": "Custom.V",
                  "name": "V",
                  "kind": "valueObject",
                  "isCustom": true,
                  "layer": "Domain",
                  "props": [],
                  "methods": [],
                  "rules": []
                },
                {
                  "id": "Custom.S",
                  "name": "S",
                  "kind": "subType",
                  "isCustom": true,
                  "layer": "Infrastructure",
                  "props": [],
                  "methods": [ { "name": "Bar", "returnTypeName": "string", "parameters": [] } ],
                  "rules": []
                }
              ],
              "edges": [],
              "positions": {}
            }
            """;

        var graph = FeatureJsonConverter.ToDomainGraph(json, "F");
        var ctx = graph.BoundedContexts.Should().ContainSingle().Subject;

        var e = ctx.Entities.Should().ContainSingle().Subject;
        e.Layer.Should().Be("Domain");
        e.Methods.Should().ContainSingle().Which.Name.Should().Be("Do");
        e.Rules.Should().ContainSingle().Which.Text.Should().Be("Always valid");

        var a = ctx.Aggregates.Should().ContainSingle().Subject;
        a.Layer.Should().Be("Application");
        a.Methods.Should().ContainSingle().Which.Name.Should().Be("Foo");
        a.Rules.Should().ContainSingle().Which.Name.Should().Be("Rule");
        a.Rules[0].Text.Should().Be("Must be consistent");

        ctx.ValueObjects.Should().ContainSingle();
        var s = ctx.SubTypes.Should().ContainSingle().Subject;
        s.Layer.Should().Be("Infrastructure");
        s.IsCustom.Should().BeTrue();
        s.Methods.Should().ContainSingle().Which.Name.Should().Be("Bar");
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

    [Fact]
    public void ToDomainGraph_PersistsBoundedContextAndLayerOnEntity()
    {
        const string json = """
            {
              "nodes": [
                {
                  "id": "App.LineItem",
                  "name": "LineItem",
                  "kind": "entity",
                  "isCustom": false,
                  "boundedContext": "Catalog",
                  "layer": "Domain",
                  "props": []
                }
              ],
              "edges": [],
              "positions": {}
            }
            """;

        var g = FeatureJsonConverter.ToDomainGraph(json, "MyFeature");
        var e = g.BoundedContexts[0].Entities.Should().ContainSingle().Subject;
        e.FullName.Should().Be("App.LineItem");
        e.BoundedContextName.Should().Be("Catalog");
        e.Layer.Should().Be("Domain");
    }

    [Fact]
    public void FeatureGraphSliceEnrichment_FillsDescriptionAndBoundedContextFromFullGraph()
    {
        var full = new DomainGraph(
            new BoundedContextNode
            {
                Name = "Catalog",
                Entities =
                [
                    new EntityNode
                    {
                        Name = "Product",
                        FullName = "DomainModeling.Example.Domain.Product",
                        Description = "A sellable item in the catalog.",
                    },
                ],
            });

        const string featureJson = """
            {
              "nodes": [
                {
                  "id": "DomainModeling.Example.Domain.Product",
                  "name": "Product",
                  "kind": "entity",
                  "isCustom": false,
                  "props": []
                }
              ],
              "edges": [],
              "positions": {}
            }
            """;

        var slice = FeatureJsonConverter.ToDomainGraph(featureJson, "Pricing");
        FeatureGraphSliceEnrichment.Apply(slice, full);

        var entity = slice.BoundedContexts[0].Entities.Should().ContainSingle().Subject;
        entity.Description.Should().Be("A sellable item in the catalog.");
        entity.BoundedContextName.Should().Be("Catalog");

        var fg = FeatureGraph.FromDomainGraph(slice);
        var fe = fg.BoundedContexts[0].Entities.Should().ContainSingle().Subject;
        fe.Description.Should().Be("A sellable item in the catalog.");
        fe.BoundedContextName.Should().Be("Catalog");
        fg.BoundedContexts[0].ReferencedBoundedContextNames.Should().Equal("Catalog");
    }
}
