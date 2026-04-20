using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Builds a self-contained Markdown prompt for an LLM to implement a feature described by a
/// <see cref="FeatureGraph"/> (typically from the feature editor). Includes structured instructions,
/// the full graph as JSON, optional raw <c>feature.json</c> text, and command-handler registration hints.
/// </summary>
public static class FeatureLlmImplementationPrompt
{
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates Markdown suitable as an LLM system or user prompt to implement the feature.
    /// </summary>
    /// <param name="graph">The feature graph (one bounded context named after the feature).</param>
    /// <param name="rawFeatureEditorJson">
    /// Optional verbatim JSON from <c>feature.json</c>. When provided, it is included so the model
    /// sees exactly what the editor persisted (e.g. <c>readOnly</c>, layout metadata).
    /// </param>
    public static string BuildMarkdown(FeatureGraph graph, string? rawFeatureEditorJson = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var sb = new StringBuilder();
        var featureTitle = graph.BoundedContexts.Count switch
        {
            0 => "Unnamed feature",
            1 => graph.BoundedContexts[0].Name,
            _ => string.Join(", ", graph.BoundedContexts.Select(c => c.Name)),
        };

        sb.AppendLine($"# Implement domain feature: {featureTitle}");
        sb.AppendLine();
        sb.AppendLine("You are assisting with a **Domain-Driven Design** feature. Implement it in the host solution’s style (namespaces, patterns, MediatR or equivalent, testing conventions).");
        sb.AppendLine();
        sb.AppendLine("## Objectives");
        sb.AppendLine();
        sb.AppendLine("- Implement or extend **types** shown in the JSON (aggregates, entities, value objects, domain/integration events, commands, handlers, repositories, services).");
        sb.AppendLine("- Respect **relationships** (ownership, references, emits, handles, manages, etc.).");
        sb.AppendLine("- Add **behavior** on aggregates/entities where methods are specified.");
        sb.AppendLine("- Wire **dependency injection** (e.g. handler registrations for command/event handlers).");
        sb.AppendLine("- Add **automated tests** that cover the main flows implied by the diagram.");
        sb.AppendLine("- Use **full type names** from the JSON when creating new files or registrations.");
        sb.AppendLine();

        sb.AppendLine("## Feature graph (canonical JSON)");
        sb.AppendLine();
        sb.AppendLine("This is the full **FeatureGraph** model derived from the diagram (types, properties, methods, relationships). Treat it as the source of truth for structure.");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(graph, PromptJsonOptions));
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(rawFeatureEditorJson))
        {
            sb.AppendLine("## Raw feature editor JSON");
            sb.AppendLine();
            sb.AppendLine("Verbatim persisted document (may include `readOnly`, `positions`, and editor-only fields):");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(rawFeatureEditorJson.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("## Relationships (quick reference)");
        sb.AppendLine();
        AppendRelationshipsSummary(sb, graph);
        sb.AppendLine();

        var cmdAppendix = FeatureCommandRegistrationScaffold.BuildMarkdownAppendix(graph);
        if (!string.IsNullOrWhiteSpace(cmdAppendix))
        {
            sb.AppendLine(cmdAppendix.TrimEnd());
            sb.AppendLine();
        }

        sb.AppendLine("## Deliverables checklist");
        sb.AppendLine();
        sb.AppendLine("- [ ] Domain model types and behavior aligned with the graph");
        sb.AppendLine("- [ ] Application/infrastructure wiring (handlers, repositories, buses)");
        sb.AppendLine("- [ ] DI registrations updated");
        sb.AppendLine("- [ ] Tests passing");
        sb.AppendLine();

        return sb.ToString();
    }

    private static void AppendRelationshipsSummary(StringBuilder sb, FeatureGraph graph)
    {
        var rows = new List<(string Source, string Kind, string Target, string? Label)>();
        foreach (var ctx in graph.BoundedContexts)
        {
            foreach (var r in ctx.Relationships)
            {
                rows.Add((r.SourceType, r.Kind.ToString(), r.TargetType, r.Label));
            }
        }

        if (rows.Count == 0)
        {
            sb.AppendLine("*No explicit relationships in this feature diagram.*");
            return;
        }

        sb.AppendLine("| Source type | Relationship | Target type | Label |");
        sb.AppendLine("|-------------|--------------|-------------|-------|");
        foreach (var (source, kind, target, label) in rows)
        {
            var lbl = string.IsNullOrWhiteSpace(label) ? "" : label.Replace("|", "\\|");
            sb.AppendLine($"| `{source}` | {kind} | `{target}` | {lbl} |");
        }
    }
}
