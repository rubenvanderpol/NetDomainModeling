using DomainModeling.Graph;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;
using System.Text;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Options for the Domain Model explorer UI.
/// </summary>
public sealed class DomainModelOptions
{
    /// <summary>
    /// When <c>true</c>, an additional "Editor" tab is shown that allows developers
    /// to view the domain graph with visibility controls (hide/show items and types),
    /// select bounded contexts, and download the visible subset as JSON or SVG.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool EnableDeveloperView { get; set; }

    /// <summary>
    /// this is another feature, should not yet be avaialable yet
    /// </summary>
    public bool EnableTestingView { get; } = false;

    /// <summary>
    /// When <c>true</c>, an additional "Features" tab is shown that provides a visual
    /// editor for creating feature diagrams. Users can add domain types, draw
    /// relationships by dragging lines between nodes, and save/load named features.
    /// Feature configurations are stored as JSON files on disk.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool EnableFeatureEditor { get; set; }

    /// <summary>
    /// Directory path where feature editor JSON files are stored.
    /// Defaults to <c>./features</c> relative to the application root.
    /// </summary>
    public string FeatureStoragePath { get; set; } = "./features";

    /// <summary>
    /// Directory path where domain type alias/description metadata is stored.
    /// Bounded-context UI selections for the explorer are stored under <c>{MetadataStoragePath}/ui/bounded-contexts.json</c>.
    /// Defaults to <c>./metadata</c> relative to the application root.
    /// </summary>
    public string MetadataStoragePath { get; set; } = "./metadata";

    /// <summary>
    /// Configuration for the aggregate testing feature.
    /// </summary>
    public DomainModelTestingOptions Testing { get; } = new();

    internal List<ExportRegistration> Exports { get; } = [];

    internal List<FeatureExportRegistration> FeatureExports { get; } = [];

    /// <summary>
    /// Registers a named export that produces a downloadable string from the domain graph.
    /// <para>
    /// Example:
    /// <code>
    /// opts.AddExport("Ubiquitous Language", "md", graph => BuildMarkdown(graph));
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="name">Display name shown in the UI.</param>
    /// <param name="fileExtension">File extension for the download (without leading dot).</param>
    /// <param name="builder">Function that receives the <see cref="DomainGraph"/> and returns the export content.</param>
    public DomainModelOptions AddExport(string name, string fileExtension, Func<DomainGraph, string> builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);
        ArgumentNullException.ThrowIfNull(builder);
        Exports.Add(new ExportRegistration(name, fileExtension.TrimStart('.'), builder));
        return this;
    }

    /// <summary>
    /// Registers a named export for feature editor diagrams.
    /// The builder receives a <see cref="FeatureGraph"/> containing a single bounded context
    /// with the feature's types and relationships. Types created in the editor have
    /// <c>IsCustom = true</c>.
    /// <para>
    /// Example:
    /// <code>
    /// opts.AddFeatureExport("Feature Summary", "md", graph => BuildMarkdown(graph));
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="name">Display name shown in the UI.</param>
    /// <param name="fileExtension">File extension for the download (without leading dot).</param>
    /// <param name="builder">Function that receives a <see cref="FeatureGraph"/> and returns the export content.</param>
    /// <remarks>
    /// Download requests may include <c>?registerCommands=true</c> to append a Markdown section with
    /// C# DI registration scaffolds for command handlers (see <see cref="FeatureCommandRegistrationScaffold"/>).
    /// </remarks>
    public DomainModelOptions AddFeatureExport(string name, string fileExtension, Func<FeatureGraph, string> builder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileExtension);
        ArgumentNullException.ThrowIfNull(builder);
        FeatureExports.Add(new FeatureExportRegistration(name, fileExtension.TrimStart('.'), builder));
        return this;
    }
}

/// <summary>
/// A named export that produces a downloadable string from the domain graph.
/// </summary>
/// <param name="Name">Display name shown in the UI.</param>
/// <param name="FileExtension">File extension (without dot).</param>
/// <param name="Builder">Function that produces the export content.</param>
internal sealed record ExportRegistration(string Name, string FileExtension, Func<DomainGraph, string> Builder);

/// <summary>
/// A named export for feature editor diagrams.
/// </summary>
internal sealed record FeatureExportRegistration(string Name, string FileExtension, Func<FeatureGraph, string> Builder);

/// <summary>
/// Extension methods to mount the Domain Model explorer UI and JSON API
/// on an ASP.NET Core application — similar to how Scalar serves OpenAPI docs.
/// </summary>
public static class DomainModelEndpointExtensions
{
    /// <summary>
    /// Maps the domain model JSON API and interactive explorer UI.
    /// <para>
    /// <c>GET {routePrefix}</c> — serves the interactive HTML explorer.<br/>
    /// <c>GET {routePrefix}/json</c> — returns the raw domain graph JSON.<br/>
    /// <c>GET {routePrefix}/assets/**</c> — serves CSS and JS modules.
    /// </para>
    /// When <see cref="DomainModelOptions.EnableDeveloperView"/> is <c>true</c>,
    /// an additional <c>PUT {routePrefix}/json</c> endpoint is registered to accept
    /// edited domain graphs from the browser.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder (e.g. <c>app</c>).</param>
    /// <param name="graph">A pre-built <see cref="DomainGraph"/> instance.</param>
    /// <param name="routePrefix">The URL prefix. Defaults to <c>/domain-model</c>.</param>
    /// <param name="configure">Optional configuration callback for <see cref="DomainModelOptions"/>.</param>
    public static IEndpointRouteBuilder MapDomainModel(
        this IEndpointRouteBuilder endpoints,
        DomainGraph graph,
        string routePrefix = "/domain-model",
        Action<DomainModelOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var options = new DomainModelOptions();
        configure?.Invoke(options);

        // Normalize prefix
        routePrefix = routePrefix.TrimEnd('/');

        // Cache the JSON — mutable so it can be updated when developer view saves
        var json = graph.ToJson();

        // Custom metadata store (alias / description per type) persisted as JSON files on disk
        var metadataDir = Path.GetFullPath(options.MetadataStoragePath);
        var metadata = new System.Collections.Concurrent.ConcurrentDictionary<string, TypeMetadata>();
        if (Directory.Exists(metadataDir))
        {
            foreach (var file in Directory.GetFiles(metadataDir, "*.json"))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (!TryDecodeMetadataFileName(fileName, out var fullName))
                    continue;

                var content = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<TypeMetadata>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (entry is null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.Alias) && string.IsNullOrWhiteSpace(entry.Description))
                    continue;

                metadata[fullName] = entry;
            }
        }

        var uiStateJsonDir = Path.Combine(metadataDir, "ui");
        var uiBoundedContextsPath = Path.Combine(uiStateJsonDir, "bounded-contexts.json");
        var uiBcJsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        BoundedContextUiSelections LoadUiBoundedContextSelectionsFromDisk()
        {
            try
            {
                if (File.Exists(uiBoundedContextsPath))
                {
                    var text = File.ReadAllText(uiBoundedContextsPath);
                    var parsed = JsonSerializer.Deserialize<BoundedContextUiSelections>(text, uiBcJsonOpts);
                    return NormalizeBoundedContextUiSelections(parsed, graph);
                }
            }
            catch
            {
                // Corrupt or unreadable file — fall through to defaults
            }

            return CreateDefaultBoundedContextUiSelections(graph);
        }

        var uiBoundedContextSelections = LoadUiBoundedContextSelectionsFromDisk();
        var uiBcSync = new object();

        var assetsPrefix = $"{routePrefix}/assets";

        // GET /domain-model/json — raw JSON
        endpoints.MapGet($"{routePrefix}/json", () => Results.Content(json, "application/json"))
            .ExcludeFromDescription()
            .WithName("DomainModelJson");

        // GET /domain-model/metadata — return all custom type metadata
        endpoints.MapGet($"{routePrefix}/metadata", () =>
            Results.Json(metadata, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            }))
            .ExcludeFromDescription()
            .WithName("DomainModelMetadata");

        // GET /domain-model/exports — list available exports
        if (options.Exports.Count > 0)
        {
            endpoints.MapGet($"{routePrefix}/exports", () =>
                Results.Json(options.Exports.Select(e => new { name = e.Name, extension = e.FileExtension })))
                .ExcludeFromDescription()
                .WithName("DomainModelExports");

            // GET /domain-model/exports/{name} — download a specific export
            endpoints.MapGet($"{routePrefix}/exports/{{name}}", (string name) =>
            {
                var export = options.Exports.FirstOrDefault(e =>
                    string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                if (export is null)
                    return Results.NotFound();

                var content = export.Builder(graph);
                var fileName = $"{export.Name.ToLowerInvariant().Replace(' ', '-')}.{export.FileExtension}";
                return Results.File(
                    System.Text.Encoding.UTF8.GetBytes(content),
                    "application/octet-stream",
                    fileName);
            })
            .ExcludeFromDescription()
            .WithName("DomainModelExportDownload");
        }

        // GET /domain-model/ui/bounded-contexts — explorer / diagram / feature palette context selection
        endpoints.MapGet($"{routePrefix}/ui/bounded-contexts", () =>
        {
            lock (uiBcSync)
            {
                var normalized = NormalizeBoundedContextUiSelections(uiBoundedContextSelections, graph);
                return Results.Json(normalized, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
            }
        })
        .ExcludeFromDescription()
        .WithName("DomainModelUiBoundedContexts");

        // PUT /domain-model/ui/bounded-contexts — persist selection (same folder as type metadata)
        endpoints.MapPut($"{routePrefix}/ui/bounded-contexts", async (HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            BoundedContextUiSelections? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<BoundedContextUiSelections>(body, uiBcJsonOpts);
            }
            catch (JsonException)
            {
                return Results.BadRequest("Invalid JSON");
            }

            if (parsed is null)
                return Results.BadRequest("Invalid body");

            var normalized = NormalizeBoundedContextUiSelections(parsed, graph);
            string serialized;
            lock (uiBcSync)
            {
                uiBoundedContextSelections = normalized;
                serialized = JsonSerializer.Serialize(normalized, uiBcJsonOpts);
            }

            Directory.CreateDirectory(uiStateJsonDir);
            await File.WriteAllTextAsync(uiBoundedContextsPath, serialized);

            return Results.Ok(new { saved = true });
        })
        .ExcludeFromDescription()
        .WithName("DomainModelUiBoundedContextsUpdate");

        // PUT /domain-model/metadata/{fullName} — update alias/description for a type
        endpoints.MapPut($"{routePrefix}/metadata/{{**fullName}}", async (string fullName, HttpContext ctx) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();
            var entry = JsonSerializer.Deserialize<TypeMetadata>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (entry is null)
                return Results.BadRequest("Invalid metadata");

            var safeFileName = EncodeMetadataFileName(fullName);
            var path = Path.Combine(metadataDir, safeFileName + ".json");

            if (string.IsNullOrWhiteSpace(entry.Alias) && string.IsNullOrWhiteSpace(entry.Description))
            {
                metadata.TryRemove(fullName, out _);
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                metadata[fullName] = entry;
                Directory.CreateDirectory(metadataDir);

                var toWrite = JsonSerializer.Serialize(entry, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                });
                await File.WriteAllTextAsync(path, toWrite);
            }

            return Results.Ok(new { saved = true });
        })
        .ExcludeFromDescription()
        .WithName("DomainModelMetadataUpdate");

        // GET /domain-model — interactive HTML UI
        endpoints.MapGet(routePrefix, (HttpContext ctx) =>
        {
            var html = GetEmbeddedHtml(routePrefix, assetsPrefix, options);
            return Results.Content(html, "text/html");
        })
        .ExcludeFromDescription()
        .WithName("DomainModelExplorer");

        // GET /domain-model/assets/{**path} — serve CSS/JS from embedded resources
        endpoints.MapGet($"{assetsPrefix}/{{**path}}", (string path) =>
        {
            var content = GetEmbeddedAsset(path);
            if (content is null)
                return Results.NotFound();

            var contentType = path switch
            {
                _ when path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) => "text/css",
                _ when path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) => "text/javascript",
                _ => "application/octet-stream"
            };

            return Results.Content(content, contentType);
        })
        .ExcludeFromDescription();

        // Testing-mode endpoints
        if (options.EnableTestingView)
        {
            var testingService = new AggregateTestingService(graph, options.Testing);

            // GET /domain-model/testing/aggregates — aggregate metadata for the testing UI
            endpoints.MapGet($"{routePrefix}/testing/aggregates", () =>
                Results.Json(testingService.GetAggregateMetadata()))
                .ExcludeFromDescription();

            // POST /domain-model/testing/create — create an aggregate instance
            endpoints.MapPost($"{routePrefix}/testing/create", async (HttpContext ctx) =>
            {
                try
                {
                    using var reader = new StreamReader(ctx.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    var request = JsonDocument.Parse(body).RootElement;

                    var typeFullName = request.GetProperty("typeFullName").GetString()!;
                    var factoryMethod = request.TryGetProperty("factoryMethod", out var fm)
                                       && fm.ValueKind == JsonValueKind.String
                        ? fm.GetString()
                        : null;
                    JsonElement? parameters = request.TryGetProperty("parameters", out var p)
                                              && p.ValueKind != JsonValueKind.Null
                        ? p
                        : null;

                    var instance = testingService.CreateInstance(
                        typeFullName, parameters, factoryMethod, ctx.RequestServices);
                    return Results.Json(instance);
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 400);
                }
            })
            .ExcludeFromDescription();

            // GET /domain-model/testing/instances — list all stored instances
            endpoints.MapGet($"{routePrefix}/testing/instances", () =>
                Results.Json(testingService.GetInstances()))
                .ExcludeFromDescription();

            // PUT /domain-model/testing/instances/{id} — update a stored instance
            endpoints.MapPut($"{routePrefix}/testing/instances/{{id}}", async (string id, HttpContext ctx) =>
            {
                try
                {
                    using var reader = new StreamReader(ctx.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    var request = JsonDocument.Parse(body).RootElement;

                    JsonElement? parameters = request.TryGetProperty("parameters", out var p)
                                              && p.ValueKind != JsonValueKind.Null
                        ? p
                        : null;

                    var instance = testingService.UpdateInstance(id, parameters, ctx.RequestServices);
                    return Results.Json(instance);
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 400);
                }
            })
            .ExcludeFromDescription();

            // DELETE /domain-model/testing/instances/{id} — delete a stored instance
            endpoints.MapDelete($"{routePrefix}/testing/instances/{{id}}", (string id, HttpContext ctx) =>
            {
                var deleted = testingService.DeleteInstance(id, ctx.RequestServices);
                return deleted ? Results.Ok() : Results.NotFound();
            })
            .ExcludeFromDescription();

            // POST /domain-model/testing/instances/{id}/invoke — invoke a method on a stored instance
            endpoints.MapPost($"{routePrefix}/testing/instances/{{id}}/invoke", async (string id, HttpContext ctx) =>
            {
                try
                {
                    using var reader = new StreamReader(ctx.Request.Body);
                    var body = await reader.ReadToEndAsync();
                    var request = JsonDocument.Parse(body).RootElement;

                    var methodName = request.GetProperty("methodName").GetString()!;
                    JsonElement? parameters = request.TryGetProperty("parameters", out var p)
                                              && p.ValueKind != JsonValueKind.Null
                        ? p
                        : null;

                    var result = testingService.InvokeMethod(id, methodName, parameters, ctx.RequestServices);
                    return Results.Json(result);
                }
                catch (Exception ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 400);
                }
            })
            .ExcludeFromDescription();
        }

        // Feature editor endpoints
        if (options.EnableFeatureEditor)
        {
            var featureDir = Path.GetFullPath(options.FeatureStoragePath);

            // GET /domain-model/features — list all saved features (folder per feature)
            endpoints.MapGet($"{routePrefix}/features", () =>
            {
                if (!Directory.Exists(featureDir))
                    return Results.Json(Array.Empty<object>());

                var features = Directory.GetDirectories(featureDir)
                    .Select(d => Path.GetFileName(d))
                    .Where(n => SanitizeFileName(n) == n && File.Exists(Path.Combine(featureDir, n, "feature.json")))
                    .OrderBy(n => n)
                    .ToArray();
                return Results.Json(features);
            })
            .ExcludeFromDescription()
            .WithName("FeatureEditorList");

            // GET /domain-model/features/{name} — load a specific feature
            endpoints.MapGet($"{routePrefix}/features/{{name}}", (string name) =>
            {
                var safeName = SanitizeFileName(name);
                if (safeName is null) return Results.BadRequest("Invalid feature name");
                var path = Path.Combine(featureDir, safeName, "feature.json");
                if (!File.Exists(path))
                    return Results.NotFound();
                var content = File.ReadAllText(path);
                return Results.Content(content, "application/json");
            })
            .ExcludeFromDescription()
            .WithName("FeatureEditorGet");

            // PUT /domain-model/features/{name} — create or update a feature
            endpoints.MapPut($"{routePrefix}/features/{{name}}", async (string name, HttpContext ctx) =>
            {
                var safeName = SanitizeFileName(name);
                if (safeName is null) return Results.BadRequest("Invalid feature name");

                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();

                // Validate JSON
                JsonDocument featureDoc;
                try { featureDoc = JsonDocument.Parse(body); }
                catch (JsonException) { return Results.BadRequest("Invalid JSON"); }

                using (featureDoc)
                {
                    var requestedReadOnly = featureDoc.RootElement.TryGetProperty("readOnly", out var ro)
                        && ro.ValueKind == JsonValueKind.True;

                    var featureFolder = Path.Combine(featureDir, safeName);
                    var path = Path.Combine(featureFolder, "feature.json");
                    var existingReadOnly = false;

                    if (File.Exists(path))
                    {
                        existingReadOnly = IsFeatureReadOnly(File.ReadAllText(path));
                    }

                    // Once a feature is read-only, it cannot be downgraded.
                    if (existingReadOnly && !requestedReadOnly)
                    {
                        return Results.BadRequest("Read-only mode cannot be disabled once enabled for a feature.");
                    }

                    var effectiveReadOnly = existingReadOnly || requestedReadOnly;
                    if (effectiveReadOnly)
                    {
                        var validation = ReadOnlyFeatureValidator.Validate(body, graph);
                        if (!validation.IsValid)
                            return Results.BadRequest(validation.ErrorMessage);
                    }

                    Directory.CreateDirectory(featureFolder);
                    await File.WriteAllTextAsync(path, body);
                    return Results.Ok(new { saved = true });
                }
            })
            .ExcludeFromDescription()
            .WithName("FeatureEditorSave");

            // DELETE /domain-model/features/{name} — delete a feature
            endpoints.MapDelete($"{routePrefix}/features/{{name}}", (string name) =>
            {
                var safeName = SanitizeFileName(name);
                if (safeName is null) return Results.BadRequest("Invalid feature name");
                var featureFolder = Path.Combine(featureDir, safeName);
                if (!Directory.Exists(featureFolder))
                    return Results.NotFound();
                Directory.Delete(featureFolder, recursive: true);
                return Results.Ok(new { deleted = true });
            })
            .ExcludeFromDescription()
            .WithName("FeatureEditorDelete");

            // Feature export endpoints
            if (options.FeatureExports.Count > 0)
            {
                // GET /domain-model/features/exports — list available feature exports
                endpoints.MapGet($"{routePrefix}/features/exports", () =>
                    Results.Json(options.FeatureExports.Select(e => new { name = e.Name, extension = e.FileExtension })))
                    .ExcludeFromDescription()
                    .WithName("FeatureExportsList");

                // GET /domain-model/features/{name}/exports/{exportName} — download a feature export
                endpoints.MapGet($"{routePrefix}/features/{{name}}/exports/{{exportName}}", (HttpContext http, string name, string exportName) =>
                {
                    var safeName = SanitizeFileName(name);
                    if (safeName is null) return Results.BadRequest("Invalid feature name");

                    var export = options.FeatureExports.FirstOrDefault(e =>
                        string.Equals(e.Name, exportName, StringComparison.OrdinalIgnoreCase));
                    if (export is null)
                        return Results.NotFound();

                    var path = Path.Combine(featureDir, safeName, "feature.json");
                    if (!File.Exists(path))
                        return Results.NotFound();

                    var featureJson = File.ReadAllText(path);
                    var featureGraph = FeatureGraph.FromDomainGraph(
                        FeatureJsonConverter.ToDomainGraph(featureJson, safeName));

                    var content = export.Builder(featureGraph);
                    if (http.Request.Query.TryGetValue("registerCommands", out var rc) && rc.Count > 0)
                    {
                        var v = rc[0];
                        if (string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1")
                        {
                            var appendix = FeatureCommandRegistrationScaffold.BuildMarkdownAppendix(featureGraph);
                            if (!string.IsNullOrWhiteSpace(appendix))
                                content = content.TrimEnd() + Environment.NewLine + Environment.NewLine + appendix;
                        }
                    }

                    var fileName = $"{safeName}-{export.Name.ToLowerInvariant().Replace(' ', '-')}.{export.FileExtension}";
                    return Results.File(
                        System.Text.Encoding.UTF8.GetBytes(content),
                        "application/octet-stream",
                        fileName);
                })
                .ExcludeFromDescription()
                .WithName("FeatureExportDownload");
            }
        }

        // Developer-mode endpoints
        if (options.EnableDeveloperView)
        {
            // PUT /domain-model/json — accept an edited domain graph
            endpoints.MapPut($"{routePrefix}/json", async (HttpContext ctx) =>
            {
                using var reader = new StreamReader(ctx.Request.Body);
                var body = await reader.ReadToEndAsync();

                // Validate it's parseable JSON
                try
                {
                    using var doc = JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    return Results.BadRequest("Invalid JSON");
                }

                // Update the cached JSON so subsequent GETs return the edited version
                json = body;
                return Results.Ok(new { saved = true });
            })
            .ExcludeFromDescription()
            .WithName("DomainModelJsonUpdate");
        }

        return endpoints;
    }

    /// <summary>
    /// Registers a <see cref="DomainGraph"/> in DI so it can be resolved by the endpoints.
    /// </summary>
    public static IServiceCollection AddDomainModel(this IServiceCollection services, DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        services.AddSingleton(graph);
        return services;
    }

    private static string GetEmbeddedHtml(string routePrefix, string assetsPrefix, DomainModelOptions options)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("domain-model.html", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded domain-model.html resource not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        return html
            .Replace("{{API_URL}}", $"{routePrefix}/json")
            .Replace("{{ASSETS_URL}}", assetsPrefix)
            .Replace("{{DEVELOPER_MODE}}", options.EnableDeveloperView ? "true" : "false")
            .Replace("{{TESTING_MODE}}", options.EnableTestingView ? "true" : "false")
            .Replace("{{FEATURE_EDITOR_MODE}}", options.EnableFeatureEditor ? "true" : "false");
    }

    private static string? GetEmbeddedAsset(string relativePath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Normalize path separators → dots for resource lookup, but keep directory structure
        // Embedded resources use '.' as separator; files under wwwroot/css/foo.css become
        // <RootNamespace>.wwwroot.css.foo.css
        var normalizedPath = relativePath.Replace('/', '.').Replace('\\', '.');
        var suffix = $"wwwroot.{normalizedPath}";

        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Sanitizes a feature name to prevent path traversal. Returns null if invalid.
    /// </summary>
    private static string? SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        // Only allow alphanumeric, dashes, underscores, spaces
        var sanitized = name.Trim();
        if (sanitized.Length == 0 || sanitized.Length > 100) return null;
        foreach (var c in sanitized)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != ' ') return null;
        }
        // Prevent path traversal
        if (sanitized.Contains("..") || sanitized.Contains('/') || sanitized.Contains('\\'))
            return null;
        return sanitized;
    }

    private static string EncodeMetadataFileName(string fullName)
    {
        // Human-readable but reversible. This keeps most namespace characters (including '.')
        // and percent-encodes characters that are invalid in file names (e.g. '<', '>', spaces).
        return Uri.EscapeDataString(fullName);
    }

    private static bool TryDecodeMetadataFileName(string fileName, out string fullName)
    {
        fullName = string.Empty;
        try
        {
            var unescaped = Uri.UnescapeDataString(fileName);

            // Guard against false positives: ensure round-trip matches the input.
            var escapedCheck = Uri.EscapeDataString(unescaped);
            if (!string.Equals(escapedCheck, fileName, StringComparison.OrdinalIgnoreCase))
                return false;

            fullName = unescaped;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsFeatureReadOnly(string featureJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(featureJson);
            return doc.RootElement.TryGetProperty("readOnly", out var ro)
                   && ro.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static BoundedContextUiSelections CreateDefaultBoundedContextUiSelections(DomainGraph graph)
    {
        var all = graph.BoundedContexts.Select(c => c.Name).ToList();
        return new BoundedContextUiSelections
        {
            Explorer = [.. all],
            Diagram = [.. all],
            FeatureEditorPalette = [.. all],
        };
    }

    private static BoundedContextUiSelections NormalizeBoundedContextUiSelections(BoundedContextUiSelections? incoming, DomainGraph graph)
    {
        var all = graph.BoundedContexts.Select(c => c.Name).ToList();
        var valid = all.ToHashSet(StringComparer.Ordinal);
        if (all.Count == 0)
        {
            return new BoundedContextUiSelections
            {
                Explorer = [],
                Diagram = [],
                FeatureEditorPalette = [],
            };
        }

        List<string> Coerce(List<string>? part)
        {
            if (part is null || part.Count == 0)
                return [.. all];
            var filtered = part.Where(valid.Contains).Distinct(StringComparer.Ordinal).ToList();
            return filtered.Count > 0 ? filtered : [.. all];
        }

        var src = incoming ?? new BoundedContextUiSelections();
        return new BoundedContextUiSelections
        {
            Explorer = Coerce(src.Explorer),
            Diagram = Coerce(src.Diagram),
            FeatureEditorPalette = Coerce(src.FeatureEditorPalette),
        };
    }
}
