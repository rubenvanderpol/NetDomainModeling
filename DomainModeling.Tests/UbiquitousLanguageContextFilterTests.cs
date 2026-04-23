using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class UbiquitousLanguageContextFilterTests
{
    [Fact]
    public void Build_WithBoundedContextNames_ReturnsOnlyThoseContexts()
    {
        var graph = new DomainGraph(
            new BoundedContextNode { Name = "A" },
            new BoundedContextNode { Name = "B" });

        var doc = UbiquitousLanguageDocumentBuilder.Build(
            graph,
            UbiquitousLanguageDefinition.CreateDefault(),
            language: null,
            boundedContextNames: ["B"]);

        doc.BoundedContexts.Should().ContainSingle().Which.Name.Should().Be("B");
        doc.FilteredToBoundedContexts.Should().Equal("B");
    }

    [Fact]
    public void Markdown_Build_WithFilter_MatchesDocument()
    {
        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "X",
                Aggregates = [new AggregateNode { Name = "Root", FullName = "X.Root" }],
            },
            new BoundedContextNode
            {
                Name = "Y",
                Aggregates = [new AggregateNode { Name = "Other", FullName = "Y.Other" }],
            });

        var def = UbiquitousLanguageDefinition.CreateDefault();
        var md = UbiquitousLanguageMarkdownExport.Build(graph, def, language: null, boundedContextNames: ["X"]);

        md.Should().Contain("Bounded context: X");
        md.Should().NotContain("Bounded context: Y");
    }
}
