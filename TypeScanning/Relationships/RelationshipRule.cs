namespace TypeScanning.Relationships;

/// <summary>
/// A consumer-named relationship with source scope and a detection strategy.
/// </summary>
public sealed class RelationshipRule
{
    public string Name { get; }
    public IReadOnlyList<string> SourceCategories { get; }
    public IRelationshipDetector Detector { get; }

    public RelationshipRule(string name, IReadOnlyList<string> sourceCategories, IRelationshipDetector detector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(sourceCategories);
        ArgumentNullException.ThrowIfNull(detector);

        if (sourceCategories.Count == 0)
            throw new ArgumentException("At least one source category is required.", nameof(sourceCategories));

        Name = name;
        SourceCategories = sourceCategories;
        Detector = detector;
    }
}
