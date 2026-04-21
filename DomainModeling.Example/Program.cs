using System.Reflection;
using DomainModeling.AspNetCore;
using DomainModeling.Builder;
using DomainModeling.Graph;
using DomainModeling.Example.Application;
using DomainModeling.Example.Domain;
using DomainModeling.Example.IntegrationEvents;
using DomainModeling.Example.Shipping.Domain;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

static Assembly SharedExampleAssembly(Assembly anyDomainAssembly) =>
    anyDomainAssembly.GetReferencedAssemblies()
        .Select(Assembly.Load)
        .First(a => string.Equals(a.GetName().Name, "DomainModeling.Example.Shared", StringComparison.Ordinal));

// Build the domain graph describing our bounded contexts
var domainGraph = DDDBuilder.Create(ctx => ctx
        .Entities(e => e.InheritsFrom<Entity>())
        .Aggregates(a => a.InheritsFrom<AggregateRoot>())
        .ValueObjects(v => v.InheritsFrom<ValueObject>())
        .DomainEvents(e => e.InheritsFrom<DomainEvent>())
        .IntegrationEvents(e => e.InheritsFrom<IntegrationEvent>())
        .EventHandlers(h => h
            .Implements(typeof(IEventHandler<>))
            .Implements(typeof(IIntegrationEventHandler<>)))
        .CommandHandlers(h => h.Implements(typeof(ICommandHandler<>)))
        .Commands(c => c.NameEndsWith("Command"))
        .Repositories(r => r.Implements(typeof(IRepository<>)))
    )
    .WithSharedAssembly(SharedExampleAssembly(typeof(Product).Assembly))
    .WithSharedAssembly(typeof(IntegrationEvent).Assembly, "IntegrationContracts")
    .WithBoundedContext("Catalog", ctx => ctx
        .WithDomainAssembly(typeof(Product).Assembly)
    )
    .WithBoundedContext("Shipping", ctx => ctx
        .WithDomainAssembly(typeof(Shipment).Assembly)
    )
    .Build();

builder.Services.AddDomainModel(domainGraph);
builder.Services.AddDomainModelTracing();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<ShipOrderCommandHandler>());

// Register in-memory repositories
builder.Services.AddSingleton<IRepository<Order>, OrderRepository>();
builder.Services.AddSingleton<IRepository<Customer>, CustomerRepository>();
builder.Services.AddSingleton<IRepository<Product>, ProductRepository>();
builder.Services.AddSingleton<IRepository<Shipment>, ShipmentRepository>();
builder.Services.AddSingleton<IRepository<Carrier>, CarrierRepository>();

var app = builder.Build();

UbiquitousLanguageDefinition ubiquitousLanguageDefinition = UbiquitousLanguageDefinition.CreateDefault();

// Mount the domain model explorer UI at /domain-model (with developer editor and testing enabled)
app.MapDomainModel(domainGraph, configure: opts =>
{
    opts.EnableDeveloperView = true;
    opts.EnableFeatureEditor = true;
    opts.EnableTraceView = true;
    opts.Testing.RepositoryInterfaceType = typeof(IRepository<>);
    opts.Testing.Repository(repo => repo
        .Add()
        .Update()
        .Delete());

    opts.UseUbiquitousLanguage(b => b
        .UseDefaultLanguage("en")
        .Language("nl", p => p
            .WithTitle("Ubiquit taal")
            .WithIntroduction("Gegenereerd uit het domeinmodel.")
            .WithMarkdownSectionAggregates("Aggregaten")
            .WithMarkdownSectionDomainEvents("Domeingebeurtenissen")
            .WithMarkdownBoundedContextHeadingFormat("Begrensd domein: {0}")
            .WithNoAggregatesInContext("Geen aggregaten in deze context.")
            .WithNoDomainEventsInContext("Geen domeingebeurtenissen in deze context.")
            .WithNoRelationsFromConcept("Geen relaties vanaf dit concept.")
            .WithMarkdownRelationsHeading("Relaties")
            .WithMarkdownTypePrefix("Type")
            .WithMarkdownRelationshipViaWord("via")
            .WithKindAggregate("aggregaat")
            .WithKindEntity("entiteit")
            .WithKindValueObject("waardeobject")
            .WithKindSubType("subtype")
            .WithRelationshipHas("heeft")
            .WithRelationshipHasMany("heeft veel")
            .WithRelationshipContains("bevat")
            .WithRelationshipReferences("verwijst naar")
            .WithRelationshipReferencesById("verwijst naar via id")));

    ubiquitousLanguageDefinition = opts.UbiquitousLanguage;

    opts.AddExport("Summary", "txt", graph =>
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var ctx in graph.BoundedContexts)
        {
            lines.Add($"Bounded Context: {ctx.Name}");
            lines.Add($"  Aggregates: {ctx.Aggregates.Count}");
            lines.Add($"  Entities: {ctx.Entities.Count}");
            lines.Add($"  Value Objects: {ctx.ValueObjects.Count}");
            lines.Add($"  Domain Events: {ctx.DomainEvents.Count}");
            lines.Add("");
        }
        return string.Join(Environment.NewLine, lines);
    });

    opts.AddExport("Ubiquitous Language", "md", graph =>
        UbiquitousLanguageMarkdownExport.Build(graph, ubiquitousLanguageDefinition, language: null));

    opts.AddFeatureExport("Summary", "md", graph =>
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var ctx in graph.BoundedContexts)
        {
            lines.Add($"# {ctx.Name}");
            foreach (var a in ctx.Aggregates)
            {
                var displayName = !string.IsNullOrWhiteSpace(a.Alias) ? a.Alias : a.Name;
                var desc = !string.IsNullOrWhiteSpace(a.Description) ? $" — {a.Description}" : "";
                lines.Add($"- **Aggregate**: {displayName}{desc}{(a.IsCustom ? " *(new)*" : "")}");
            }
            foreach (var e in ctx.Entities)
            {
                var displayName = !string.IsNullOrWhiteSpace(e.Alias) ? e.Alias : e.Name;
                var desc = !string.IsNullOrWhiteSpace(e.Description) ? $" — {e.Description}" : "";
                lines.Add($"- **Entity**: {displayName}{desc}{(e.IsCustom ? " *(new)*" : "")}");
            }
            foreach (var v in ctx.ValueObjects)
            {
                var displayName = !string.IsNullOrWhiteSpace(v.Alias) ? v.Alias : v.Name;
                var desc = !string.IsNullOrWhiteSpace(v.Description) ? $" — {v.Description}" : "";
                lines.Add($"- **Value Object**: {displayName}{desc}{(v.IsCustom ? " *(new)*" : "")}");
            }
            foreach (var ev in ctx.DomainEvents)
            {
                var displayName = !string.IsNullOrWhiteSpace(ev.Alias) ? ev.Alias : ev.Name;
                var desc = !string.IsNullOrWhiteSpace(ev.Description) ? $" — {ev.Description}" : "";
                lines.Add($"- **Event**: {displayName}{desc}{(ev.IsCustom ? " *(new)*" : "")}");
            }
            foreach (var h in ctx.EventHandlers)
            {
                var displayName = !string.IsNullOrWhiteSpace(h.Alias) ? h.Alias : h.Name;
                var desc = !string.IsNullOrWhiteSpace(h.Description) ? $" — {h.Description}" : "";
                lines.Add($"- **Handler**: {displayName}{desc}{(h.IsCustom ? " *(new)*" : "")}");
            }
            foreach (var h in ctx.CommandHandlers)
            {
                var displayName = !string.IsNullOrWhiteSpace(h.Alias) ? h.Alias : h.Name;
                var desc = !string.IsNullOrWhiteSpace(h.Description) ? $" — {h.Description}" : "";
                lines.Add($"- **Handler**: {displayName}{desc}{(h.IsCustom ? " *(new)*" : "")}");
            }
            lines.Add("");
        }
        return string.Join(Environment.NewLine, lines);
    });

    opts.AddFeatureExport("LLM implementation prompt", "md", (graph, ctx) =>
        FeatureLlmImplementationPrompt.BuildMarkdown(graph, ctx.RawFeatureEditorJson));
});

// Demo: push a sample trace row to the Trace tab (POST with empty body)
app.MapPost("/domain-model/trace/demo", async (IServiceProvider sp) =>
{
    await DomainModelTracing.NotifyAsync(sp, typeof(OrderPlacedEvent), new { demo = true, at = DateTime.UtcNow });
    return Results.Ok(new { traced = typeof(OrderPlacedEvent).FullName });
});

app.MapGet("/", () => Results.Redirect("/domain-model"));
app.MapGet("/favicon.ico", () => Results.NoContent());

app.MapPost("/api/orders/{orderId:guid}/ship", async (Guid orderId, ISender mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new ShipOrderCommand(orderId), ct).ConfigureAwait(false);
    return result.Success
        ? Results.Ok(new { orderId, shipped = true })
        : Results.NotFound(new { error = result.Error });
});

app.Run();
