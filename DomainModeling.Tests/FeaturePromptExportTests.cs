using System.Reflection;
using DomainModeling.AspNetCore;
using DomainModeling.Graph;
using FluentAssertions;

namespace DomainModeling.Tests;

public class FeaturePromptExportTests
{
    [Fact]
    public void AddFeaturePromptExport_RegistersPromptExport_WithConfiguredNameAndExtension()
    {
        var options = new DomainModelOptions();

        options.AddFeaturePromptExport("AI Prompt", "txt");

        var registration = GetSingleFeatureExportRegistration(options);
        registration.Name.Should().Be("AI Prompt");
        registration.FileExtension.Should().Be("txt");
        registration.Builder.Should().NotBeNull();
    }

    [Fact]
    public void AddFeaturePromptExport_GeneratesPrompt_WithFeatureDetails()
    {
        var options = new DomainModelOptions();
        options.AddFeaturePromptExport(additionalInstructions: "Prefer command handlers.");

        var registration = GetSingleFeatureExportRegistration(options);
        var prompt = registration.Builder(new FeatureGraph
        {
            BoundedContexts =
            [
                new FeatureBoundedContext
                {
                    Name = "Catalog",
                    Aggregates = [new FeatureAggregate { Name = "Product", IsCustom = true, Description = "Main aggregate root." }],
                    Entities = [new FeatureEntity { Name = "ProductVariant" }],
                    DomainEvents = [new FeatureDomainEvent { Name = "ProductCreated" }],
                    CommandHandlers = [new FeatureHandler { Name = "CreateProductHandler" }],
                    Relationships =
                    [
                        new FeatureRelationship
                        {
                            SourceType = "Product",
                            TargetType = "ProductVariant",
                            Kind = RelationshipKind.Composition,
                            Label = "contains"
                        }
                    ]
                }
            ]
        });

        prompt.Should().Contain("## Additional Instructions");
        prompt.Should().Contain("Prefer command handlers.");
        prompt.Should().Contain("## Bounded Context: Catalog");
        prompt.Should().Contain("### Aggregates");
        prompt.Should().Contain("- Aggregate: Product [custom] — Main aggregate root.");
        prompt.Should().Contain("### Entities");
        prompt.Should().Contain("- Entity: ProductVariant");
        prompt.Should().Contain("### Domain Events");
        prompt.Should().Contain("- Domain Event: ProductCreated");
        prompt.Should().Contain("### Command Handlers");
        prompt.Should().Contain("- Command Handler: CreateProductHandler");
        prompt.Should().Contain("### Relationships");
        prompt.Should().Contain("- Product --[Composition: contains]--> ProductVariant");
    }

    private static FeatureExportReflectionView GetSingleFeatureExportRegistration(DomainModelOptions options)
    {
        var featureExportsProperty = typeof(DomainModelOptions).GetProperty(
            "FeatureExports",
            BindingFlags.Instance | BindingFlags.NonPublic);

        featureExportsProperty.Should().NotBeNull("DomainModelOptions should keep internal feature export registrations");
        var registrations = featureExportsProperty!.GetValue(options) as System.Collections.IEnumerable;
        registrations.Should().NotBeNull();

        var registration = registrations!.Cast<object>().Should().ContainSingle().Subject;
        var type = registration.GetType();
        var name = type.GetProperty("Name")!.GetValue(registration) as string;
        var extension = type.GetProperty("FileExtension")!.GetValue(registration) as string;
        var builder = type.GetProperty("Builder")!.GetValue(registration) as Func<FeatureGraph, string>;

        return new FeatureExportReflectionView(
            name ?? string.Empty,
            extension ?? string.Empty,
            builder ?? throw new InvalidOperationException("Feature export builder was not set."));
    }

    private sealed record FeatureExportReflectionView(
        string Name,
        string FileExtension,
        Func<FeatureGraph, string> Builder);
}
