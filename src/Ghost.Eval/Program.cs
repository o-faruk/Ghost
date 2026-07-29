using Ghost.Core.Config;
using Ghost.Core.Logging;
using Ghost.Core.Models;
using Ghost.Core.Resolve;
using Ghost.Core.Screen;
using Ghost.Eval;

var fixturesRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures");
fixturesRoot = Path.GetFullPath(fixturesRoot);

// `capture` and `run` are where the interesting decisions happen (UIA filtering, and resolver
// scoring/acceptance respectively); run both at Debug so those are visible instead of swallowed.
var logLevel = args.Length > 0 && (args[0] == "capture" || args[0] == "run") ? "Debug" : "Warning";
LoggingSetup.Configure(new LoggingConfig { Level = logLevel, FileRetentionDays = 7 });

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

switch (args[0])
{
    case "run":
        return await RunAsync();

    case "capture":
        return await CaptureAsync();

    default:
        PrintUsage();
        return 1;
}

async Task<int> RunAsync()
{
    var config = ConfigLoader.Load();
    var pipeline = new ResolverPipeline(
    [
        new DeterministicResolver(config.Thresholds.AcceptScore, config.Thresholds.AcceptMargin),
        // LlmResolver joins the pipeline in Phase 3.
    ]);

    var runner = new FixtureRunner(pipeline);
    var results = await runner.RunAllAsync(fixturesRoot, CancellationToken.None);
    Console.WriteLine(Report.Format(results));
    return 0;
}

async Task<int> CaptureAsync()
{
    if (args.Length < 3)
    {
        Console.WriteLine("Usage: Ghost.Eval capture <app> <name>");
        Console.WriteLine("  e.g. Ghost.Eval capture chrome address-bar");
        return 1;
    }

    var app = args[1];
    var name = args[2];

    Console.WriteLine("Focus the target window now. Capturing in 5 seconds...");
    await Task.Delay(TimeSpan.FromSeconds(5));

    using var uiaThread = new UiaThread();
    using var reader = new UiaScreenReader(uiaThread, new SnapshotCache());

    ScreenSnapshot snapshot;
    try
    {
        snapshot = await reader.GetSnapshotAsync(forceRefresh: true, CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Capture failed: {ex.Message}");
        return 1;
    }

    var appDir = Path.Combine(fixturesRoot, app);
    var snapshotFileName = $"{name}.snapshot.json";
    var snapshotPath = Path.Combine(appDir, snapshotFileName);
    JsonIo.Write(snapshotPath, snapshot);

    var casePath = Path.Combine(appDir, $"{name}.case.json");
    var caseWritten = false;
    if (!File.Exists(casePath))
    {
        var stub = new FixtureCase
        {
            Id = $"{app}-{name}",
            Snapshot = snapshotFileName,
            Action = StepAction.Click,
            TargetDescription = "TODO: describe the target element in plain English",
            ExpectedRect = new PhysicalRect(0, 0, 0, 0),
            Notes = "TODO: fill in expectedRect (and optionally expectedRuntimeId) by inspecting the snapshot above",
        };
        JsonIo.Write(casePath, stub);
        caseWritten = true;
    }

    Console.WriteLine($"Captured {snapshot.Elements.Count} elements from {snapshot.ProcessName} \"{snapshot.WindowTitle}\" in {snapshot.CaptureDuration.TotalMilliseconds:0}ms");
    Console.WriteLine($"Wrote {snapshotPath}");
    Console.WriteLine(caseWritten
        ? $"Wrote stub case file: {casePath} — fill in targetDescription/expectedRect/expectedRuntimeId"
        : $"Case file already exists, left untouched: {casePath}");
    return 0;
}

void PrintUsage()
{
    Console.WriteLine("Ghost.Eval usage:");
    Console.WriteLine("  Ghost.Eval capture <app> <name>   Capture the foreground window as a fixture");
    Console.WriteLine("  Ghost.Eval run                    Replay all fixtures under fixtures/ and print a report");
}
