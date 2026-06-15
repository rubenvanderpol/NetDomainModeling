using System.Reflection;
using TypeScanning.Convention;
using TypeScanning.Relationships;

namespace TypeScanning.Configuration;

/// <summary>
/// Fluent builder for a single scan context: assemblies, layers, and named type categories.
/// </summary>
public sealed class ScanContextBuilder
{
    private string? _name;
    private Assembly? _domainAssembly;
    private Assembly? _applicationAssembly;
    private Assembly? _infrastructureAssembly;
    private readonly List<Assembly> _additionalAssemblies = [];
    private readonly List<Assembly> _sharedAssemblies = [];
    private readonly HashSet<Assembly> _externallyOwnedAssemblies = [];
    private readonly Dictionary<string, TypeCategory> _categories = new(StringComparer.Ordinal);
    private readonly List<RelationshipRule> _relationshipRules = [];

    /// <summary>
    /// Sets the context name (e.g. bounded context or module name).
    /// </summary>
    public ScanContextBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    /// <summary>
    /// Adds assemblies to scan.
    /// </summary>
    public ScanContextBuilder WithAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        foreach (var assembly in assemblies)
            _additionalAssemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assemblies)));
        return this;
    }

    /// <summary>
    /// Maps an assembly to a layer label returned by <see cref="ScanContext.LayerResolver"/>.
    /// </summary>
    public ScanContextBuilder WithLayer(string layerName, Assembly assembly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerName);
        ArgumentNullException.ThrowIfNull(assembly);

        return layerName switch
        {
            "Domain" => WithDomainAssembly(assembly),
            "Application" => WithApplicationAssembly(assembly),
            "Infrastructure" => WithInfrastructureAssembly(assembly),
            _ => throw new ArgumentException($"Unknown layer name '{layerName}'. Use Domain, Application, or Infrastructure.", nameof(layerName))
        };
    }

    /// <summary>
    /// Sets the assembly associated with the domain layer.
    /// </summary>
    public ScanContextBuilder WithDomainAssembly(Assembly assembly)
    {
        _domainAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Sets the assembly associated with the application layer.
    /// </summary>
    public ScanContextBuilder WithApplicationAssembly(Assembly assembly)
    {
        _applicationAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Sets the assembly associated with the infrastructure layer.
    /// </summary>
    public ScanContextBuilder WithInfrastructureAssembly(Assembly assembly)
    {
        _infrastructureAssembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        return this;
    }

    /// <summary>
    /// Adds a shared assembly scanned for cross-references but listed under another context.
    /// </summary>
    public ScanContextBuilder WithSharedAssembly(Assembly assembly)
    {
        _sharedAssemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assembly)));
        return this;
    }

    /// <summary>
    /// Marks an assembly as externally owned: types are scanned for IL and references but excluded from category listings.
    /// </summary>
    public ScanContextBuilder WithExternallyOwnedAssembly(Assembly assembly)
    {
        _externallyOwnedAssemblies.Add(assembly ?? throw new ArgumentNullException(nameof(assembly)));
        return this;
    }

    /// <summary>
    /// Registers a named type category with a convention.
    /// </summary>
    public ScanContextBuilder WithCategory(string name, Action<TypeConventionBuilder> configure, TypeCategoryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var convention = new TypeConventionBuilder();
        configure(convention);
        _categories[name] = new TypeCategory(name, convention, options);
        return this;
    }

    /// <summary>
    /// Registers a named type category with an existing convention builder instance.
    /// </summary>
    internal ScanContextBuilder WithCategory(string name, TypeConventionBuilder convention, TypeCategoryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(convention);
        _categories[name] = new TypeCategory(name, convention, options);
        return this;
    }

    /// <summary>
    /// Registers a consumer-named relationship with a configurable detection strategy.
    /// </summary>
    public ScanContextBuilder WithRelationship(string name, Action<RelationshipRuleBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new RelationshipRuleBuilder(name);
        configure(builder);
        _relationshipRules.Add(builder.Build());
        return this;
    }

    internal ScanContext Build()
    {
        if (string.IsNullOrWhiteSpace(_name))
            throw new InvalidOperationException("Context name is required. Call WithName before Build.");

        var assemblies = new List<Assembly>();
        if (_domainAssembly is not null) assemblies.Add(_domainAssembly);
        if (_applicationAssembly is not null) assemblies.Add(_applicationAssembly);
        if (_infrastructureAssembly is not null) assemblies.Add(_infrastructureAssembly);
        assemblies.AddRange(_additionalAssemblies);
        assemblies.AddRange(_sharedAssemblies);
        assemblies.AddRange(_externallyOwnedAssemblies);

        return new ScanContext(
            _name,
            assemblies.Distinct().ToList(),
            _categories.Values.ToList(),
            _externallyOwnedAssemblies,
            GetLayer,
            _relationshipRules);
    }

    private string? GetLayer(Type type)
    {
        var assembly = type.Assembly;
        if (_domainAssembly is not null && assembly == _domainAssembly) return "Domain";
        if (_applicationAssembly is not null && assembly == _applicationAssembly) return "Application";
        if (_infrastructureAssembly is not null && assembly == _infrastructureAssembly) return "Infrastructure";
        return null;
    }
}
