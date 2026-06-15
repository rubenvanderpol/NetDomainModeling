namespace TypeScanning.Relationships;

/// <summary>
/// Options for <see cref="PropertyReferenceDetector"/>.
/// </summary>
public sealed class PropertyReferenceOptions
{
    public bool CollectionsOnly { get; set; }
    public bool ExcludeCollections { get; set; }
    public bool TargetMustBeKnown { get; set; } = true;
    public string[]? TargetCategories { get; set; }
}

/// <summary>
/// Options for <see cref="IdPropertyReferenceDetector"/>.
/// </summary>
public sealed class IdPropertyReferenceOptions
{
    public string Suffix { get; set; } = "Id";
    public string[] PreferTargetCategories { get; set; } = [];
}

/// <summary>
/// Options for IL-based detectors.
/// </summary>
public sealed class IlTargetOptions
{
    public required string[] TargetCategories { get; init; }
    public string? LabelTemplate { get; init; }
}

/// <summary>
/// Options for <see cref="InterfaceGenericArgumentDetector"/>.
/// </summary>
public sealed class InterfaceGenericArgumentOptions
{
    public string[]? TargetCategories { get; set; }
    public bool TargetMustBeKnown { get; set; } = true;
}

/// <summary>
/// Options for <see cref="CategoryPropertyReferenceDetector"/>.
/// </summary>
public sealed class CategoryPropertyReferenceOptions
{
    public required string TargetCategory { get; init; }
    public bool CollectionsOnly { get; set; }
    public bool ExcludeCollections { get; set; }
}
