using TypeScanning.Discovery;
using TypeScanning.Model;

namespace TypeScanning.Configuration;

/// <summary>
/// Top-level builder for one or more scan contexts.
/// </summary>
public sealed class ScanBuilder
{
    private readonly List<ScanContext> _contexts = [];

    /// <summary>
    /// Creates a new scan builder.
    /// </summary>
    public static ScanBuilder Create(Action<ScanBuilder>? configure = null)
    {
        var builder = new ScanBuilder();
        configure?.Invoke(builder);
        return builder;
    }

    /// <summary>
    /// Adds a scan context.
    /// </summary>
    public ScanBuilder WithContext(string name, Action<ScanContextBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var contextBuilder = new ScanContextBuilder().WithName(name);
        configure(contextBuilder);
        _contexts.Add(contextBuilder.Build());
        return this;
    }

    /// <summary>
    /// Adds a pre-built scan context.
    /// </summary>
    internal ScanBuilder WithContext(ScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _contexts.Add(context);
        return this;
    }

    /// <summary>
    /// Scans all configured contexts.
    /// </summary>
    public ScanGraph Build()
    {
        if (_contexts.Count == 0)
            throw new InvalidOperationException("At least one scan context is required.");

        var results = _contexts
            .Select(ctx => new TypeScanner(ctx).Scan())
            .ToList();

        return new ScanGraph(results);
    }
}
