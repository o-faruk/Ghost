namespace Ghost.Core.Models;

public enum ResolutionTier
{
    Deterministic,
    LlmDisambiguation,
    Ocr,
    Vision,
    Failed,
}

/// <summary>The outcome of resolving a PlanStep against a ScreenSnapshot to a specific rectangle.</summary>
public sealed record Resolution
{
    public required ResolutionTier Tier { get; init; }

    /// <summary>Null when Tier == Vision or Failed.</summary>
    public UiElement? Element { get; init; }

    public PhysicalRect? Bounds { get; init; }

    public required double Confidence { get; init; }

    /// <summary>Human-readable explanation, used in logs and the eval report.</summary>
    public required string Rationale { get; init; }

    public required TimeSpan Duration { get; init; }

    public int LlmCallCount { get; init; }

    public decimal EstimatedCostUsd { get; init; }
}
