using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Persisted diagram layout for one bounded context (or merged context view).
/// Serialized to JSON under <see cref="DomainModelOptions.DiagramLayoutStoragePath"/>.
/// </summary>
internal sealed class DiagramContextLayout
{
    [JsonPropertyName("positions")]
    public Dictionary<string, DiagramNodePosition> Positions { get; set; } = [];

    [JsonPropertyName("viewport")]
    public DiagramViewport? Viewport { get; set; }

    [JsonPropertyName("hiddenKinds")]
    public List<string> HiddenKinds { get; set; } = [];

    [JsonPropertyName("hiddenEdgeKinds")]
    public List<string> HiddenEdgeKinds { get; set; } = [];

    [JsonPropertyName("showAliases")]
    public bool? ShowAliases { get; set; }

    [JsonPropertyName("showLayers")]
    public bool? ShowLayers { get; set; }
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
