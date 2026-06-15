using TypeScanning.Model;

namespace TypeScanning.Relationships;

/// <summary>
/// Detects relationships between scanned types within a single scan context.
/// </summary>
public interface IRelationshipDetector
{
    IEnumerable<ScannedRelationship> Detect(RelationshipDetectionContext context);
}
