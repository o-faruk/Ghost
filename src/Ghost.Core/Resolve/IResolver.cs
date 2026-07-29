using Ghost.Core.Models;

namespace Ghost.Core.Resolve;

/// <summary>
/// A single resolution tier. Returns null to escalate to the next tier, or a Resolution when
/// this tier is confident enough to accept.
/// </summary>
public interface IResolver
{
    ResolutionTier Tier { get; }

    Task<Resolution?> TryResolveAsync(PlanStep step, ScreenSnapshot snapshot, CancellationToken ct);
}
