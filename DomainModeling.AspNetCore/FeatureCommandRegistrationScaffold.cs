namespace DomainModeling.AspNetCore;

/// <summary>
/// Builds DI registration snippets for command handlers described in a <see cref="FeatureGraph"/>.
/// Intended for feature-editor exports and scaffolding; callers use their own handler interface (e.g. <c>ICommandHandler&lt;T&gt;</c>).
/// </summary>
public static class FeatureCommandRegistrationScaffold
{
    /// <summary>
    /// Emits one <c>AddTransient</c> line per command/handler pair derived from
    /// <see cref="FeatureHandler.Handles"/> (populated from assembly scanning or feature <c>Handles</c> edges).
    /// </summary>
    /// <param name="graph">Feature graph (typically a single bounded context from the feature editor).</param>
    /// <param name="serviceCollectionExpr">Left-hand expression, usually <c>services</c>.</param>
    public static string BuildCSharpRegistrations(FeatureGraph graph, string serviceCollectionExpr = "services")
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCollectionExpr);

        var lines = new List<string>
        {
            "// Command handler registrations (scaffold)",
            "// Replace ICommandHandler<T> with your application's handler interface if it differs.",
        };

        foreach (var ctx in graph.BoundedContexts)
        {
            foreach (var handler in ctx.CommandHandlers)
            {
                foreach (var commandFullName in handler.Handles)
                {
                    if (string.IsNullOrWhiteSpace(commandFullName)) continue;
                    var cmd = GlobalAlias(commandFullName);
                    var hnd = GlobalAlias(handler.FullName);
                    lines.Add(
                        $"{serviceCollectionExpr}.AddTransient<ICommandHandler<{cmd}>, {hnd}>();");
                }
            }
        }

        return lines.Count > 2 ? string.Join(Environment.NewLine, lines) : "";
    }

    /// <summary>
    /// Wraps <see cref="BuildCSharpRegistrations"/> in a Markdown section for appending to text exports.
    /// </summary>
    public static string BuildMarkdownAppendix(FeatureGraph graph, string serviceCollectionExpr = "services")
    {
        var code = BuildCSharpRegistrations(graph, serviceCollectionExpr);
        if (string.IsNullOrWhiteSpace(code)) return "";

        return $"""
            ## Command handler registrations (scaffold)

            > Replace `ICommandHandler<T>` with your application's command-handler abstraction if it differs.

            ```csharp
            {code}
            ```
            """;
    }

    private static string GlobalAlias(string fullName)
    {
        var t = fullName.Trim();
        if (t.StartsWith("global::", StringComparison.Ordinal))
            t = t["global::".Length..];
        return "global::" + t;
    }
}
