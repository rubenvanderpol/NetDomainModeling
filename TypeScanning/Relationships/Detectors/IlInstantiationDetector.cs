using TypeScanning.Discovery;
using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships where source types instantiate target types via IL <c>newobj</c>.
/// </summary>
public sealed class IlInstantiationDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly IlTargetOptions _options;

    public IlInstantiationDetector(string relationshipName, string[] sourceCategories, IlTargetOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipName);
        ArgumentNullException.ThrowIfNull(sourceCategories);
        ArgumentNullException.ThrowIfNull(options);
        _relationshipName = relationshipName;
        _sourceCategories = sourceCategories;
        _options = options;
    }

    public IEnumerable<ScannedRelationship> Detect(RelationshipDetectionContext context)
    {
        var targetFullNames = context.GetFullNamesInCategories(_options.TargetCategories);
        if (targetFullNames.Count == 0)
            yield break;

        foreach (var source in context.GetTypesInCategories(_sourceCategories))
        {
            var creations = IlBodyAnalyzer.DetectTypeCreations(source.ClrType, targetFullNames);
            foreach (var (targetType, methods) in creations)
            {
                foreach (var method in methods)
                {
                    yield return new ScannedRelationship
                    {
                        Kind = _relationshipName,
                        SourceType = source.FullName,
                        TargetType = targetType,
                        Label = FormatLabel(method)
                    };
                }
            }
        }
    }

    private string? FormatLabel(string methodName)
    {
        if (string.IsNullOrWhiteSpace(_options.LabelTemplate))
            return null;

        return _options.LabelTemplate.Replace("{Method}", methodName, StringComparison.Ordinal);
    }
}
