using Ghost.Eval;

var fixturesRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures");
fixturesRoot = Path.GetFullPath(fixturesRoot);

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
        // The live UIA capture path (UiaScreenReader) lands in Phase 1. For now this verb
        // exists so the CLI surface is stable; it explains what's missing rather than failing silently.
        Console.WriteLine("`capture` requires Ghost.Core's UiaScreenReader, which lands in Phase 1.");
        Console.WriteLine("For now, write fixtures by hand as <app>/<name>.snapshot.json + <name>.case.json.");
        return 1;

    default:
        PrintUsage();
        return 1;
}

async Task<int> RunAsync()
{
    var runner = new FixtureRunner(new PlaceholderResolverPipeline());
    var results = await runner.RunAllAsync(fixturesRoot, CancellationToken.None);
    Console.WriteLine(Report.Format(results));
    return 0;
}

void PrintUsage()
{
    Console.WriteLine("Ghost.Eval usage:");
    Console.WriteLine("  Ghost.Eval capture   Capture a live snapshot fixture (Phase 1+)");
    Console.WriteLine("  Ghost.Eval run       Replay all fixtures under fixtures/ and print a report");
}
