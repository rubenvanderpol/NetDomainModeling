using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Disk-backed overrides and notes for individual relationship edges on diagrams.
/// Stored as <c>relationship-metadata.json</c> under <see cref="DomainModelOptions.MetadataStoragePath"/>.
/// Keys match the diagram convention: <c>sourceFullName|targetFullName|RelationshipKind</c>.
/// </summary>
internal sealed class RelationshipMetadataDocument
{
    [JsonPropertyName("edges")]
    public Dictionary<string, RelationshipEdgeMetadata> Edges { get; set; } =
        new Dictionary<string, RelationshipEdgeMetadata>(StringComparer.Ordinal);
}

/// <summary>
/// User-editable metadata for one directed relationship edge.
/// </summary>
internal sealed class RelationshipEdgeMetadata
{
    /// <summary>Optional free-form description shown in edge inspector panels.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>When true, the edge is not drawn on the main diagram (per-edge hide).</summary>
    [JsonPropertyName("hiddenOnDiagram")]
    public bool? HiddenOnDiagram { get; set; }

    /// <summary>
    /// Replaces the short text drawn on the edge (scanner label / kind name).
    /// Does not change the underlying domain <see cref="Graph.Relationship.Label"/>.
    /// </summary>
    [JsonPropertyName("labelOverride")]
    public string? LabelOverride { get; set; }
}
