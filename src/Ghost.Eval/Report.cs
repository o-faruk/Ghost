using System.Text;
using Ghost.Core.Models;

namespace Ghost.Eval;

public static class Report
{
    public static string Format(IReadOnlyList<FixtureResult> results)
    {
        var sb = new StringBuilder();
        var total = results.Count;

        sb.AppendLine();
        sb.AppendLine($"Ghost eval — {total} cases");
        sb.AppendLine();

        if (total == 0)
        {
            sb.AppendLine("No fixtures found.");
            return sb.ToString();
        }

        var passed = results.Count(r => r.Passed);
        sb.AppendLine($"Overall top-1 accuracy          {Pct(passed, total)}  ({passed}/{total})");
        sb.AppendLine("  by tier");

        foreach (var tier in Enum.GetValues<ResolutionTier>())
        {
            var inTier = results.Where(r => r.Resolution.Tier == tier).ToList();
            if (inTier.Count == 0 && tier is ResolutionTier.Vision or ResolutionTier.Ocr)
            {
                sb.AppendLine($"    {tier,-24} {Pct(0, total),6}    disabled");
                continue;
            }

            var tierPassed = inTier.Count(r => r.Passed);
            var avgMs = inTier.Count > 0 ? inTier.Average(r => r.Resolution.Duration.TotalMilliseconds) : 0;
            sb.AppendLine($"    {tier,-24} {Pct(inTier.Count, total),6}   avg {avgMs,4:0}ms");
        }

        sb.AppendLine();

        var zeroLlm = results.Count(r => r.Resolution.Tier == ResolutionTier.Deterministic);
        sb.AppendLine($"Zero-LLM resolution rate        {Pct(zeroLlm, total)}");

        var durations = results.Select(r => r.Resolution.Duration.TotalMilliseconds).OrderBy(d => d).ToList();
        sb.AppendLine($"Latency  p50 {Percentile(durations, 0.50):0}ms  p95 {Percentile(durations, 0.95):0}ms");

        var avgCost = results.Average(r => r.Resolution.EstimatedCostUsd);
        sb.AppendLine($"Est. cost per resolution        ${avgCost:0.00000}");
        sb.AppendLine();

        var failuresByApp = results
            .Where(r => !r.Passed)
            .GroupBy(r => r.App)
            .OrderByDescending(g => g.Count());

        sb.AppendLine("Failures by app");
        foreach (var g in failuresByApp)
        {
            sb.AppendLine($"  {g.Key,-12} {g.Count()}");
        }

        return sb.ToString();
    }

    private static string Pct(int part, int total) => total == 0 ? "0.0%" : $"{100.0 * part / total,5:0.0}%";

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
