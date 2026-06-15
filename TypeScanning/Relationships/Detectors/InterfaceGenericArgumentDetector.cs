using System.Reflection;
using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships from generic arguments of implemented interfaces on source types.
/// </summary>
public sealed class InterfaceGenericArgumentDetector : IRelationshipDetector
{
    private readonly string _relationshipName;
    private readonly string[] _sourceCategories;
    private readonly InterfaceGenericArgumentOptions _options;

    public InterfaceGenericArgumentDetector(
        string relationshipName,
        string[] sourceCategories,
        InterfaceGenericArgumentOptions options)
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
        HashSet<string>? allowedTargets = null;
        if (_options.TargetCategories is { Length: > 0 })
            allowedTargets = context.GetFullNamesInCategories(_options.TargetCategories);

        foreach (var source in context.GetTypesInCategories(_sourceCategories))
        {
            var handled = new HashSet<string>(StringComparer.Ordinal);

            foreach (var iface in source.ClrType.GetInterfaces().Where(i => i.IsGenericType))
            {
                foreach (var arg in iface.GetGenericArguments())
                {
                    var fullName = arg.FullName;
                    if (fullName is null || !handled.Add(fullName))
                        continue;
                    if (_options.TargetMustBeKnown && !context.KnownTypeFullNames.Contains(fullName))
                        continue;
                    if (allowedTargets is not null && !allowedTargets.Contains(fullName))
                        continue;

                    yield return new ScannedRelationship
                    {
                        Kind = _relationshipName,
                        SourceType = source.FullName,
                        TargetType = fullName,
                        Label = "handles"
                    };
                }
            }
        }
    }
}
