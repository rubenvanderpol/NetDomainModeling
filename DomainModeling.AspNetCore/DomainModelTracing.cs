using System.Text.Json;
using System.Text.Json.Serialization;
using DomainModeling.Graph;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Broadcasts domain event notifications to the Trace tab in the explorer UI (GitHub #34).
/// Call <see cref="Notify"/> from middleware or application code when an event is dispatched.
/// </summary>
public static class DomainModelTracing
{
    /// <summary>
    /// Notifies connected Trace tab clients about a domain event instance.
    /// Resolves matching event nodes and in-context handlers from the registered <see cref="DomainGraph"/>.
    /// </summary>
    /// <param name="services">The request-scoped or root <see cref="IServiceProvider"/>.</param>
    /// <param name="eventType">The CLR type of the event (closed or open generic).</param>
    /// <param name="payload">Optional payload; serialized with <see cref="JsonSerializer"/>.</param>
    /// <param name="boundedContextName">When set, only handlers in this bounded context are included.</param>
    public static async Task NotifyAsync(
        IServiceProvider services,
        Type eventType,
        object? payload = null,
        string? boundedContextName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(eventType);

        var notifier = services.GetService<IDomainModelTraceNotifier>();
        if (notifier is null)
            return;

        var graph = services.GetRequiredService<DomainGraph>();
        var eventKey = EventTypeKey(eventType);
        var (handlers, contextsTouched) = ResolveHandlers(graph, eventKey, boundedContextName);

        string payloadJson;
        try
        {
            payloadJson = payload is null ? "null" : JsonSerializer.Serialize(payload, TraceJson.Options);
        }
        catch (Exception ex)
        {
            payloadJson = JsonSerializer.Serialize($"<serialization failed: {ex.Message}>", TraceJson.Options);
        }

        await notifier.NotifyAsync(new DomainModelTraceMessage(
            TimestampUtc: DateTime.UtcNow,
            EventTypeFullName: eventType.FullName ?? eventType.Name,
            EventGraphKey: eventKey,
            BoundedContextName: boundedContextName,
            BoundedContextsWithMatch: contextsTouched,
            HandlerFullNames: handlers,
            PayloadJson: payloadJson)).ConfigureAwait(false);
    }

    internal static string EventTypeKey(Type eventType)
    {
        if (eventType.IsGenericType && !eventType.IsGenericTypeDefinition)
            return eventType.GetGenericTypeDefinition().FullName ?? eventType.GetGenericTypeDefinition().Name;
        return eventType.FullName ?? eventType.Name;
    }

    internal static (List<string> Handlers, List<string> Contexts) ResolveHandlers(
        DomainGraph graph,
        string eventKey,
        string? boundedContextFilter)
    {
        var handlers = new HashSet<string>(StringComparer.Ordinal);
        var contexts = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ctx in graph.BoundedContexts)
        {
            if (boundedContextFilter is not null &&
                !string.Equals(ctx.Name, boundedContextFilter, StringComparison.Ordinal))
                continue;

            foreach (var rel in MatchEventNodes(ctx.DomainEvents))
                AddFromContext(ctx.Name, rel);

            foreach (var rel in MatchEventNodes(ctx.IntegrationEvents))
                AddFromContext(ctx.Name, rel);

            foreach (var h in ctx.EventHandlers)
            {
                foreach (var handled in h.Handles)
                {
                    if (!EventKeyMatches(handled, eventKey)) continue;
                    handlers.Add(h.FullName);
                    contexts.Add(ctx.Name);
                }
            }
        }

        return (handlers.OrderBy(h => h, StringComparer.Ordinal).ToList(), contexts.OrderBy(c => c, StringComparer.Ordinal).ToList());

        IEnumerable<(DomainEventNode Node, List<string> HandledBy)> MatchEventNodes(IEnumerable<DomainEventNode> events) =>
            events.Where(e => EventKeyMatches(e.FullName, eventKey)).Select(e => (e, e.HandledBy));

        void AddFromContext(string contextName, (DomainEventNode Node, List<string> HandledBy) tuple)
        {
            contexts.Add(contextName);
            foreach (var hb in tuple.HandledBy)
                handlers.Add(hb);
        }
    }

    internal static bool EventKeyMatches(string graphFullName, string eventKey) =>
        string.Equals(graphFullName, eventKey, StringComparison.Ordinal);
}

/// <summary>
/// Payload pushed to Trace tab subscribers.
/// </summary>
public sealed record DomainModelTraceMessage(
    DateTime TimestampUtc,
    string EventTypeFullName,
    string EventGraphKey,
    string? BoundedContextName,
    IReadOnlyList<string> BoundedContextsWithMatch,
    IReadOnlyList<string> HandlerFullNames,
    string PayloadJson);

/// <summary>
/// Sends trace messages to SignalR clients.
/// </summary>
public interface IDomainModelTraceNotifier
{
    Task NotifyAsync(DomainModelTraceMessage message, CancellationToken cancellationToken = default);
}

internal sealed class DomainModelTraceNotifier : IDomainModelTraceNotifier
{
    private readonly IHubContext<DomainModelTraceHub> _hub;
    private readonly DomainModelTraceLastNotification _history;

    public DomainModelTraceNotifier(
        IHubContext<DomainModelTraceHub> hub,
        DomainModelTraceLastNotification history)
    {
        _hub = hub;
        _history = history;
    }

    public Task NotifyAsync(DomainModelTraceMessage message, CancellationToken cancellationToken = default)
    {
        _history.Record(message);
        return _hub.Clients.All.SendAsync("trace", message, cancellationToken);
    }
}

/// <summary>
/// SignalR hub for live event trace updates. No client methods required.
/// </summary>
public sealed class DomainModelTraceHub : Hub;

/// <summary>
/// In-memory ring buffer of recent trace notifications (optional diagnostics / GET endpoint).
/// </summary>
public sealed class DomainModelTraceLastNotification
{
    private readonly object _lock = new();
    private readonly List<DomainModelTraceMessage> _recent = [];
    private const int MaxItems = 100;

    public void Record(DomainModelTraceMessage message)
    {
        lock (_lock)
        {
            _recent.Insert(0, message);
            if (_recent.Count > MaxItems)
                _recent.RemoveRange(MaxItems, _recent.Count - MaxItems);
        }
    }

    public DomainModelTraceMessage? Latest
    {
        get
        {
            lock (_lock)
            {
                return _recent.Count > 0 ? _recent[0] : null;
            }
        }
    }

    public IReadOnlyList<DomainModelTraceMessage> Recent(int max = 50)
    {
        lock (_lock)
        {
            return _recent.Take(Math.Min(max, _recent.Count)).ToList();
        }
    }
}

file static class TraceJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal static class TraceJsonSerializer
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
