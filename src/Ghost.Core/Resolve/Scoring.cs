using System.Text.RegularExpressions;
using Ghost.Core.Models;

namespace Ghost.Core.Resolve;

/// <summary>
/// Normalization and match-scoring rules for the deterministic resolver. This logic is where
/// accuracy lives — see Section 8.2 of the build spec for the tables this implements verbatim.
/// </summary>
public static partial class Scoring
{
    private static readonly IReadOnlySet<string> ClickCompatible = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Button", "MenuItem", "Hyperlink", "CheckBox", "RadioButton", "TabItem", "ListItem", "SplitButton", "TreeItem",
    };

    private static readonly IReadOnlyDictionary<StepAction, IReadOnlySet<string>> CompatibleControlTypes =
        new Dictionary<StepAction, IReadOnlySet<string>>
        {
            [StepAction.Click] = ClickCompatible,
            [StepAction.DoubleClick] = ClickCompatible,
            [StepAction.RightClick] = ClickCompatible,
            [StepAction.Type] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Edit", "ComboBox", "Document" },
        };

    /// <summary>lowercase, strip accelerator markers/trailing ellipsis/surrounding punctuation, collapse whitespace, trim.</summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var s = input.ToLowerInvariant();
        s = s.Replace("&", "");
        s = TrailingEllipsisRegex().Replace(s, "");
        s = s.Trim();
        s = s.Trim('"', '\'', '“', '”', '‘', '’', '.', ',', ':', ';', '!', '?', '(', ')');
        s = WhitespaceRegex().Replace(s, " ");
        return s.Trim();
    }

    /// <summary>Splits camelCase/snake_case/kebab-case identifiers into space-separated words, e.g. "downloadButton" -> "download Button".</summary>
    public static string SplitIdentifierWords(string id)
    {
        var withSpaces = CamelCaseBoundaryRegex().Replace(id, "$1 $2");
        return withSpaces.Replace('_', ' ').Replace('-', ' ');
    }

    /// <summary>Best-match score between an already-normalized query and an already-normalized candidate string, before any weight is applied.</summary>
    public static double MatchScore(string normalizedQuery, string normalizedCandidate)
    {
        if (normalizedQuery.Length == 0 || normalizedCandidate.Length == 0)
        {
            return 0.0;
        }

        if (normalizedCandidate == normalizedQuery)
        {
            return 1.00;
        }

        if (normalizedCandidate.StartsWith(normalizedQuery, StringComparison.Ordinal) ||
            normalizedQuery.StartsWith(normalizedCandidate, StringComparison.Ordinal))
        {
            return 0.85;
        }

        if (ContainsWholeWord(normalizedCandidate, normalizedQuery))
        {
            return 0.75;
        }

        var jaccard = TokenJaccard(normalizedQuery, normalizedCandidate);
        return jaccard > 0 ? 0.70 * jaccard : 0.0;
    }

    /// <summary>Full per-element score: best weighted candidate-string match, times the ControlType/enabled/area modifiers.</summary>
    public static double Score(string query, UiElement element, StepAction action, int windowArea)
    {
        var normalizedQuery = Normalize(query);
        var best = 0.0;

        foreach (var (text, weight) in CandidateStrings(element))
        {
            var normalizedCandidate = Normalize(text);
            var raw = MatchScore(normalizedQuery, normalizedCandidate) * weight;
            if (raw > best)
            {
                best = raw;
            }
        }

        if (best <= 0.0)
        {
            return 0.0;
        }

        if (CompatibleControlTypes.TryGetValue(action, out var compatible) && compatible.Contains(element.ControlType))
        {
            best = Math.Min(1.0, best * 1.15);
        }

        if (!element.IsEnabled)
        {
            best *= 0.3;
        }

        if (windowArea > 0 && element.Bounds.Area > windowArea * 0.4)
        {
            best *= 0.7;
        }

        if (element.Bounds.Area < 64)
        {
            best *= 0.8;
        }

        return best;
    }

    private static IEnumerable<(string Text, double Weight)> CandidateStrings(UiElement e)
    {
        if (e.Name.Length > 0)
        {
            yield return (e.Name, 1.00);
        }

        if (!string.IsNullOrEmpty(e.HelpText))
        {
            yield return (e.HelpText, 0.80);
        }

        if (!string.IsNullOrEmpty(e.AutomationId))
        {
            yield return (SplitIdentifierWords(e.AutomationId), 0.65);
        }

        if (!string.IsNullOrEmpty(e.Value))
        {
            yield return (e.Value, 0.55);
        }
    }

    private static bool ContainsWholeWord(string haystack, string needle)
    {
        var pattern = $@"(?<![a-z0-9]){Regex.Escape(needle)}(?![a-z0-9])";
        return Regex.IsMatch(haystack, pattern);
    }

    private static double TokenJaccard(string a, string b)
    {
        var setA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var setB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (setA.Count == 0 || setB.Count == 0)
        {
            return 0.0;
        }

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    [GeneratedRegex(@"\.{3}\s*$")]
    private static partial Regex TrailingEllipsisRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelCaseBoundaryRegex();
}
