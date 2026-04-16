using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Persisted main diagram layout (single document for the explorer diagram).
/// Stored as <c>diagram-layout.json</c> under <see cref="DomainModelOptions.DiagramLayoutStoragePath"/>.
/// </summary>
internal sealed class DiagramLayoutDocument
{
    [JsonPropertyName("positions")]
    public Dictionary<string, DiagramNodePosition> Positions { get; set; } = [];

    [JsonPropertyName("viewport")]
    public DiagramViewport? Viewport { get; set; }

    [JsonPropertyName("hiddenKinds")]
    public List<string> HiddenKinds { get; set; } = [];

    [JsonPropertyName("hiddenEdgeKinds")]
    public List<string> HiddenEdgeKinds { get; set; } = [];

    /// <summary>
    /// Node ids (type full names) hidden individually on the main diagram, in addition to kind/edge filters.
    /// </summary>
    [JsonPropertyName("hiddenNodeIds")]
    public List<string> HiddenNodeIds { get; set; } = [];

    [JsonPropertyName("showAliases")]
    public bool? ShowAliases { get; set; }

    [JsonPropertyName("showLayers")]
    public bool? ShowLayers { get; set; }

    /// <summary>
    /// User-defined waypoints for edges, keyed by <c>"sourceType|targetType|kind"</c>.
    /// Each value is an ordered list of intermediate points the edge passes through.
    /// </summary>
    [JsonPropertyName("edgeWaypoints")]
    public Dictionary<string, List<DiagramNodePosition>>? EdgeWaypoints { get; set; }
}

internal sealed class DiagramNodePosition
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }
}

internal sealed class DiagramViewport
{
    [JsonPropertyName("zoom")]
    public double Zoom { get; set; }

    [JsonPropertyName("panX")]
    public double PanX { get; set; }

    [JsonPropertyName("panY")]
    public double PanY { get; set; }
}
