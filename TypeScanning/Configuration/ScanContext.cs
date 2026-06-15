using System.Reflection;
using TypeScanning.Convention;
using TypeScanning.Relationships;

namespace TypeScanning.Configuration;

/// <summary>
/// Options controlling how a named category participates in scanning.
/// </summary>
public sealed class TypeCategoryOptions
{
    /// <summary>
    /// When true, types in <see cref="ScanContext.ExternallyOwnedAssemblies"/> are excluded from this category's listed types.
    /// </summary>
    public bool ExcludeWhenExternallyOwned { get; init; } = true;

    /// <summary>
    /// When true, types matching this category in externally-owned assemblies still contribute to the known-type set
    /// used for reference detection.
    /// </summary>
    public bool IncludeExternallyOwnedInKnownTypes { get; init; }
}

/// <summary>
/// A named type category with its matching convention.
/// </summary>
public sealed class TypeCategory
{
    public string Name { get; }
    public TypeConventionBuilder Convention { get; }
    public TypeCategoryOptions Options { get; }

    public TypeCategory(string name, TypeConventionBuilder convention, TypeCategoryOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(convention);
        Name = name;
        Convention = convention;
        Options = options ?? new TypeCategoryOptions();
    }
}

/// <summary>
/// Immutable scan configuration for a single context (bounded context, module, etc.).
/// </summary>
public sealed class ScanContext
{
    public string Name { get; }
    public IReadOnlyList<Assembly> Assemblies { get; }
    public IReadOnlyList<TypeCategory> Categories { get; }
    public HashSet<Assembly> ExternallyOwnedAssemblies { get; }
    public Func<Type, string?>? LayerResolver { get; }
    public IReadOnlyList<RelationshipRule> RelationshipRules { get; }

    public ScanContext(
        string name,
        IReadOnlyList<Assembly> assemblies,
        IReadOnlyList<TypeCategory> categories,
        HashSet<Assembly>? externallyOwnedAssemblies = null,
        Func<Type, string?>? layerResolver = null,
        IReadOnlyList<RelationshipRule>? relationshipRules = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(assemblies);
        ArgumentNullException.ThrowIfNull(categories);
        Name = name;
        Assemblies = assemblies;
        Categories = categories;
        ExternallyOwnedAssemblies = externallyOwnedAssemblies ?? [];
        LayerResolver = layerResolver;
        RelationshipRules = relationshipRules ?? [];
    }
}
