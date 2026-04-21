using System.Text;

namespace DomainModeling.Graph;

/// <summary>
/// Renders <see cref="UbiquitousLanguageDocument"/> as Markdown (same content as <see cref="UbiquitousLanguageDocumentBuilder"/>).
/// </summary>
public static class UbiquitousLanguageMarkdownExport
{
    /// <summary>
    /// Creates Markdown using the default English ubiquitous language definition.
    /// </summary>
    public static string Build(DomainGraph graph) =>
        Build(UbiquitousLanguageDocumentBuilder.Build(graph));

    /// <summary>
    /// Creates Markdown with a custom definition and optional language key (<c>null</c> = definition default).
    /// </summary>
    public static string Build(DomainGraph graph, UbiquitousLanguageDefinition definition, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(definition);
        return Build(UbiquitousLanguageDocumentBuilder.Build(graph, definition, language));
    }

    /// <summary>
    /// Renders a pre-built ubiquitous language document as Markdown.
    /// </summary>
    public static string Build(UbiquitousLanguageDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var sb = new StringBuilder();
        sb.AppendLine($"# {doc.Title}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(doc.Introduction))
        {
            sb.AppendLine(doc.Introduction.Trim());
            sb.AppendLine();
        }

        foreach (var ctx in doc.BoundedContexts)
            AppendBoundedContext(sb, ctx, doc);

        return sb.ToString();
    }

    private static void AppendBoundedContext(StringBuilder sb, UbiquitousLanguageBoundedContext ctx, UbiquitousLanguageDocument doc)
    {
        var bcHeading = string.Format(doc.BoundedContextMarkdownHeadingFormat, ctx.Name);
        if (!bcHeading.StartsWith('#'))
            sb.AppendLine($"## {bcHeading}");
        else
            sb.AppendLine(bcHeading);

        sb.AppendLine();

        sb.AppendLine($"### {doc.AggregatesSectionLabel}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ctx.Aggregates.EmptyMessage))
        {
            sb.AppendLine($"_{ctx.Aggregates.EmptyMessage}_");
            sb.AppendLine();
        }
        else
        {
            foreach (var root in ctx.Aggregates.Roots)
                AppendConceptTree(sb, root, doc, indent: "");
        }

        sb.AppendLine($"### {doc.DomainEventsSectionLabel}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ctx.DomainEvents.EmptyMessage))
        {
            sb.AppendLine($"_{ctx.DomainEvents.EmptyMessage}_");
            sb.AppendLine();
        }
        else
        {
            foreach (var ev in ctx.DomainEvents.Items)
            {
                sb.AppendLine($"#### {ev.DisplayName}");
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(ev.Description))
                {
                    sb.AppendLine(ev.Description.Trim());
                    sb.AppendLine();
                }

                sb.AppendLine($"_{doc.TypeLabelPrefix}:_ `{ev.TypeName}`");
                sb.AppendLine();
            }
        }
    }

    private static void AppendConceptTree(StringBuilder sb, UbiquitousLanguageConceptBlock block, UbiquitousLanguageDocument doc, string indent)
    {
        if (block.Depth == 0)
        {
            sb.AppendLine($"#### {block.DisplayName}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($"{indent}**{block.DisplayName}** _({block.KindLabel})_");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(block.Description))
        {
            foreach (var line in block.Description!.Trim().Split('\n'))
                sb.AppendLine($"{indent}{line.TrimEnd()}");

            sb.AppendLine();
        }

        sb.AppendLine($"{indent}_{doc.TypeLabelPrefix}:_ `{block.TypeName}`");
        sb.AppendLine();

        AppendRelationsMarkdown(sb, block.Relations, doc, indent);

        var childIndent = indent + "  ";
        foreach (var child in block.LinkedConcepts)
            AppendConceptTree(sb, child, doc, childIndent);
    }

    private static void AppendRelationsMarkdown(
        StringBuilder sb,
        UbiquitousLanguageRelationsBlock rel,
        UbiquitousLanguageDocument doc,
        string indent)
    {
        sb.AppendLine($"{indent}**{doc.RelationsHeadingLabel}**");
        sb.AppendLine();

        if (rel.Items.Count == 0)
        {
            sb.AppendLine($"{indent}_{doc.NoRelationsMessage}_");
            sb.AppendLine();
            return;
        }

        foreach (var r in rel.Items)
        {
            var target = $"`{r.TargetDisplayName}`";
            var via = string.IsNullOrWhiteSpace(r.ViaLabel)
                ? ""
                : $" _({doc.RelationshipViaWord} `{r.ViaLabel}`)_";
            sb.AppendLine($"{indent}- **{r.Phrase}** {target}{via}");
        }

        sb.AppendLine();
    }
}
