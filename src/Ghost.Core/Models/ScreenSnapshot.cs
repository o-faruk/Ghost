namespace Ghost.Core.Models;

/// <summary>
/// A single point-in-time capture of the foreground window's accessibility tree.
/// </summary>
public sealed record ScreenSnapshot
{
    public required nint WindowHandle { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public required PhysicalRect WindowBounds { get; init; }
    public required IReadOnlyList<UiElement> Elements { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }

    /// <summary>
    /// SHA-256 over "ControlType|Name|Bounds" for every element, ordered by Depth then Left then Top.
    /// Used by SettleDetector to know when the UI has stopped changing.
    /// </summary>
    public required string StructureHash { get; init; }

    public required TimeSpan CaptureDuration { get; init; }
}
