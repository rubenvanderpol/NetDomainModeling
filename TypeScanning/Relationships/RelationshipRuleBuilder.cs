using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Fluent builder for a consumer-named relationship rule and its detection strategy.
/// </summary>
public sealed class RelationshipRuleBuilder
{
    private readonly string _name;
    private readonly List<string> _sourceCategories = [];
    private IRelationshipDetector? _detector;

    internal RelationshipRuleBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>
    /// Limits detection to source types in the given categories.
    /// </summary>
    public RelationshipRuleBuilder FromCategories(params string[] categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        if (categories.Length == 0)
            throw new ArgumentException("At least one source category is required.", nameof(categories));

        _sourceCategories.Clear();
        _sourceCategories.AddRange(categories);
        return this;
    }

    /// <summary>
    /// Detects relationships from object-reference properties.
    /// </summary>
    public RelationshipRuleBuilder PropertyReference(Action<PropertyReferenceOptions>? configure = null)
    {
        var options = new PropertyReferenceOptions();
        configure?.Invoke(options);
        _detector = new PropertyReferenceDetector(_name, SourceCategoriesArray(), options);
        return this;
    }

    /// <summary>
    /// Detects relationships from ID-style properties (e.g. <c>CustomerId</c>).
    /// </summary>
    public RelationshipRuleBuilder IdPropertyReference(Action<IdPropertyReferenceOptions>? configure = null)
    {
        var options = new IdPropertyReferenceOptions();
        configure?.Invoke(options);
        _detector = new IdPropertyReferenceDetector(_name, SourceCategoriesArray(), options);
        return this;
    }

    /// <summary>
    /// Detects relationships where source types have properties referencing types in a target category.
    /// </summary>
    public RelationshipRuleBuilder PropertyReferencesCategory(string targetCategory, Action<CategoryPropertyReferenceOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetCategory);
        var options = new CategoryPropertyReferenceOptions { TargetCategory = targetCategory };
        configure?.Invoke(options);
        _detector = new CategoryPropertyReferenceDetector(_name, SourceCategoriesArray(), options);
        return this;
    }

    /// <summary>
    /// Detects relationships where source types instantiate target types via IL <c>newobj</c>.
    /// </summary>
    public RelationshipRuleBuilder IlInstantiatesTypesInCategories(
        string[] targetCategories,
        string? labelTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(targetCategories);
        _detector = new IlInstantiationDetector(_name, SourceCategoriesArray(), new IlTargetOptions
        {
            TargetCategories = targetCategories,
            LabelTemplate = labelTemplate
        });
        return this;
    }

    /// <summary>
    /// Detects relationships where source types call instance methods on types in target categories.
    /// </summary>
    public RelationshipRuleBuilder IlCallsTypesInCategories(
        string[] targetCategories,
        string? labelTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(targetCategories);
        _detector = new IlInstanceCallDetector(_name, SourceCategoriesArray(), new IlTargetOptions
        {
            TargetCategories = targetCategories,
            LabelTemplate = labelTemplate
        });
        return this;
    }

    /// <summary>
    /// Detects relationships from generic arguments of implemented interfaces.
    /// </summary>
    public RelationshipRuleBuilder InterfaceGenericArguments(Action<InterfaceGenericArgumentOptions>? configure = null)
    {
        var options = new InterfaceGenericArgumentOptions();
        configure?.Invoke(options);
        _detector = new InterfaceGenericArgumentDetector(_name, SourceCategoriesArray(), options);
        return this;
    }

    /// <summary>
    /// Uses a custom detector implementation.
    /// </summary>
    public RelationshipRuleBuilder DetectUsing(IRelationshipDetector detector)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        return this;
    }

    /// <summary>
    /// Uses a custom detection delegate.
    /// </summary>
    public RelationshipRuleBuilder DetectUsing(Func<RelationshipDetectionContext, IEnumerable<ScannedRelationship>> detect)
    {
        ArgumentNullException.ThrowIfNull(detect);
        _detector = new DelegateRelationshipDetector(detect);
        return this;
    }

    internal RelationshipRule Build()
    {
        if (_sourceCategories.Count == 0)
            throw new InvalidOperationException($"Relationship '{_name}' requires at least one source category. Call FromCategories first.");

        if (_detector is null)
            throw new InvalidOperationException($"Relationship '{_name}' has no detection strategy configured.");

        return new RelationshipRule(_name, _sourceCategories.ToArray(), _detector);
    }

    private string[] SourceCategoriesArray() => _sourceCategories.ToArray();
}
