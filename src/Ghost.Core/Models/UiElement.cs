namespace Ghost.Core.Models;

/// <summary>
/// A single node from the UIA accessibility tree, flattened and cached at capture time.
/// </summary>
public sealed record UiElement
{
    /// <summary>Stable only within the ScreenSnapshot it was captured in. Never persist or compare across captures.</summary>
    public required string RuntimeId { get; init; }

    public required string Name { get; init; }

    public required string ControlType { get; init; }

    public string? AutomationId { get; init; }
    public string? HelpText { get; init; }

    /// <summary>e.g. "Alt+F".</summary>
    public string? AccessKey { get; init; }

    public string? ClassName { get; init; }

    /// <summary>ValuePattern.Value if the element supports it.</summary>
    public string? Value { get; init; }

    public required PhysicalRect Bounds { get; init; }
    public required bool IsEnabled { get; init; }
    public required bool IsOffscreen { get; init; }
    public required bool IsKeyboardFocusable { get; init; }
    public required int Depth { get; init; }
    public string? ParentRuntimeId { get; init; }
}
