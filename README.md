# Domain Modeling

A small .NET library that **reflects over your assemblies** and builds a **domain graph**: DDD-style building blocks (aggregates, entities, handlers, events, repositories) and the **relationships between them**. You can **serialize the graph to JSON** for your own tooling or mount an **embedded web explorer** in ASP.NET Core so teams can navigate how the application is structured and how work flows through handlers and events.

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
| `DomainModeling.Workbench.*` | Optional: **.NET Aspire** app host, **Vite + TypeScript** SPA, and a small API that mounts `MapDomainModel` — see [Workbench (Aspire + Vite)](#workbench-aspire--vite) |

### Explorer UI source (TypeScript)

The interactive explorer (diagram, detail views, feature editor, etc.) is implemented in **TypeScript** under:

`DomainModeling.Workbench/DomainModeling.Workbench.Web/src/explorer/`

Layout:

| Folder | Purpose |
|--------|---------|
| `explorer/app/` | Main bootstrap and embed entry for the bundled script |
| `explorer/features/` | Diagram, feature editor, developer editor, testing, trace |
| `explorer/lib/` | Shared helpers and DOM utilities |
| `explorer/ui/` | Tabs and detail rendering |
| `explorer/styles/` | Explorer CSS (synced into `DomainModeling.AspNetCore/wwwroot/css` when the bundle is built) |
| `explorer/types/` | Ambient types for `window` globals used by inline handlers |

The library ships a single **`wwwroot/js/explorer-bundle.js`** (produced by **esbuild** from that tree). It is **checked in** so `dotnet build` works without Node.js. If you change explorer sources, run **`npm run build`** (or **`npm run build:explorer`**) from `DomainModeling.Workbench/DomainModeling.Workbench.Web` to regenerate the bundle and copied CSS. The `DomainModeling.AspNetCore` project can also run `npm ci && npm run build:explorer` automatically when the bundle file is missing.

TypeScript is checked with **`tsc -b`** (see `DomainModeling.Workbench/DomainModeling.Workbench.Web/tsconfig.json` and `tsconfig.explorer.json`).

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

If you omit conventions for a role, nothing is classified for that role (no implicit defaults).

### 3. Use the graph

- **`graph.ToJson()`** — camelCase JSON suitable for a custom dashboard or documentation pipeline.
- **`DomainGraph`** — inspect `BoundedContexts`, each with typed node lists and a flat `Relationships` list (`RelationshipKind`: contains, handles, emits, publishes, manages, references, etc.).

The scanner derives many edges from **metadata** (interfaces, properties, generic arguments on repositories). It also uses **IL inspection** where needed—for example domain event construction inside methods, integration events published from handlers, and aggregate method calls from command handlers—so the picture stays closer to real control flow than naming alone.

#### How links between types are detected

Relationships are edges in `DomainGraph.Relationships` with a `RelationshipKind`. Only types that match your **configured conventions** participate as nodes; edges are inferred as follows.

| Kind | Detection |
|------|-----------|
| **Contains** | An aggregate has a public instance property whose type (or collection element type) is a classified **entity**—those entities are treated as children of the aggregate. |
| **Has** / **HasMany** | On entities, aggregates, value objects, command-handler targets, and discovered **sub-types** (custom property types that are not framework primitives): a public instance property whose type is another **known domain type**, or a **custom** non-primitive type in your assemblies (not `System.*` / `Microsoft.*`), yields an edge labeled with the property name. Collections use **HasMany**; scalars use **Has**. |
| **ReferencesById** | If a property is named `{Something}Id` (suffix `Id`), has no object reference above, and `{Something}` matches the **name** of a classified entity or aggregate, an association to that type is inferred (foreign-key style). |
| **Handles** | For event, command, and query handlers: types taken from **generic arguments** of implemented interfaces (for example `IHandler<T>`), or—if none are found—from **public method parameters** that match known domain types. Event handlers also get **Handles** to command DTOs when IL shows **`new`** on those types. |
| **References** | **Command handlers** calling **instance methods** on classified aggregates (for example `order.Place()`), including inside async state machines. **Event handlers** calling instance methods on **command handler** types (for example dispatching to another handler). |
| **Manages** | **Repositories**: the aggregate type is taken from a **generic interface argument** on `IRepository<T>`-style interfaces that matches a classified aggregate. |
| **Emits** | **Domain events** constructed in entity/aggregate IL (`new` / calls resolved to event constructors), including compiler-generated nested types for async/lambdas. |
| **Publishes** | **Integration events** constructed in event-handler IL (same emission scan as domain events, applied to integration event types). |

Cross-references on event nodes (who emits or handles an event) are derived from these same rules. Command DTOs surfaced via `.Commands(...)` appear as nodes so **Handles** edges from handlers have endpoints even when nothing else references the type yet.

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
| GET | `{prefix}/assets/**` | UI assets (including `js/explorer-bundle.js`) |

With **developer view** enabled, a **PUT** `{prefix}/json` endpoint allows saving an edited graph from the browser. Optional **exports** appear under `{prefix}/exports` and `{prefix}/exports/{name}`.

At **runtime**, the embedded UI needs only the DLL and its embedded resources—**no database or Docker**. **Node.js** is **not** required to *run* the explorer from a built package; it **is** used in this repo to **author or rebuild** the TypeScript bundle (see [Explorer UI source](#explorer-ui-source-typescript)).

## Workbench (Aspire + Vite)

The **`DomainModeling.Workbench`** folder hosts an optional stack for local development toward a separate SPA + API:

| Piece | Role |
|--------|------|
| `DomainModeling.Workbench.AppHost` | .NET Aspire orchestration (`AddViteApp` for the frontend, reference to the API) |
| `DomainModeling.Workbench.Api` | ASP.NET Core API calling `MapDomainModel` (graph + feature editor endpoints); serves the built SPA under `/app` in development-style setups |
| `DomainModeling.Workbench.Web` | **Vite** + **TypeScript** SPA (`npm run dev` / `npm run build`) — proxies `/domain-model` to the API in dev |
| `DomainModeling.Workbench.ServiceDefaults` | Shared Aspire service defaults |

Run the full stack from the repository root:

```bash
dotnet run --project DomainModeling.Workbench/DomainModeling.Workbench.AppHost
```

Use the Aspire dashboard URLs from the console output. For frontend-only dev (after `npm install` in `DomainModeling.Workbench/DomainModeling.Workbench.Web`):

```bash
npm run dev --prefix DomainModeling.Workbench/DomainModeling.Workbench.Web
```

The workbench API currently builds its sample graph the same way as `DomainModeling.Example` (`ExampleDomainModelGraph`); it is a host for the SPA and endpoints, not a separate product backend yet.

## Build and test (this repository)

```bash
dotnet restore
dotnet build
dotnet test
```

If you change **explorer TypeScript** or **workbench web** sources, install Node.js **20+** and from `DomainModeling.Workbench/DomainModeling.Workbench.Web` run:

```bash
npm ci
npm run build
```

That runs `tsc`, builds the Vite SPA, and regenerates `DomainModeling.AspNetCore/wwwroot/js/explorer-bundle.js` plus synced CSS.

Run the sample host:

```bash
dotnet run --project DomainModeling.Example --urls "http://localhost:5000"
```

Then open `/domain-model` (the example also redirects `/` there).

## Modeling “application flow” in practice

Think of the graph as a **navigable map** of how work moves: command and query handlers link to the types they handle; handlers can link to aggregates when the scanner sees instance calls; domain and integration events connect emitters and handlers; repositories link to aggregates. **Multiple bounded contexts** are merged so **integration events** can show **cross-context** publishers and subscribers.

Tune your **conventions** so they match your real base classes and interfaces; split **Domain / Application / Infrastructure** assemblies when you want **layer** labels on nodes. In the ASP.NET Core explorer you can also attach **aliases and descriptions** per type via the metadata endpoints and store them on disk.
