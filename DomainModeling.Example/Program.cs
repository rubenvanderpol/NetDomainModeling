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
                lines.Add($"- **Aggregate**: {a.Name}{(a.IsCustom ? " *(new)*" : "")}");
            foreach (var e in ctx.Entities)
                lines.Add($"- **Entity**: {e.Name}{(e.IsCustom ? " *(new)*" : "")}");
            foreach (var v in ctx.ValueObjects)
                lines.Add($"- **Value Object**: {v.Name}{(v.IsCustom ? " *(new)*" : "")}");
            foreach (var ev in ctx.DomainEvents)
                lines.Add($"- **Event**: {ev.Name}{(ev.IsCustom ? " *(new)*" : "")}");
            foreach (var h in ctx.EventHandlers)
                lines.Add($"- **Handler**: {h.Name}{(h.IsCustom ? " *(new)*" : "")}");
            foreach (var h in ctx.CommandHandlers)
                lines.Add($"- **Handler**: {h.Name}{(h.IsCustom ? " *(new)*" : "")}");
            lines.Add("");
        }
        return string.Join(Environment.NewLine, lines);
    });
});

app.MapGet("/", () => Results.Redirect("/domain-model"));
app.MapGet("/favicon.ico", () => Results.NoContent());

app.Run();
