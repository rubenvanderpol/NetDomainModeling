using DomainModeling.Graph;
using FluentAssertions;
using Xunit;

namespace DomainModeling.Tests;

public class UbiquitousLanguageDefinitionTests
{
    [Fact]
    public void Builder_AddsLanguage_AndDefaultResolvesPhrases()
    {
        var def = UbiquitousLanguageDefinitionBuilder.Create()
            .UseDefaultLanguage("nl")
            .Language("nl", p => p.WithTitle("NL titel").WithRelationshipHas("bezit"))
            .Build();

        def.DefaultLanguage.Should().Be("nl");
        var phrases = def.ResolvePhrases(null);
        phrases.Title.Should().Be("NL titel");
        phrases.RelationshipHas.Should().Be("bezit");
    }

    [Fact]
    public void DocumentBuilder_UsesPhrasesForRelationAndSections()
    {
        var def = UbiquitousLanguageDefinitionBuilder.Create()
            .Language("de", p => p
                .WithMarkdownSectionAggregates("Aggregate")
                .WithMarkdownSectionDomainEvents("Ereignisse")
                .WithRelationshipHas("hat"))
            .Build();

        var graph = new DomainGraph(
            new BoundedContextNode
            {
                Name = "X",
                Aggregates =
                [
                    new AggregateNode { Name = "A", FullName = "X.A" },
                ],
                ValueObjects =
                [
                    new ValueObjectNode { Name = "V", FullName = "X.V" },
                ],
                Relationships =
                [
                    new Relationship { SourceType = "X.A", TargetType = "X.V", Kind = RelationshipKind.Has },
                ],
            });

        var doc = UbiquitousLanguageDocumentBuilder.Build(graph, def, "de");
        doc.Language.Should().Be("de");
        doc.AvailableLanguages.Should().Contain("de");
        doc.AggregatesSectionLabel.Should().Be("Aggregate");
        doc.DomainEventsSectionLabel.Should().Be("Ereignisse");
        doc.BoundedContexts[0].Aggregates.Roots[0].Relations.Items.Should().ContainSingle()
            .Which.Phrase.Should().Be("hat");
    }
}
