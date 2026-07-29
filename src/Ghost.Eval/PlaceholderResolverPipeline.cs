using Ghost.Core.Models;
using Ghost.Core.Resolve;

namespace Ghost.Eval;

/// <summary>
/// Stand-in for the real ResolverPipeline (DeterministicResolver + LlmResolver), which lands
/// in Phase 1/3. Does exact, case-insensitive Name matching only, just enough to exercise the
/// eval report's plumbing end to end against hand-written fixtures in Phase 0.
/// </summary>
public sealed class PlaceholderResolverPipeline : IResolverPipeline
{
    public Task<Resolution> ResolveAsync(PlanStep step, ScreenSnapshot snapshot, CancellationToken ct)
    {
        var query = step.TargetDescription.Trim().ToLowerInvariant();

        var match = snapshot.Elements
            .FirstOrDefault(e => query.Contains(e.Name.Trim().ToLowerInvariant()) && e.Name.Length > 0);

        var resolution = match is null
            ? new Resolution
            {
                Tier = ResolutionTier.Failed,
                Confidence = 0.0,
                Rationale = "placeholder resolver found no element whose Name appears in the target description",
                Duration = TimeSpan.Zero,
            }
            : new Resolution
            {
                Tier = ResolutionTier.Deterministic,
                Element = match,
                Bounds = match.Bounds,
                Confidence = 1.0,
                Rationale = $"placeholder exact-name match on \"{match.Name}\"",
                Duration = TimeSpan.Zero,
            };

        return Task.FromResult(resolution);
    }
}
