using System.Text.Json.Serialization;

namespace DomainModeling.AspNetCore;

/// <summary>
/// Per-surface bounded context visibility for the domain explorer UI, persisted as JSON on disk.
/// </summary>
public sealed class BoundedContextUiSelections
{
    [JsonPropertyName("explorer")]
    public List<string>? Explorer { get; set; }

    [JsonPropertyName("diagram")]
    public List<string>? Diagram { get; set; }

    [JsonPropertyName("featureEditorPalette")]
    public List<string>? FeatureEditorPalette { get; set; }
}
