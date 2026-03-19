## Cursor Cloud specific instructions

### Project overview
DDD Domain Modeling visualization toolkit for .NET. A library that scans .NET assemblies to discover DDD building blocks and serves an interactive web-based explorer UI via ASP.NET Core. No external dependencies (no database, Docker, or Node.js required).

### Prerequisites
- .NET 10 SDK (all projects target `net10.0`). Installed via the `dotnet-install.sh` script to `$HOME/.dotnet`.
- `DOTNET_ROOT` and `PATH` must include `$HOME/.dotnet` (configured in `~/.bashrc`).

### Key commands
- **Restore**: `dotnet restore`
- **Build**: `dotnet build`
- **Test**: `dotnet test` (38 xUnit tests in `DomainModeling.Tests`)
- **Run example app**: `dotnet run --project DomainModeling.Example --urls "http://localhost:5000"`
  - UI served at `/domain-model`
  - Root `/` redirects to `/domain-model`
  - Domain graph JSON at `/domain-model/json`
  - Exports at `/domain-model/exports/Summary`

### Solution structure (6 projects)
| Project | Type |
|---|---|
| `DomainModeling/` | Core library (builder API, scanner, graph model) |
| `DomainModeling.AspNetCore/` | ASP.NET Core integration (endpoints, embedded UI) |
| `DomainModeling.Example/` | Runnable example web app |
| `DomainModeling.Example.Shared/` | Shared DDD base classes |
| `DomainModeling.Example.Shipping/` | Shipping bounded context |
| `DomainModeling.Tests/` | xUnit test project |

### Gotchas
- The solution uses the `.slnx` format (XML-based solution format); `dotnet build` at the workspace root picks it up automatically.
- Build produces CS0436 warnings (type conflicts between `DomainModeling.Example` and `DomainModeling.Example.Shared` for `Money`/`Address`/`EmailAddress`). These are pre-existing and expected.
- No linter is configured beyond the C# compiler warnings. `dotnet build` is the lint check.
