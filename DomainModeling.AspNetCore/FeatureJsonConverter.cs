using System.Text.Json;
using DomainModeling.Graph;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Converts a feature editor JSON file into a <see cref="DomainGraph"/> with a single
/// bounded context containing the feature's types and relationships.
/// </summary>
internal static class FeatureJsonConverter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses feature JSON and produces a <see cref="DomainGraph"/> with a single
    /// <see cref="BoundedContextNode"/> named after the feature.
    /// </summary>
    public static DomainGraph ToDomainGraph(string featureJson, string featureName)
    {
        var doc = JsonDocument.Parse(featureJson);
        var root = doc.RootElement;

        var ctx = new BoundedContextNode { Name = featureName };

        // Parse nodes
        if (root.TryGetProperty("nodes", out var nodesEl) && nodesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodesEl.EnumerateArray())
            {
                var id = node.GetProperty("id").GetString() ?? "";
                var name = node.GetProperty("name").GetString() ?? "";
                var kind = node.GetProperty("kind").GetString() ?? "";
                var isCustom = node.TryGetProperty("isCustom", out var ic) && ic.GetBoolean();
                var alias = GetOptString(node, "alias");
                var description = GetOptString(node, "description");
                var properties = ParseProperties(node);

                switch (kind)
                {
                    case "aggregate":
                        ctx.Aggregates.Add(new AggregateNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            Layer = GetOptString(node, "layer"),
                            IsCustom = isCustom,
                            Properties = properties,
                            Methods = ParseMethods(node),
                            Rules = ParseRules(node),
                        });
                        break;
                    case "entity":
                        ctx.Entities.Add(new EntityNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            Layer = GetOptString(node, "layer"),
                            IsCustom = isCustom,
                            Properties = properties,
                            Methods = ParseMethods(node),
                            Rules = ParseRules(node),
                        });
                        break;
                    case "valueObject":
                        ctx.ValueObjects.Add(new ValueObjectNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            Layer = GetOptString(node, "layer"),
                            IsCustom = isCustom,
                            Properties = properties,
                            Methods = ParseMethods(node),
                            Rules = ParseRules(node),
                        });
                        break;
                    case "event":
                        ctx.DomainEvents.Add(new DomainEventNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                            Properties = properties,
                        });
                        break;
                    case "integrationEvent":
                        ctx.IntegrationEvents.Add(new DomainEventNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                            Properties = properties,
                        });
                        break;
                    case "commandHandlerTarget":
                    case "command": // legacy feature JSON
                        ctx.CommandHandlerTargets.Add(new CommandHandlerTargetNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            Layer = GetOptString(node, "layer"),
                            IsCustom = isCustom,
                            Properties = properties,
                        });
                        break;
                    case "eventHandler":
                        ctx.EventHandlers.Add(new HandlerNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                        });
                        break;
                    case "commandHandler":
                        ctx.CommandHandlers.Add(new HandlerNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                        });
                        break;
                    case "queryHandler":
                        ctx.QueryHandlers.Add(new HandlerNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                        });
                        break;
                    case "repository":
                        ctx.Repositories.Add(new RepositoryNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                        });
                        break;
                    case "service":
                        ctx.DomainServices.Add(new DomainServiceNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            IsCustom = isCustom,
                        });
                        break;
                    case "subType":
                        ctx.SubTypes.Add(new SubTypeNode
                        {
                            Name = name,
                            FullName = id,
                            Alias = alias,
                            Description = description,
                            Layer = GetOptString(node, "layer"),
                            IsCustom = isCustom,
                            Properties = properties,
                            Methods = ParseMethods(node),
                            Rules = ParseRules(node),
                        });
                        break;
                }
            }
        }

        // Parse edges into relationships
        if (root.TryGetProperty("edges", out var edgesEl) && edgesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in edgesEl.EnumerateArray())
            {
                var source = edge.GetProperty("source").GetString() ?? "";
                var target = edge.GetProperty("target").GetString() ?? "";
                var kindStr = edge.GetProperty("kind").GetString() ?? "";
                var label = edge.TryGetProperty("label", out var lbl) ? lbl.GetString() : null;

                if (Enum.TryParse<RelationshipKind>(kindStr, out var relKind))
                {
                    ctx.Relationships.Add(new Relationship
                    {
                        SourceType = source,
                        TargetType = target,
                        Kind = relKind,
                        Label = string.IsNullOrWhiteSpace(label) ? null : label,
                    });
                }
            }
        }

        // Derive EmittedEvents from Emits relationships
        var emitsBySource = ctx.Relationships
            .Where(r => r.Kind == RelationshipKind.Emits)
            .GroupBy(r => r.SourceType)
            .ToDictionary(g => g.Key, g => g.Select(r => r.TargetType).ToList());

        foreach (var agg in ctx.Aggregates)
        {
            if (emitsBySource.TryGetValue(agg.FullName, out var events))
                agg.EmittedEvents.AddRange(events);
        }

        foreach (var ent in ctx.Entities)
        {
            if (emitsBySource.TryGetValue(ent.FullName, out var events))
                ent.EmittedEvents.AddRange(events);
        }

        CrossReferenceCommandHandlersFromFeatureEdges(ctx);

        return new DomainGraph(ctx);
    }

    /// <summary>
    /// Populates <see cref="HandlerNode.Handles"/> and <see cref="CommandHandlerTargetNode.HandledBy"/>
    /// from <see cref="RelationshipKind.Handles"/> edges drawn in the feature editor (command handler ↔ command DTO).
    /// </summary>
    private static void CrossReferenceCommandHandlersFromFeatureEdges(BoundedContextNode ctx)
    {
        var handlersByName = ctx.CommandHandlers.ToDictionary(h => h.FullName, StringComparer.Ordinal);
        var targetsByName = ctx.CommandHandlerTargets.ToDictionary(t => t.FullName, StringComparer.Ordinal);

        foreach (var rel in ctx.Relationships)
        {
            if (rel.Kind != RelationshipKind.Handles) continue;

            handlersByName.TryGetValue(rel.SourceType, out var handlerFromSource);
            targetsByName.TryGetValue(rel.TargetType, out var targetFromTarget);
            if (handlerFromSource is not null && targetFromTarget is not null)
            {
                AddUnique(handlerFromSource.Handles, targetFromTarget.FullName);
                AddUnique(targetFromTarget.HandledBy, handlerFromSource.FullName);
                continue;
            }

            // Edge may have been drawn command → handler
            targetsByName.TryGetValue(rel.SourceType, out var targetFromSource);
            handlersByName.TryGetValue(rel.TargetType, out var handlerFromTarget);
            if (handlerFromTarget is not null && targetFromSource is not null)
            {
                AddUnique(handlerFromTarget.Handles, targetFromSource.FullName);
                AddUnique(targetFromSource.HandledBy, handlerFromTarget.FullName);
            }
        }
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!list.Contains(value, StringComparer.Ordinal))
            list.Add(value);
    }

    private static string? GetOptString(JsonElement node, string propName)
    {
        if (!node.TryGetProperty(propName, out var el)) return null;
        var s = el.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static List<Graph.PropertyInfo> ParseProperties(JsonElement node)
    {
        // Prefer structuredProps (has name + type objects), but only if non-empty
        if (node.TryGetProperty("structuredProps", out var sp) && sp.ValueKind == JsonValueKind.Array
            && sp.GetArrayLength() > 0)
        {
            var result = new List<PropertyDto>();
            foreach (var prop in sp.EnumerateArray())
            {
                var pName = prop.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var pType = prop.TryGetProperty("type", out var t)
                    ? t.GetString() ?? "string"
                    : prop.TryGetProperty("typeName", out var tn) ? tn.GetString() ?? "string" : "string";
                if (!string.IsNullOrWhiteSpace(pName))
                {
                    result.Add(new PropertyDto { Name = pName, TypeName = pType });
                }
            }
            return result.Select(d => d.ToGraphProperty()).ToList();
        }

        // Fallback: parse from display strings "name: type"
        if (node.TryGetProperty("props", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            var result = new List<PropertyDto>();
            foreach (var prop in props.EnumerateArray())
            {
                var str = prop.GetString() ?? "";
                var parts = str.Split(':', 2, StringSplitOptions.TrimEntries);
                var pName = parts.Length > 0 ? parts[0] : str;
                var pType = parts.Length > 1 ? parts[1] : "string";
                if (!string.IsNullOrWhiteSpace(pName))
                {
                    result.Add(new PropertyDto { Name = pName, TypeName = pType });
                }
            }
            return result.Select(d => d.ToGraphProperty()).ToList();
        }

        return [];
    }

    private static List<Graph.MethodInfo> ParseMethods(JsonElement node)
    {
        if (!node.TryGetProperty("methods", out var methods) || methods.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<MethodDto>();
        foreach (var m in methods.EnumerateArray())
        {
            // Support both structured objects and display strings
            if (m.ValueKind == JsonValueKind.Object)
            {
                var mName = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                var mReturn = m.TryGetProperty("returnTypeName", out var r) ? r.GetString() ?? "void" : "void";
                var mParams = new List<MethodParameterDto>();
                if (m.TryGetProperty("parameters", out var pArr) && pArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pArr.EnumerateArray())
                    {
                        var pn = p.TryGetProperty("name", out var pnEl) ? pnEl.GetString() ?? "" : "";
                        var pt = p.TryGetProperty("typeName", out var ptEl) ? ptEl.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(pn))
                            mParams.Add(new MethodParameterDto { Name = pn, TypeName = pt });
                    }
                }
                if (!string.IsNullOrWhiteSpace(mName))
                    result.Add(new MethodDto { Name = mName, ReturnTypeName = mReturn, Parameters = mParams });
            }
            else
            {
                var str = m.GetString() ?? "";
                // Format: "ReturnType MethodName(...)" or "MethodName(...)"
                var parenIdx = str.IndexOf('(');
                var head = parenIdx >= 0 ? str[..parenIdx].Trim() : str.Trim();
                var headParts = head.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string returnTypeName = "void";
                string methodName = head;
                if (headParts.Length >= 2)
                {
                    methodName = headParts[^1];
                    returnTypeName = string.Join(' ', headParts[..^1]);
                }
                else if (headParts.Length == 1)
                {
                    methodName = headParts[0];
                }

                result.Add(new MethodDto
                {
                    Name = methodName,
                    ReturnTypeName = returnTypeName,
                });
            }
        }
        return result.Select(d => d.ToGraphMethod()).ToList();
    }

    private static List<DomainRuleInfo> ParseRules(JsonElement node)
    {
        if (!node.TryGetProperty("rules", out var rulesEl) || rulesEl.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<DomainRuleInfo>();
        foreach (var r in rulesEl.EnumerateArray())
        {
            if (r.ValueKind == JsonValueKind.Object)
            {
                var ruleName = r.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "";
                var ruleText = r.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(ruleName) && string.IsNullOrWhiteSpace(ruleText))
                    continue;
                list.Add(new DomainRuleInfo
                {
                    Name = string.IsNullOrWhiteSpace(ruleName) ? "Rule" : ruleName.Trim(),
                    Text = ruleText ?? "",
                });
            }
            else if (r.ValueKind == JsonValueKind.String)
            {
                var s = r.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(s)) continue;
                list.Add(new DomainRuleInfo { Name = "Rule", Text = s.Trim() });
            }
        }

        return list;
    }
}
