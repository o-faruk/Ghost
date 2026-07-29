namespace Ghost.Core.Models;

public enum StepAction
{
    Click,
    DoubleClick,
    RightClick,
    Type,
    Hover,
    Scroll,
    Wait,
}

/// <summary>A single, individually-clickable step within a Plan.</summary>
public sealed record PlanStep
{
    /// <summary>1-based.</summary>
    public required int Index { get; init; }

    public required StepAction Action { get; init; }

    /// <summary>Describes the element for the resolver, e.g. "the File menu in the menu bar".</summary>
    public required string TargetDescription { get; init; }

    /// <summary>Shown in the tooltip, imperative and under 6 words, e.g. "Click File".</summary>
    public required string Instruction { get; init; }

    /// <summary>Used by the verifier, e.g. "the File menu opens".</summary>
    public required string ExpectedOutcome { get; init; }

    /// <summary>Only set when Action == StepAction.Type.</summary>
    public string? TypeText { get; init; }
}

/// <summary>A sequence of steps produced by the planner to satisfy a user goal.</summary>
public sealed record Plan
{
    public required string Goal { get; init; }
    public required IReadOnlyList<PlanStep> Steps { get; init; }

    /// <summary>e.g. "I'm not certain this app has that".</summary>
    public string? Caveat { get; init; }

    /// <summary>False means the goal cannot be achieved in this application; explain and stop.</summary>
    public required bool IsAchievable { get; init; }
}
