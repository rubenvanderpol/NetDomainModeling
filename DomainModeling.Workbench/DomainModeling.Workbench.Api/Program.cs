using DomainModeling.AspNetCore;
using DomainModeling.Example;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var domainGraph = ExampleDomainModelGraph.Create();
builder.Services.AddDomainModel(domainGraph);

var app = builder.Build();

app.UseExceptionHandler();

var contentRoot = app.Environment.ContentRootPath;
var featureDir = Path.Combine(contentRoot, "features");
var metadataDir = Path.Combine(contentRoot, "metadata");
var diagramLayoutDir = Path.Combine(contentRoot, "diagram-layout");

app.MapDomainModel(domainGraph, configure: opts =>
{
    opts.EnableDeveloperView = true;
    opts.EnableFeatureEditor = true;
    opts.FeatureStoragePath = featureDir;
    opts.MetadataStoragePath = metadataDir;
    opts.DiagramLayoutStoragePath = diagramLayoutDir;

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

var appIndexPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "app", "index.html");
if (File.Exists(appIndexPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            if (string.Equals(ctx.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.Append(HeaderNames.CacheControl, "no-cache, no-store, must-revalidate");
            }
        },
    });

    async Task ServeWorkbenchSpa(HttpContext ctx)
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.Headers.Append(HeaderNames.CacheControl, "no-cache, no-store, must-revalidate");
        await ctx.Response.SendFileAsync(appIndexPath).ConfigureAwait(false);
    }

    app.MapGet("/app", () => Results.Redirect("/app/"));
    app.MapMethods("/app/", ["GET", "HEAD"], ServeWorkbenchSpa).ExcludeFromDescription();
    app.MapMethods("/app/features", ["GET", "HEAD"], ServeWorkbenchSpa).ExcludeFromDescription();
}

app.MapGet("/", () => Results.Redirect("/app/"));

app.MapDefaultEndpoints();

app.Run();
