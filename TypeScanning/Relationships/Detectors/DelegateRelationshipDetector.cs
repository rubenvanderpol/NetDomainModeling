using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Wraps a custom detection delegate.
/// </summary>
public sealed class DelegateRelationshipDetector : IRelationshipDetector
{
    private readonly Func<RelationshipDetectionContext, IEnumerable<ScannedRelationship>> _detect;

    public DelegateRelationshipDetector(Func<RelationshipDetectionContext, IEnumerable<ScannedRelationship>> detect)
    {
        _detect = detect ?? throw new ArgumentNullException(nameof(detect));
    }

    public IEnumerable<ScannedRelationship> Detect(RelationshipDetectionContext context) => _detect(context);
}
