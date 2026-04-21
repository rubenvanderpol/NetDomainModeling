using System.Text;

namespace DomainModeling.Graph;

/// <summary>
/// Builds a Markdown document that summarizes the ubiquitous language for an entire <see cref="DomainGraph"/>:
/// aggregates (with descriptions and <see cref="RelationshipKind.Has"/> / <see cref="RelationshipKind.HasMany"/>
/// relations) and domain events (with descriptions), grouped by bounded context.
/// </summary>
public static class UbiquitousLanguageMarkdownExport
{
    /// <summary>
    /// Creates Markdown suitable for download or documentation (e.g. registered via <c>AddExport</c> in ASP.NET Core).
    /// </summary>
    public static string Build(DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var sb = new StringBuilder();
        sb.AppendLine("# Ubiquitous language");
        sb.AppendLine();
        sb.AppendLine("This document is generated from the domain model. It lists aggregates with their descriptions and structural relations, then domain events.");
        sb.AppendLine();

        foreach (var ctx in graph.BoundedContexts)
        {
            AppendBoundedContext(sb, ctx);
        }

        return sb.ToString();
    }

    private static void AppendBoundedContext(StringBuilder sb, BoundedContextNode ctx)
    {
        sb.AppendLine($"## Bounded context: {ctx.Name}");
        sb.AppendLine();

        sb.AppendLine("### Aggregates");
        sb.AppendLine();

        if (ctx.Aggregates.Count == 0)
        {
            sb.AppendLine("_No aggregates in this bounded context._");
            sb.AppendLine();
        }
        else
        {
            var displayByFullName = BuildDisplayLookup(ctx);

            foreach (var aggregate in ctx.Aggregates.OrderBy(a => DisplayName(a), StringComparer.OrdinalIgnoreCase))
            {
                AppendAggregate(sb, ctx, aggregate, displayByFullName);
            }
        }

        sb.AppendLine("### Domain events");
        sb.AppendLine();

        if (ctx.DomainEvents.Count == 0)
        {
            sb.AppendLine("_No domain events in this bounded context._");
            sb.AppendLine();
        }
        else
        {
            foreach (var ev in ctx.DomainEvents.OrderBy(e => DisplayName(e), StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"#### {DisplayName(ev)}");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(ev.Description))
                {
                    sb.AppendLine(ev.Description.Trim());
                    sb.AppendLine();
                }

                sb.AppendLine($"_Type:_ `{ev.Name}`");
                sb.AppendLine();
            }
        }
    }

    private static void AppendAggregate(
        StringBuilder sb,
        BoundedContextNode ctx,
        AggregateNode aggregate,
        Dictionary<string, string> displayByFullName)
    {
        sb.AppendLine($"#### {DisplayName(aggregate)}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(aggregate.Description))
        {
            sb.AppendLine(aggregate.Description.Trim());
            sb.AppendLine();
        }

        sb.AppendLine($"_Type:_ `{aggregate.Name}`");
        sb.AppendLine();

        var relations = ctx.Relationships
            .Where(r =>
                string.Equals(r.SourceType, aggregate.FullName, StringComparison.Ordinal) &&
                (r.Kind == RelationshipKind.Has || r.Kind == RelationshipKind.HasMany))
            .OrderBy(r => r.Kind)
            .ThenBy(r => TargetLabel(r.TargetType, displayByFullName), StringComparer.OrdinalIgnoreCase)
            .ToList();

        sb.AppendLine("**Relations**");
        sb.AppendLine();

        if (relations.Count == 0)
        {
            sb.AppendLine("_No has / has many relations from this aggregate._");
            sb.AppendLine();
            return;
        }

        foreach (var r in relations)
        {
            var phrase = r.Kind == RelationshipKind.HasMany ? "has many" : "has";
            var target = TargetLabel(r.TargetType, displayByFullName);
            sb.AppendLine($"- **{phrase}** {target}");
        }

        sb.AppendLine();
    }

    private static Dictionary<string, string> BuildDisplayLookup(BoundedContextNode ctx)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string fullName, string name, string? alias)
        {
            map[fullName] = string.IsNullOrWhiteSpace(alias) ? name : alias.Trim();
        }

        foreach (var n in ctx.Aggregates)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.Entities)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.ValueObjects)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.DomainEvents)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.IntegrationEvents)
            Add(n.FullName, n.Name, n.Alias);
        foreach (var n in ctx.SubTypes)
            Add(n.FullName, n.Name, n.Alias);

        return map;
    }

    private static string TargetLabel(string targetFullName, Dictionary<string, string> displayByFullName)
    {
        if (displayByFullName.TryGetValue(targetFullName, out var label))
            return $"`{label}`";

        var shortName = targetFullName.Split('.').LastOrDefault() ?? targetFullName;
        return $"`{shortName}`";
    }

    private static string DisplayName(AggregateNode n) =>
        string.IsNullOrWhiteSpace(n.Alias) ? n.Name : n.Alias.Trim();

    private static string DisplayName(DomainEventNode n) =>
        string.IsNullOrWhiteSpace(n.Alias) ? n.Name : n.Alias.Trim();
}
