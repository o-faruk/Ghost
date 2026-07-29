using Ghost.Core.Models;

namespace Ghost.Eval;

/// <summary>One row of the committed eval suite: a snapshot plus the step to resolve against it.</summary>
public sealed record FixtureCase
{
    public required string Id { get; init; }

    /// <summary>Path to the snapshot file, relative to the fixture's own directory.</summary>
    public required string Snapshot { get; init; }

    public required StepAction Action { get; init; }
    public required string TargetDescription { get; init; }
    public string? ExpectedRuntimeId { get; init; }
    public required PhysicalRect ExpectedRect { get; init; }
    public string? Notes { get; init; }
}

public sealed record FixtureResult
{
    public required FixtureCase Case { get; init; }
    public required string App { get; init; }
    public required Resolution Resolution { get; init; }
    public required bool CenterInRect { get; init; }
    public required bool RuntimeIdMatched { get; init; }

    public bool Passed => CenterInRect;
}
