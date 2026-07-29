using FluentAssertions;
using Ghost.Core.Models;
using Ghost.Core.Resolve;
using Xunit;

namespace Ghost.Core.Tests.Resolve;

public class ScoringTests
{
    // --- Normalize ---

    [Theory]
    [InlineData("&File", "file")]
    [InlineData("Save As...", "save as")]
    [InlineData("  \"Download\"  ", "download")]
    [InlineData("Address   and    search bar", "address and search bar")]
    [InlineData("New Tab", "new tab")]
    public void Normalize_AppliesEachRule(string input, string expected)
    {
        Scoring.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Normalize_EmptyInput_ReturnsEmpty()
    {
        Scoring.Normalize("").Should().Be("");
    }

    // --- SplitIdentifierWords ---

    [Theory]
    [InlineData("downloadButton", "download Button")]
    [InlineData("some_id", "some id")]
    [InlineData("some-id", "some id")]
    public void SplitIdentifierWords_SplitsCamelSnakeKebab(string input, string expected)
    {
        Scoring.SplitIdentifierWords(input).Should().Be(expected);
    }

    // --- MatchScore tiers ---

    [Fact]
    public void MatchScore_ExactMatch_ScoresOne()
    {
        Scoring.MatchScore("file", "file").Should().Be(1.00);
    }

    [Fact]
    public void MatchScore_CandidateStartsWithQuery_Scores085()
    {
        Scoring.MatchScore("down", "download").Should().Be(0.85);
    }

    [Fact]
    public void MatchScore_QueryStartsWithCandidate_Scores085()
    {
        Scoring.MatchScore("download button", "download").Should().Be(0.85);
    }

    [Fact]
    public void MatchScore_WholeWordSubstring_Scores075()
    {
        Scoring.MatchScore("address bar", "the address bar where you type a url").Should().Be(0.75);
    }

    [Fact]
    public void MatchScore_TokenOverlap_ScoresScaledJaccard()
    {
        // "save file" vs "save as" -> intersection {save} = 1, union {save, file, as} = 3 -> jaccard 1/3
        var score = Scoring.MatchScore("save file", "save as");
        score.Should().BeApproximately(0.70 * (1.0 / 3.0), 0.0001);
    }

    [Fact]
    public void MatchScore_NoOverlap_ScoresZero()
    {
        Scoring.MatchScore("download", "print preview").Should().Be(0.0);
    }

    [Fact]
    public void MatchScore_EmptyCandidate_ScoresZero()
    {
        Scoring.MatchScore("file", "").Should().Be(0.0);
    }

    // --- Score: candidate-string weights ---

    [Fact]
    public void Score_NamePreferredOverHelpTextWhenBothMatchExactly()
    {
        var byName = MakeElement(name: "download", helpText: null);
        var byHelpText = MakeElement(name: "unrelated", helpText: "download");

        Scoring.Score("download", byName, StepAction.Click, 0).Should().BeGreaterThan(Scoring.Score("download", byHelpText, StepAction.Click, 0));
    }

    [Fact]
    public void Score_AutomationIdWeightAppliesWithCamelSplit()
    {
        var element = MakeElement(name: "", automationId: "downloadButton");

        var score = Scoring.Score("download button", element, StepAction.Click, 0);

        // exact normalized match (1.00) * automationId weight (0.65), no control-type match here
        score.Should().BeApproximately(0.65, 0.0001);
    }

    [Fact]
    public void Score_ValueWeightIsLowest()
    {
        var element = MakeElement(name: "", value: "download");

        var score = Scoring.Score("download", element, StepAction.Click, 0);

        score.Should().BeApproximately(0.55, 0.0001);
    }

    // --- Score: modifiers ---

    [Fact]
    public void Score_CompatibleControlType_BoostsScore()
    {
        var button = MakeElement(name: "download", controlType: "Button");
        var pane = MakeElement(name: "download", controlType: "Pane");

        Scoring.Score("download", button, StepAction.Click, 0).Should().BeGreaterThan(Scoring.Score("download", pane, StepAction.Click, 0));
    }

    [Fact]
    public void Score_CompatibleControlType_CapsAtOne()
    {
        var element = MakeElement(name: "download", controlType: "Button");

        Scoring.Score("download", element, StepAction.Click, 0).Should().Be(1.0);
    }

    [Fact]
    public void Score_Disabled_Multiplies0Point3()
    {
        var enabled = MakeElement(name: "download", isEnabled: true);
        var disabled = MakeElement(name: "download", isEnabled: false);

        var enabledScore = Scoring.Score("download", enabled, StepAction.Hover, 0);
        var disabledScore = Scoring.Score("download", disabled, StepAction.Hover, 0);

        disabledScore.Should().BeApproximately(enabledScore * 0.3, 0.0001);
    }

    [Fact]
    public void Score_LargeElement_Multiplies0Point7()
    {
        var windowArea = 1000 * 1000;
        var largeElement = MakeElement(name: "download", bounds: new PhysicalRect(0, 0, 900, 900)); // > 40% of window area

        var withoutAreaPenalty = Scoring.Score("download", largeElement, StepAction.Hover, 0);
        var withAreaPenalty = Scoring.Score("download", largeElement, StepAction.Hover, windowArea);

        withAreaPenalty.Should().BeApproximately(withoutAreaPenalty * 0.7, 0.0001);
    }

    [Fact]
    public void Score_TinyElement_Multiplies0Point8()
    {
        var tiny = MakeElement(name: "download", bounds: new PhysicalRect(0, 0, 4, 4)); // area 16 < 64

        var score = Scoring.Score("download", tiny, StepAction.Hover, 0);

        score.Should().BeApproximately(1.00 * 0.8, 0.0001);
    }

    [Fact]
    public void Score_NoCandidateMatches_ReturnsZero()
    {
        var element = MakeElement(name: "print preview");

        Scoring.Score("download", element, StepAction.Click, 0).Should().Be(0.0);
    }

    private static UiElement MakeElement(
        string name = "",
        string? helpText = null,
        string? automationId = null,
        string? value = null,
        string controlType = "Edit",
        bool isEnabled = true,
        PhysicalRect? bounds = null) => new()
    {
        RuntimeId = "1",
        Name = name,
        ControlType = controlType,
        HelpText = helpText,
        AutomationId = automationId,
        Value = value,
        Bounds = bounds ?? new PhysicalRect(0, 0, 100, 30),
        IsEnabled = isEnabled,
        IsOffscreen = false,
        IsKeyboardFocusable = true,
        Depth = 1,
    };
}
