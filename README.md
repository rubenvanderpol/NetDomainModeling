# Domain Modeling

A small .NET library that **reflects over your assemblies** and builds a **domain graph**: DDD-style building blocks (aggregates, entities, handlers, events, repositories), the **relationships between them**, and optional **XML documentation** text. You can **serialize the graph to JSON** for your own tooling or mount an **embedded web explorer** in ASP.NET Core so teams can navigate how the application is structured and how work flows through handlers and events.

The `DomainModeling.Example` project and `DomainModeling.Example.*` assemblies exist to **exercise and demonstrate** the scanner and UI. **Treat them as a sample host**, not as a framework you must copy verbatim—point the builder at **your** domain, application, and infrastructure assemblies instead.

## Requirements

- **.NET 10** (`net10.0`). All projects in this repository target it.

## Solution layout

| Project | Role |
|--------|------|
| `DomainModeling` | Core: fluent builder, reflection scanner, `DomainGraph` model and JSON serialization |
| `DomainModeling.AspNetCore` | ASP.NET Core integration: endpoints + embedded explorer UI (static assets as embedded resources) |
| `DomainModeling.Example*` | Runnable sample app and fake bounded contexts for tests |
| `DomainModeling.Tests` | Unit tests |

## Add to your application

### 1. Reference the projects

Add project references (or publish these projects as packages and reference those) to:

- `DomainModeling` — always, if you only need the graph in code or your own API.
- `DomainModeling.AspNetCore` — when you want the interactive UI and built-in JSON routes.

`DomainModeling.AspNetCore` pulls in `Microsoft.AspNetCore.App` and references `DomainModeling`.

### 2. Describe your bounded contexts and conventions

Use `DDDBuilder` to name each bounded context, attach the assemblies that contain your types, and define **how** each DDD role is recognized (base types, interfaces, naming, attributes, etc.).

```csharp
using System.Reflection;
using DomainModeling.Builder;

var graph = DDDBuilder.Create(ctx => ctx
        .Entities(e => e.InheritsFrom<MyEntityBase>())
        .Aggregates(a => a.InheritsFrom<MyAggregateRoot>())
        .ValueObjects(v => v.InheritsFrom<MyValueObjectBase>())
        .DomainEvents(e => e.InheritsFrom<MyDomainEventBase>())
        .IntegrationEvents(e => e.InheritsFrom<MyIntegrationEventBase>())
        .EventHandlers(h => h.Implements(typeof(IMyDomainEventHandler<>)))
        .CommandHandlers(h => h.Implements(typeof(IMyCommandHandler<>)))
        .Commands(c => c.NameEndsWith("Command")) // optional: surfaces command DTOs as targets
        .QueryHandlers(h => h.Implements(typeof(IMyQueryHandler<>)))
        .Repositories(r => r.Implements(typeof(IRepository<>)))
        .DomainServices(s => s.InheritsFrom<MyDomainServiceBase>())
    )
    .WithBoundedContext("Orders", ctx => ctx
        .WithDomainAssembly(typeof(Order).Assembly)
        .WithApplicationAssembly(typeof(PlaceOrderHandler).Assembly)
        .WithInfrastructureAssembly(typeof(OrderRepository).Assembly))
    .WithBoundedContext("Billing", ctx => ctx
        .WithDomainAssembly(typeof(Invoice).Assembly)
        .WithApplicationAssembly(typeof(IssueInvoiceHandler).Assembly))
    .Build();
```

Useful APIs:

- **`WithSharedAssembly`** on `DDDBuilder` — assemblies scanned in **every** context (for shared integration contracts).
- **`WithAssembly`** on `BoundedContextBuilder` — extra assemblies for a single context.
- **`WithDocumentation`** — control XML doc loading (`AutoDiscover()`, explicit `.xml` paths) so summaries appear on graph nodes.

If you omit conventions for a role, nothing is classified for that role (no implicit defaults).

### 3. Use the graph

- **`graph.ToJson()`** — camelCase JSON suitable for a custom dashboard or documentation pipeline.
- **`DomainGraph`** — inspect `BoundedContexts`, each with typed node lists and a flat `Relationships` list (`RelationshipKind`: contains, handles, emits, publishes, manages, references, etc.).

The scanner derives many edges from **metadata** (interfaces, properties, generic arguments on repositories). It also uses **IL inspection** where needed—for example domain event construction inside methods, integration events published from handlers, and aggregate method calls from command handlers—so the picture stays closer to real control flow than naming alone.

### 4. Optional: ASP.NET Core explorer

```csharp
using DomainModeling.AspNetCore;

builder.Services.AddDomainModel(graph);

app.MapDomainModel(graph, routePrefix: "/domain-model", configure: opts =>
{
    opts.EnableDeveloperView = false;   // optional: edit visibility / save JSON subset
    opts.EnableFeatureEditor = false;    // optional: feature diagrams + disk-backed JSON
    opts.AddExport("Summary", "txt", g => /* build string from g */);
});
```

Default routes (prefix `/domain-model` unless you change it):

| Method | Path | Purpose |
|--------|------|--------|
| GET | `{prefix}` | Interactive explorer |
| GET | `{prefix}/json` | Full domain graph JSON |
| GET | `{prefix}/assets/**` | UI assets |

With **developer view** enabled, a **PUT** `{prefix}/json` endpoint allows saving an edited graph from the browser. Optional **exports** appear under `{prefix}/exports` and `{prefix}/exports/{name}`.

There is **no database, Docker, or Node.js** dependency for the UI; assets ship inside `DomainModeling.AspNetCore`.

## Build and test (this repository)

```bash
dotnet restore
dotnet build
dotnet test
```

Run the sample host:

```bash
dotnet run --project DomainModeling.Example --urls "http://localhost:5000"
```

Then open `/domain-model` (the example also redirects `/` there).

## Modeling “application flow” in practice

Think of the graph as a **navigable map** of how work moves: command and query handlers link to the types they handle; handlers can link to aggregates when the scanner sees instance calls; domain and integration events connect emitters and handlers; repositories link to aggregates. **Multiple bounded contexts** are merged so **integration events** can show **cross-context** publishers and subscribers.

Tune your **conventions** so they match your real base classes and interfaces; split **Domain / Application / Infrastructure** assemblies when you want **layer** labels on nodes. For richer cards in the UI, enable **XML documentation** in your modeled projects and use `WithDocumentation`.
