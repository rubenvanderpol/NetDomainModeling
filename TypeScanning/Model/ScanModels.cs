using TypeScanning;

namespace TypeScanning.Model;

/// <summary>
/// A property discovered on a scanned type.
/// </summary>
public sealed class ScannedProperty
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public bool IsCollection { get; init; }
    public string? ReferenceTypeName { get; init; }
}

/// <summary>
/// A method discovered on a scanned type.
/// </summary>
public sealed class ScannedMethod
{
    public required string Name { get; init; }
    public required string ReturnTypeName { get; init; }
    public IReadOnlyList<ScannedMethodParameter> Parameters { get; init; } = [];
}

/// <summary>
/// A method parameter on a scanned type.
/// </summary>
public sealed class ScannedMethodParameter
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
}

/// <summary>
/// A relationship inferred between two scanned types.
/// </summary>
public sealed class ScannedRelationship
{
    public required string SourceType { get; init; }
    public required string TargetType { get; init; }

    /// <summary>
    /// Consumer-defined relationship type name (e.g. "Has", "Emits", "manages").
    /// </summary>
    public required string Kind { get; init; }

    public string? Label { get; init; }
}

/// <summary>
/// A type discovered and categorized by the scanner.
/// </summary>
public sealed class ScannedType
{
    public required Type ClrType { get; init; }
    public required string Category { get; init; }
    public string Name => TypeDisplayNames.ShortName(ClrType);
    public string FullName => ClrType.FullName!;
    public string? Layer { get; init; }
    public IReadOnlyList<ScannedProperty> Properties { get; init; } = [];
    public IReadOnlyList<ScannedMethod> Methods { get; init; } = [];
}

/// <summary>
/// Scan output for a single context.
/// </summary>
public sealed class ScanResult
{
    public required string ContextName { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<ScannedType>> Categories { get; init; }
    public required IReadOnlyList<Type> ConcreteTypes { get; init; }
    public required IReadOnlyList<Type> AllExportedTypes { get; init; }
    public required HashSet<string> KnownTypeFullNames { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<Type>> AllMatchedTypes { get; init; }
    public IReadOnlyList<ScannedRelationship> Relationships { get; init; } = [];
}

/// <summary>
/// Scan output for all configured contexts.
/// </summary>
public sealed class ScanGraph
{
    public ScanGraph(IReadOnlyList<ScanResult> contexts) => Contexts = contexts;
    public IReadOnlyList<ScanResult> Contexts { get; }
}
