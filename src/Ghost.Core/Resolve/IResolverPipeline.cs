using Ghost.Core.Models;

namespace Ghost.Core.Resolve;

/// <summary>
/// Resolves a PlanStep to a specific on-screen rectangle against a ScreenSnapshot, escalating
/// through tiers (Deterministic -> LlmDisambiguation -> Ocr -> Vision) until one accepts.
/// Implemented starting Phase 1; Ghost.Eval depends only on this interface so fixtures can run
/// against a fake before the real pipeline exists.
/// </summary>
public interface IResolverPipeline
{
    Task<Resolution> ResolveAsync(PlanStep step, ScreenSnapshot snapshot, CancellationToken ct);
}
