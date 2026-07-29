using Ghost.Core.Models;

namespace Ghost.Core.Resolve;

/// <summary>Runs resolvers in order, escalating to the next tier whenever one doesn't accept.</summary>
public sealed class ResolverPipeline : IResolverPipeline
{
    private readonly IReadOnlyList<IResolver> _resolvers;

    public ResolverPipeline(IEnumerable<IResolver> resolvers)
    {
        _resolvers = resolvers.ToList();
    }

    public async Task<Resolution> ResolveAsync(PlanStep step, ScreenSnapshot snapshot, CancellationToken ct)
    {
        foreach (var resolver in _resolvers)
        {
            var result = await resolver.TryResolveAsync(step, snapshot, ct);
            if (result is not null)
            {
                return result;
            }
        }

        return new Resolution
        {
            Tier = ResolutionTier.Failed,
            Confidence = 0.0,
            Rationale = "no resolver tier accepted",
            Duration = TimeSpan.Zero,
        };
    }
}
