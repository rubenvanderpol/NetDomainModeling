using System.Text.Json;
using DomainModeling.Graph;

namespace DomainModeling.AspNetCore;

internal static class ReadOnlyFeatureValidator
{
    internal readonly record struct ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Fail(string error) => new(false, error);
    }

    public static ValidationResult Validate(string featureJson, DomainGraph graph)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(featureJson);
        }
        catch (JsonException)
        {
            return ValidationResult.Fail("Invalid JSON");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ValidationResult.Fail("Feature payload must be a JSON object.");

            var allowedKindsByType = BuildAllowedKindsByType(graph);
            var allowedEdges = BuildAllowedEdges(graph);
            var nodeIds = new HashSet<string>(StringComparer.Ordinal);

            if (root.TryGetProperty("nodes", out var nodesEl))
            {
                if (nodesEl.ValueKind != JsonValueKind.Array)
                    return ValidationResult.Fail("Feature payload 'nodes' must be an array.");

                foreach (var node in nodesEl.EnumerateArray())
                {
                    if (node.ValueKind != JsonValueKind.Object)
                        return ValidationResult.Fail("Each feature node must be an object.");

                    var nodeId = GetRequiredString(node, "id");
                    if (nodeId is null)
                        return ValidationResult.Fail("Feature node is missing a valid 'id'.");

                    var nodeKind = GetRequiredString(node, "kind");
                    if (nodeKind is null)
                        return ValidationResult.Fail($"Feature node '{nodeId}' is missing a valid 'kind'.");

                    if (node.TryGetProperty("isCustom", out var isCustomEl)
                        && isCustomEl.ValueKind == JsonValueKind.True)
                    {
                        return ValidationResult.Fail(
                            $"Read-only feature mode does not allow custom type '{nodeId}'.");
                    }

                    if (!allowedKindsByType.TryGetValue(nodeId, out var allowedKinds)
                        || !allowedKinds.Contains(nodeKind))
                    {
                        return ValidationResult.Fail(
                            $"Type '{nodeId}' with kind '{nodeKind}' is not part of the discovered domain graph.");
                    }

                    nodeIds.Add(nodeId);
                }
            }

            if (root.TryGetProperty("edges", out var edgesEl))
            {
                if (edgesEl.ValueKind != JsonValueKind.Array)
                    return ValidationResult.Fail("Feature payload 'edges' must be an array.");

                foreach (var edge in edgesEl.EnumerateArray())
                {
                    if (edge.ValueKind != JsonValueKind.Object)
                        return ValidationResult.Fail("Each feature edge must be an object.");

                    var source = GetRequiredString(edge, "source");
                    var target = GetRequiredString(edge, "target");
                    var kind = GetRequiredString(edge, "kind");
                    if (source is null || target is null || kind is null)
                        return ValidationResult.Fail("Each feature edge must include 'source', 'target', and 'kind'.");

                    if (!nodeIds.Contains(source) || !nodeIds.Contains(target))
                        return ValidationResult.Fail(
                            $"Feature relationship '{source} -> {target} ({kind})' must reference included nodes.");

                    if (!allowedEdges.Contains((source, target, kind)))
                        return ValidationResult.Fail(
                            $"Relationship '{source} -> {target} ({kind})' is not part of the discovered domain graph.");
                }
            }

            return ValidationResult.Success();
        }
    }

    private static Dictionary<string, HashSet<string>> BuildAllowedKindsByType(DomainGraph graph)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var ctx in graph.BoundedContexts)
        {
            AddKinds(result, ctx.Aggregates.Select(a => a.FullName), "aggregate");
            AddKinds(result, ctx.Entities.Select(e => e.FullName), "entity");
            AddKinds(result, ctx.ValueObjects.Select(v => v.FullName), "valueObject");
            AddKinds(result, ctx.DomainEvents.Select(e => e.FullName), "event");
            AddKinds(result, ctx.IntegrationEvents.Select(e => e.FullName), "integrationEvent");
            AddKinds(result, ctx.CommandHandlerTargets.Select(t => t.FullName), "commandHandlerTarget");
            AddKinds(result, ctx.EventHandlers.Select(h => h.FullName), "eventHandler");
            AddKinds(result, ctx.CommandHandlers.Select(h => h.FullName), "commandHandler");
            AddKinds(result, ctx.QueryHandlers.Select(h => h.FullName), "queryHandler");
            AddKinds(result, ctx.Repositories.Select(r => r.FullName), "repository");
            AddKinds(result, ctx.DomainServices.Select(s => s.FullName), "service");
            AddKinds(result, ctx.SubTypes.Select(s => s.FullName), "subType");
        }

        return result;
    }

    private static void AddKinds(
        Dictionary<string, HashSet<string>> kindsByType,
        IEnumerable<string> typeFullNames,
        string kind)
    {
        foreach (var fullName in typeFullNames)
        {
            if (!kindsByType.TryGetValue(fullName, out var kinds))
            {
                kinds = new HashSet<string>(StringComparer.Ordinal);
                kindsByType[fullName] = kinds;
            }

            kinds.Add(kind);
        }
    }

    private static HashSet<(string Source, string Target, string Kind)> BuildAllowedEdges(DomainGraph graph)
    {
        var edges = new HashSet<(string Source, string Target, string Kind)>();
        foreach (var ctx in graph.BoundedContexts)
        {
            foreach (var rel in ctx.Relationships)
            {
                edges.Add((rel.SourceType, rel.TargetType, rel.Kind.ToString()));
            }
        }

        return edges;
    }

    private static string? GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind != JsonValueKind.String)
            return null;

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
