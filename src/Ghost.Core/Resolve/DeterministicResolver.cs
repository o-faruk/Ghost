using System.Diagnostics;
using Ghost.Core.Models;

namespace Ghost.Core.Resolve;

/// <summary>
/// Tier 1: no network, no model, target under 15ms. Scores every element via Scoring.Score and
/// accepts only when the top score clears AcceptScore with at least AcceptMargin over the
/// runner-up — a confident-but-ambiguous match is a failure, not a success, and escalates.
/// </summary>
public sealed class DeterministicResolver : IResolver
{
    private readonly double _acceptScore;
    private readonly double _acceptMargin;

    public DeterministicResolver(double acceptScore = 0.80, double acceptMargin = 0.15)
    {
        _acceptScore = acceptScore;
        _acceptMargin = acceptMargin;
    }

    public ResolutionTier Tier => ResolutionTier.Deterministic;

    public Task<Resolution?> TryResolveAsync(PlanStep step, ScreenSnapshot snapshot, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var windowArea = snapshot.WindowBounds.Area;

        var scored = snapshot.Elements
            .Select(e => (Element: e, Score: Scoring.Score(step.TargetDescription, e, step.Action, windowArea)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scored.Count == 0)
        {
            return Task.FromResult<Resolution?>(null);
        }

        var top = scored[0];
        var second = scored.Count > 1 ? scored[1].Score : 0.0;

        if (top.Score < _acceptScore || (top.Score - second) < _acceptMargin)
        {
            return Task.FromResult<Resolution?>(null);
        }

        return Task.FromResult<Resolution?>(new Resolution
        {
            Tier = ResolutionTier.Deterministic,
            Element = top.Element,
            Bounds = top.Element.Bounds,
            Confidence = top.Score,
            Rationale = $"deterministic top score {top.Score:0.00} vs second {second:0.00} for \"{top.Element.Name}\"",
            Duration = sw.Elapsed,
        });
    }
}
