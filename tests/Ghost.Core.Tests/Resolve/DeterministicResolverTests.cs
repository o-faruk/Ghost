using FluentAssertions;
using Ghost.Core.Models;
using Ghost.Core.Resolve;
using Xunit;

namespace Ghost.Core.Tests.Resolve;

public class DeterministicResolverTests
{
    private readonly DeterministicResolver _resolver = new(acceptScore: 0.80, acceptMargin: 0.15);

    [Fact]
    public async Task TryResolveAsync_ClearWinner_Accepts()
    {
        var snapshot = MakeSnapshot(
            MakeElement("Download", "Button"),
            MakeElement("Print", "Button"));

        var step = MakeStep("Download");

        var result = await _resolver.TryResolveAsync(step, snapshot, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Tier.Should().Be(ResolutionTier.Deterministic);
        result.Element!.Name.Should().Be("Download");
    }

    [Fact]
    public async Task TryResolveAsync_AmbiguousTopTwo_EscalatesInsteadOfGuessing()
    {
        // Both named "Save" -> identical top scores, margin is 0 -> must not accept.
        var snapshot = MakeSnapshot(
            MakeElement("Save", "Button"),
            MakeElement("Save", "MenuItem"));

        var step = MakeStep("Save");

        var result = await _resolver.TryResolveAsync(step, snapshot, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_NoCandidatesAboveZero_Escalates()
    {
        var snapshot = MakeSnapshot(MakeElement("Print preview", "Button"));
        var step = MakeStep("Download as PDF");

        var result = await _resolver.TryResolveAsync(step, snapshot, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_TopBelowAcceptScore_Escalates()
    {
        // "save" vs "save as" -> token jaccard only, well under 0.80.
        var snapshot = MakeSnapshot(MakeElement("Save As", "Button"));
        var step = MakeStep("save file now please");

        var result = await _resolver.TryResolveAsync(step, snapshot, CancellationToken.None);

        result.Should().BeNull();
    }

    private static PlanStep MakeStep(string targetDescription) => new()
    {
        Index = 1,
        Action = StepAction.Click,
        TargetDescription = targetDescription,
        Instruction = targetDescription,
        ExpectedOutcome = "",
    };

    private static UiElement MakeElement(string name, string controlType) => new()
    {
        RuntimeId = Guid.NewGuid().ToString(),
        Name = name,
        ControlType = controlType,
        Bounds = new PhysicalRect(0, 0, 100, 30),
        IsEnabled = true,
        IsOffscreen = false,
        IsKeyboardFocusable = true,
        Depth = 1,
    };

    private static ScreenSnapshot MakeSnapshot(params UiElement[] elements) => new()
    {
        WindowHandle = 1,
        ProcessName = "test",
        WindowTitle = "test",
        WindowBounds = new PhysicalRect(0, 0, 1000, 1000),
        Elements = elements,
        CapturedAt = DateTimeOffset.Now,
        StructureHash = "test",
        CaptureDuration = TimeSpan.Zero,
    };
}
