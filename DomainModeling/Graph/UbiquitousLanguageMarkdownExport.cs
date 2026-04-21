using System.Text;

namespace DomainModeling.Graph;

/// <summary>
/// Renders <see cref="UbiquitousLanguageDocument"/> as Markdown (same content as <see cref="UbiquitousLanguageDocumentBuilder"/>).
/// </summary>
public static class UbiquitousLanguageMarkdownExport
{
    /// <summary>
    /// Creates Markdown suitable for download or documentation (e.g. registered via <c>AddExport</c> in ASP.NET Core).
    /// </summary>
    public static string Build(DomainGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Build(UbiquitousLanguageDocumentBuilder.Build(graph));
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
            AppendBoundedContext(sb, ctx);

        return sb.ToString();
    }

    private static void AppendBoundedContext(StringBuilder sb, UbiquitousLanguageBoundedContext ctx)
    {
        sb.AppendLine($"## Bounded context: {ctx.Name}");
        sb.AppendLine();

        sb.AppendLine("### Aggregates");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ctx.Aggregates.EmptyMessage))
        {
            sb.AppendLine($"_{ctx.Aggregates.EmptyMessage}_");
            sb.AppendLine();
        }
        else
        {
            foreach (var root in ctx.Aggregates.Roots)
                AppendConceptTree(sb, root, indent: "");
        }

        sb.AppendLine("### Domain events");
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

                sb.AppendLine($"_Type:_ `{ev.TypeName}`");
                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Aggregate roots use <c>####</c> headings; nested linked concepts use indented bold lines (max 4 heading levels in the file).
    /// </summary>
    private static void AppendConceptTree(StringBuilder sb, UbiquitousLanguageConceptBlock block, string indent)
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

        sb.AppendLine($"{indent}_Type:_ `{block.TypeName}`");
        sb.AppendLine();

        AppendRelationsMarkdown(sb, block.Relations, indent);

        var childIndent = indent + "  ";
        foreach (var child in block.LinkedConcepts)
            AppendConceptTree(sb, child, childIndent);
    }

    private static void AppendRelationsMarkdown(StringBuilder sb, UbiquitousLanguageRelationsBlock rel, string indent)
    {
        sb.AppendLine($"{indent}**Relations**");
        sb.AppendLine();

        if (rel.Items.Count == 0)
        {
            sb.AppendLine($"{indent}_None from this concept._");
            sb.AppendLine();
            return;
        }

        foreach (var r in rel.Items)
        {
            var target = $"`{r.TargetDisplayName}`";
            var via = string.IsNullOrWhiteSpace(r.ViaLabel) ? "" : $" _(via `{r.ViaLabel}`)_";
            sb.AppendLine($"{indent}- **{r.Phrase}** {target}{via}");
        }

        sb.AppendLine();
    }
}
