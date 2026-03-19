using DomainModeling.AspNetCore;
using DomainModeling.Builder;
using DomainModeling.Example.Domain;
using DomainModeling.Example.Shipping.Domain;

var builder = WebApplication.CreateBuilder(args);

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
        .Repositories(r => r.Implements(typeof(IRepository<>)))
    )
    .WithSharedAssembly(typeof(IntegrationEvent).Assembly)
    .WithBoundedContext("Catalog", ctx => ctx
        .WithDomainAssembly(typeof(Product).Assembly)
    )
    .WithBoundedContext("Shipping", ctx => ctx
        .WithDomainAssembly(typeof(Shipment).Assembly)
    )
    .Build();

builder.Services.AddDomainModel(domainGraph);

// Register in-memory repositories
builder.Services.AddSingleton<IRepository<Order>, OrderRepository>();
builder.Services.AddSingleton<IRepository<Customer>, CustomerRepository>();
builder.Services.AddSingleton<IRepository<Product>, ProductRepository>();
builder.Services.AddSingleton<IRepository<Shipment>, ShipmentRepository>();
builder.Services.AddSingleton<IRepository<Carrier>, CarrierRepository>();

var app = builder.Build();

// Mount the domain model explorer UI at /domain-model (with developer editor and testing enabled)
app.MapDomainModel(domainGraph, configure: opts =>
{
    opts.EnableDeveloperView = true;
    opts.EnableFeatureEditor = true;
    opts.Testing.RepositoryInterfaceType = typeof(IRepository<>);
    opts.Testing.Repository(repo => repo
        .Add()
        .Update()
        .Delete());

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
});

app.MapGet("/", () => Results.Redirect("/domain-model"));
app.MapGet("/favicon.ico", () => Results.NoContent());

app.Run();
