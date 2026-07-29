using Ghost.Core.Models;
using Ghost.Core.Resolve;

namespace Ghost.Eval;

/// <summary>Loads every *.case.json fixture under a root directory, replays it offline against a resolver, and reports pass/fail.</summary>
public sealed class FixtureRunner
{
    private readonly IResolverPipeline _resolver;

    public FixtureRunner(IResolverPipeline resolver)
    {
        _resolver = resolver;
    }

    public async Task<IReadOnlyList<FixtureResult>> RunAllAsync(string fixturesRoot, CancellationToken ct)
    {
        var results = new List<FixtureResult>();

        foreach (var appDir in Directory.EnumerateDirectories(fixturesRoot).OrderBy(d => d))
        {
            var app = Path.GetFileName(appDir);

            foreach (var casePath in Directory.EnumerateFiles(appDir, "*.case.json").OrderBy(f => f))
            {
                var fixtureCase = JsonIo.ReadRequired<FixtureCase>(casePath);
                var snapshotPath = Path.Combine(appDir, fixtureCase.Snapshot);
                var snapshot = JsonIo.ReadRequired<ScreenSnapshot>(snapshotPath);

                var step = new PlanStep
                {
                    Index = 1,
                    Action = fixtureCase.Action,
                    TargetDescription = fixtureCase.TargetDescription,
                    Instruction = fixtureCase.TargetDescription,
                    ExpectedOutcome = "",
                };

                var resolution = await _resolver.ResolveAsync(step, snapshot, ct);

                var centerInRect = resolution.Bounds is { } b && fixtureCase.ExpectedRect.Contains(b.Center);
                var runtimeIdMatched = fixtureCase.ExpectedRuntimeId is not null &&
                    resolution.Element?.RuntimeId == fixtureCase.ExpectedRuntimeId;

                results.Add(new FixtureResult
                {
                    Case = fixtureCase,
                    App = app,
                    Resolution = resolution,
                    CenterInRect = centerInRect,
                    RuntimeIdMatched = runtimeIdMatched,
                });
            }
        }

        return results;
    }
}
