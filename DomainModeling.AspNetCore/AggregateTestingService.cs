using DomainModeling.Graph;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Options for the aggregate testing feature.
/// </summary>
public sealed class DomainModelTestingOptions
{
    /// <summary>
    /// Map of aggregate type full name (or short name) → static factory method name.
    /// When configured, the testing UI defaults to the factory method for creating instances.
    /// </summary>
    public Dictionary<string, string> FactoryMethods { get; } = new();

    /// <summary>
    /// The open generic repository interface type (e.g. <c>typeof(IRepository&lt;&gt;)</c>).
    /// When set, the testing service will resolve the closed generic from DI and attempt to
    /// call configured methods after creating an instance.
    /// </summary>
    public Type? RepositoryInterfaceType { get; set; }

    /// <summary>
    /// Configured repository method mappings.
    /// </summary>
    internal RepositoryMethodOptions RepositoryMethods { get; } = new();

    /// <summary>
    /// Configure repository method mappings using fluent syntax.
    /// <para>
    /// Example:
    /// <code>
    /// opts.Testing.Repository(repo => repo
    ///     .Add("AddAsync")
    ///     .Update("UpdateAsync")
    ///     .Delete("DeleteAsync"));
    /// </code>
    /// </para>
    /// </summary>
    public DomainModelTestingOptions Repository(Action<RepositoryMethodBuilder> configure)
    {
        var builder = new RepositoryMethodBuilder(RepositoryMethods);
        configure(builder);
        return this;
    }
}

/// <summary>
/// Holds the configured repository method names.
/// </summary>
public sealed class RepositoryMethodOptions
{
    public string? AddMethodName { get; internal set; }
    public string? UpdateMethodName { get; internal set; }
    public string? DeleteMethodName { get; internal set; }
}

/// <summary>
/// Fluent builder for configuring repository method names.
/// </summary>
public sealed class RepositoryMethodBuilder
{
    private readonly RepositoryMethodOptions _options;

    internal RepositoryMethodBuilder(RepositoryMethodOptions options) => _options = options;

    /// <summary>
    /// Configures the method name used to add (persist) a new aggregate.
    /// Defaults to <c>"AddAsync"</c> when called without arguments.
    /// </summary>
    public RepositoryMethodBuilder Add(string methodName = "AddAsync")
    {
        _options.AddMethodName = methodName;
        return this;
    }

    /// <summary>
    /// Configures the method name used to update an existing aggregate.
    /// Defaults to <c>"UpdateAsync"</c> when called without arguments.
    /// </summary>
    public RepositoryMethodBuilder Update(string methodName = "UpdateAsync")
    {
        _options.UpdateMethodName = methodName;
        return this;
    }

    /// <summary>
    /// Configures the method name used to delete an aggregate.
    /// Defaults to <c>"DeleteAsync"</c> when called without arguments.
    /// </summary>
    public RepositoryMethodBuilder Delete(string methodName = "DeleteAsync")
    {
        _options.DeleteMethodName = methodName;
        return this;
    }
}

/// <summary>
/// Provides aggregate testing capabilities: type metadata extraction,
/// instance creation via constructors or factory methods, and in-memory storage
/// with optional DI repository persistence.
/// </summary>
internal sealed class AggregateTestingService
{
    private readonly DomainGraph _graph;
    private readonly DomainModelTestingOptions _options;
    private readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    private readonly ConcurrentDictionary<string, StoredInstance> _store = new();

    private static readonly JsonSerializerOptions SerializeOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions DeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record StoredInstance(string Id, object Instance, Type Type, DateTime CreatedAt);

    public AggregateTestingService(DomainGraph graph, DomainModelTestingOptions options)
    {
        _graph = graph;
        _options = options;
    }

    // ── Public API ───────────────────────────────────────

    /// <summary>
    /// Returns metadata for all aggregate types including constructors,
    /// factory methods, and settable properties.
    /// </summary>
    public object GetAggregateMetadata()
    {
        var result = new List<object>();

        foreach (var bc in _graph.BoundedContexts)
        {
            foreach (var agg in bc.Aggregates)
            {
                var type = ResolveType(agg.FullName);
                if (type == null) continue;

                var constructors = GetConstructors(type);
                var factories = GetFactoryMethods(type);
                var configuredFactory = _options.FactoryMethods.GetValueOrDefault(agg.FullName)
                                        ?? _options.FactoryMethods.GetValueOrDefault(agg.Name);
                var settableProps = GetSettableProperties(type);
                var methods = GetPublicMethods(type);

                result.Add(new
                {
                    name = agg.Name,
                    fullName = agg.FullName,
                    description = agg.Description,
                    boundedContext = bc.Name,
                    constructors,
                    factoryMethods = factories,
                    configuredFactory,
                    properties = settableProps,
                    methods,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Creates an aggregate instance using either JSON deserialization or a factory method.
    /// Stores the result in the in-memory store and optionally saves to a DI repository.
    /// </summary>
    public object CreateInstance(
        string typeFullName,
        JsonElement? parameters,
        string? factoryMethod,
        IServiceProvider? serviceProvider)
    {
        var type = ResolveType(typeFullName)
            ?? throw new InvalidOperationException($"Type '{typeFullName}' not found in loaded assemblies.");

        object instance;

        if (!string.IsNullOrEmpty(factoryMethod))
        {
            instance = CreateViaFactory(type, factoryMethod, parameters);
        }
        else
        {
            instance = CreateViaDeserialization(type, parameters);
        }

        // Extract ID from the instance (aggregates inherit Entity which has Id)
        var idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var id = idProp?.GetValue(instance)?.ToString() ?? Guid.NewGuid().ToString();

        var now = DateTime.UtcNow;
        _store[id] = new StoredInstance(id, instance, type, now);

        // Try DI repository add
        InvokeRepositoryMethod(instance, type, _options.RepositoryMethods.AddMethodName, serviceProvider);

        return FormatInstance(id, instance, type, now);
    }

    /// <summary>
    /// Updates a stored instance with new property values and optionally calls Update on the DI repository.
    /// </summary>
    public object UpdateInstance(
        string id,
        JsonElement? parameters,
        IServiceProvider? serviceProvider)
    {
        if (!_store.TryGetValue(id, out var stored))
            throw new InvalidOperationException($"Instance '{id}' not found.");

        // Re-deserialize with updated properties merged
        var type = stored.Type;
        object updated;

        if (parameters is not null && parameters.Value.ValueKind == JsonValueKind.Object)
        {
            // Serialize current → merge incoming → deserialize back
            var currentJson = JsonSerializer.Serialize(stored.Instance, type, SerializeOpts);
            var currentDoc = JsonDocument.Parse(currentJson);
            var merged = MergeJson(currentDoc.RootElement, parameters.Value);
            updated = JsonSerializer.Deserialize(merged, type, DeserializeOpts)
                      ?? throw new InvalidOperationException("Failed to merge and deserialize.");
        }
        else
        {
            updated = stored.Instance;
        }

        var now = DateTime.UtcNow;
        _store[id] = new StoredInstance(id, updated, type, now);

        InvokeRepositoryMethod(updated, type, _options.RepositoryMethods.UpdateMethodName, serviceProvider);

        return FormatInstance(id, updated, type, now);
    }

    /// <summary>
    /// Returns all stored instances.
    /// </summary>
    public object GetInstances()
    {
        return _store.Values
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => FormatInstance(s.Id, s.Instance, s.Type, s.CreatedAt))
            .ToList();
    }

    /// <summary>
    /// Deletes a stored instance by ID and optionally calls Delete on the DI repository.
    /// </summary>
    public bool DeleteInstance(string id, IServiceProvider? serviceProvider = null)
    {
        if (!_store.TryRemove(id, out var stored)) return false;

        InvokeRepositoryMethod(stored.Instance, stored.Type, _options.RepositoryMethods.DeleteMethodName, serviceProvider);
        return true;
    }

    /// <summary>
    /// Invokes a public method on a stored aggregate instance.
    /// </summary>
    public object InvokeMethod(
        string id,
        string methodName,
        JsonElement? parameters,
        IServiceProvider? serviceProvider)
    {
        if (!_store.TryGetValue(id, out var stored))
            throw new InvalidOperationException($"Instance '{id}' not found.");

        var type = stored.Type;
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on '{type.Name}'.");

        var methodParams = method.GetParameters();
        var args = MatchParameters(methodParams, parameters);

        var result = method.Invoke(stored.Instance, args);
        if (result is Task task) task.GetAwaiter().GetResult();

        // Re-store in case the method mutated state
        var now = DateTime.UtcNow;
        var idProp = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var instanceId = idProp?.GetValue(stored.Instance)?.ToString() ?? id;
        _store[instanceId] = new StoredInstance(instanceId, stored.Instance, type, now);

        // Update repository
        InvokeRepositoryMethod(stored.Instance, type, _options.RepositoryMethods.UpdateMethodName, serviceProvider);

        // Return the updated instance plus any domain events raised
        var formatted = FormatInstance(instanceId, stored.Instance, type, now);
        var events = GetRaisedEvents(stored.Instance, type);

        return new { instance = formatted, raisedEvents = events };
    }

    // ── Instance creation ────────────────────────────────

    private static object CreateViaFactory(Type type, string methodName, JsonElement? parameters)
    {
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Static method '{methodName}' not found on '{type.Name}'.");

        var methodParams = method.GetParameters();
        var args = MatchParameters(methodParams, parameters);

        return method.Invoke(null, args)
            ?? throw new InvalidOperationException($"Factory method '{methodName}' returned null.");
    }

    private static object CreateViaDeserialization(Type type, JsonElement? parameters)
    {
        if (parameters is null ||
            parameters.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Activator.CreateInstance(type)
                ?? throw new InvalidOperationException($"Could not create instance of '{type.Name}'.");
        }

        var json = parameters.Value.GetRawText();
        return JsonSerializer.Deserialize(json, type, DeserializeOpts)
            ?? throw new InvalidOperationException($"Deserialization of '{type.Name}' returned null.");
    }

    private static object?[] MatchParameters(ParameterInfo[] methodParams, JsonElement? parameters)
    {
        if (methodParams.Length == 0) return [];

        var args = new object?[methodParams.Length];

        if (parameters is null || parameters.Value.ValueKind != JsonValueKind.Object)
        {
            for (var i = 0; i < methodParams.Length; i++)
                args[i] = methodParams[i].HasDefaultValue
                    ? methodParams[i].DefaultValue
                    : DefaultOf(methodParams[i].ParameterType);
            return args;
        }

        var obj = parameters.Value;
        for (var i = 0; i < methodParams.Length; i++)
        {
            var p = methodParams[i];
            if (TryGetJsonProperty(obj, p.Name!, out var val))
            {
                args[i] = JsonSerializer.Deserialize(val.GetRawText(), p.ParameterType, DeserializeOpts);
            }
            else if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
            }
            else
            {
                args[i] = DefaultOf(p.ParameterType);
            }
        }

        return args;
    }

    private static bool TryGetJsonProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value)) return true;
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return obj.TryGetProperty(camel, out value);
    }

    // ── DI repository integration ────────────────────────

    private void InvokeRepositoryMethod(object instance, Type aggregateType, string? methodName, IServiceProvider? sp)
    {
        if (sp is null || _options.RepositoryInterfaceType is null || string.IsNullOrEmpty(methodName)) return;

        try
        {
            var closedRepoType = _options.RepositoryInterfaceType.MakeGenericType(aggregateType);
            var repo = sp.GetService(closedRepoType);
            if (repo is null) return;

            var method = repo.GetType().GetMethod(methodName);
            if (method is null) return;

            var result = method.Invoke(repo, [instance, CancellationToken.None]);
            if (result is Task task) task.GetAwaiter().GetResult();
        }
        catch
        {
            // Silently continue — DI repository call is best-effort
        }
    }

    private static string MergeJson(JsonElement current, JsonElement updates)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var prop in current.EnumerateObject())
            dict[prop.Name] = prop.Value;

        foreach (var prop in updates.EnumerateObject())
            dict[prop.Name] = prop.Value;

        return JsonSerializer.Serialize(dict, SerializeOpts);
    }

    // ── Metadata extraction ──────────────────────────────

    private static List<object> GetConstructors(Type type)
    {
        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Select(c => (object)new
            {
                parameters = c.GetParameters()
                    .Select(p =>
                    {
                        var isComplex = !IsPrimitive(p.ParameterType);
                        return (object)new
                        {
                            name = p.Name,
                            typeName = Friendly(p.ParameterType),
                            fullTypeName = p.ParameterType.FullName,
                            isRequired = !p.HasDefaultValue,
                            defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                            isComplex,
                            subProperties = isComplex ? GetSubProperties(p.ParameterType) : null,
                        };
                    }).ToList()
            }).ToList();
    }

    private static List<object> GetFactoryMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == type || m.ReturnType.IsAssignableTo(type))
            .Where(m => !m.IsSpecialName)
            .Select(m => (object)new
            {
                name = m.Name,
                parameters = m.GetParameters()
                    .Select(p =>
                    {
                        var isComplex = !IsPrimitive(p.ParameterType);
                        return (object)new
                        {
                            name = p.Name,
                            typeName = Friendly(p.ParameterType),
                            fullTypeName = p.ParameterType.FullName,
                            isRequired = !p.HasDefaultValue,
                            defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                            isComplex,
                            subProperties = isComplex ? GetSubProperties(p.ParameterType) : null,
                        };
                    }).ToList()
            }).ToList();
    }

    private static List<object> GetSettableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Where(p => p.Name is not "Id" and not "Events")
            .Where(p => p.DeclaringType != typeof(object))
            .Select(p =>
            {
                var isRequired = p.GetCustomAttributes()
                    .Any(a => a.GetType().Name == "RequiredMemberAttribute");
                var isComplex = !IsPrimitive(p.PropertyType);
                var isCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                                   && p.PropertyType != typeof(string);

                return (object)new
                {
                    name = p.Name,
                    typeName = Friendly(p.PropertyType),
                    fullTypeName = p.PropertyType.FullName,
                    isRequired,
                    isComplex,
                    isCollection,
                    subProperties = isComplex && !isCollection ? GetSubProperties(p.PropertyType) : null,
                };
            }).ToList();
    }

    /// <summary>
    /// Extracts the public settable properties of a complex type for nested form rendering.
    /// Only goes one level deep to avoid cycles.
    /// </summary>
    private static List<object>? GetSubProperties(Type type)
    {
        // Unwrap nullable
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (IsPrimitive(underlying)) return null;

        var props = underlying.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is { IsPublic: true })
            .Where(p => p.DeclaringType != typeof(object))
            .Select(p =>
            {
                var isRequired = p.GetCustomAttributes()
                    .Any(a => a.GetType().Name == "RequiredMemberAttribute");
                var subIsComplex = !IsPrimitive(p.PropertyType);
                var subIsCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                                     && p.PropertyType != typeof(string);

                return (object)new
                {
                    name = p.Name,
                    typeName = Friendly(p.PropertyType),
                    fullTypeName = p.PropertyType.FullName,
                    isRequired,
                    isComplex = subIsComplex,
                    isCollection = subIsCollection,
                    // One level only — no further nesting
                };
            }).ToList();

        return props.Count > 0 ? props : null;
    }

    // ── Formatting ───────────────────────────────────────

    private static object FormatInstance(string id, object instance, Type type, DateTime createdAt)
    {
        var json = JsonSerializer.Serialize(instance, type, SerializeOpts);
        var properties = JsonSerializer.Deserialize<JsonElement>(json);

        return new
        {
            id,
            typeName = type.Name,
            typeFullName = type.FullName,
            createdAt = createdAt.ToString("O"),
            properties,
        };
    }

    private static List<object> GetRaisedEvents(object instance, Type type)
    {
        var eventsProp = type.GetProperty("Events", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (eventsProp is null) return [];

        var eventsValue = eventsProp.GetValue(instance);
        if (eventsValue is not System.Collections.IEnumerable enumerable) return [];

        var result = new List<object>();
        foreach (var evt in enumerable)
        {
            result.Add(new
            {
                typeName = evt.GetType().Name,
                properties = JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(evt, evt.GetType(), SerializeOpts)),
            });
        }
        return result;
    }

    /// <summary>
    /// Gets the public instance methods declared on an aggregate type,
    /// excluding property accessors, object base methods and the Raise helper.
    /// </summary>
    private static List<object> GetPublicMethods(Type type)
    {
        var objectMethods = typeof(object).GetMethods().Select(m => m.Name).ToHashSet();

        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // skip property getters/setters
            .Where(m => !objectMethods.Contains(m.Name))
            .Where(m => m.Name is not "Raise" and not "GetType" and not "ToString" and not "Equals" and not "GetHashCode")
            .Select(m => (object)new
            {
                name = m.Name,
                returnTypeName = Friendly(m.ReturnType),
                parameters = m.GetParameters()
                    .Select(p =>
                    {
                        var isComplex = !IsPrimitive(p.ParameterType);
                        return (object)new
                        {
                            name = p.Name,
                            typeName = Friendly(p.ParameterType),
                            fullTypeName = p.ParameterType.FullName,
                            isRequired = !p.HasDefaultValue,
                            defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                            isComplex,
                            subProperties = isComplex ? GetSubProperties(p.ParameterType) : null,
                        };
                    }).ToList()
            }).ToList();
    }

    // ── Type utilities ───────────────────────────────────

    private Type? ResolveType(string fullName)
    {
        return _typeCache.GetOrAdd(fullName, name =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(name);
                    if (type is not null) return type;
                }
                catch { /* skip problematic assemblies */ }
            }
            return null;
        });
    }

    private static bool IsPrimitive(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
               || underlying == typeof(string)
               || underlying == typeof(decimal)
               || underlying == typeof(Guid)
               || underlying == typeof(DateTime)
               || underlying == typeof(DateTimeOffset)
               || underlying.IsEnum;
    }

    private static string Friendly(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(Guid)) return "Guid";
        if (type == typeof(DateTime)) return "DateTime";
        if (type == typeof(DateTimeOffset)) return "DateTimeOffset";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return Friendly(type.GetGenericArguments()[0]) + "?";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return $"List<{Friendly(type.GetGenericArguments()[0])}>";
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))
            return $"IReadOnlyCollection<{Friendly(type.GetGenericArguments()[0])}>";
        return type.Name;
    }

    private static object? DefaultOf(Type type)
        => type.IsValueType ? Activator.CreateInstance(type) : null;
}
