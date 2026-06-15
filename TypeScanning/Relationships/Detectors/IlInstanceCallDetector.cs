using TypeScanning.Discovery;
using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships where source types call instance methods on target types via IL.
/// </summary>
public sealed class IlInstanceCallDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly IlTargetOptions _options;

    public IlInstanceCallDetector(string relationshipName, string[] sourceCategories, IlTargetOptions options)
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

        var seen = new HashSet<(string Source, string Target, string Method)>();

        foreach (var source in context.GetTypesInCategories(_sourceCategories))
        {
            foreach (var (targetFullName, methodName) in IlBodyAnalyzer.DetectInstanceCallsOnTypes(source.ClrType, targetFullNames))
            {
                if (!seen.Add((source.FullName, targetFullName, methodName)))
                    continue;

                yield return new ScannedRelationship
                {
                    Kind = _relationshipName,
                    SourceType = source.FullName,
                    TargetType = targetFullName,
                    Label = FormatLabel(methodName)
                };
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
