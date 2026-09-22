using System.Text.Json.Serialization;

namespace PamAutomation.Browser;

internal sealed class PendingGridRowSnapshot
{
    [JsonPropertyName("cells")]
    public PendingGridCellSnapshot[] Cells { get; init; } = [];
}

internal sealed class PendingGridCellSnapshot
{
    [JsonPropertyName("columnId")]
    public string ColumnId { get; init; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;
}
