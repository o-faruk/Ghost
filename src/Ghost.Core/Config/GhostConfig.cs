namespace Ghost.Core.Config;

public sealed record ModelPricing(decimal InputPerMTok, decimal OutputPerMTok);

public sealed record ModelsConfig
{
    public required string Planner { get; init; }
    public required string Resolver { get; init; }
    public required string Vision { get; init; }
}

public sealed record TiersConfig
{
    public required bool LlmDisambiguation { get; init; }
    public required bool Ocr { get; init; }
    public required bool Vision { get; init; }
}

public sealed record TimeoutsConfig
{
    public required int CaptureMs { get; init; }
    public required int ResolveMs { get; init; }
    public required int PlanMs { get; init; }
    public required int SettleMs { get; init; }
}

public sealed record ThresholdsConfig
{
    public required double AcceptScore { get; init; }
    public required double AcceptMargin { get; init; }
    public required int NearMissPx { get; init; }
}

public sealed record OverlayConfig
{
    public required int SpriteSize { get; init; }
    public required int[] FollowOffset { get; init; }
    public required bool ShowRing { get; init; }
}

public sealed record LoggingConfig
{
    public required string Level { get; init; }
    public required int FileRetentionDays { get; init; }
}

/// <summary>
/// Deserialized form of %APPDATA%\Ghost\config.json. Every threshold, timeout, and model
/// name Ghost uses lives here; nothing is hardcoded elsewhere.
/// </summary>
public sealed record GhostConfig
{
    public required string ApiKey { get; init; }
    public required ModelsConfig Models { get; init; }
    public required IReadOnlyDictionary<string, ModelPricing> Pricing { get; init; }
    public required string Hotkey { get; init; }
    public required TiersConfig Tiers { get; init; }
    public required TimeoutsConfig Timeouts { get; init; }
    public required ThresholdsConfig Thresholds { get; init; }
    public required OverlayConfig Overlay { get; init; }
    public required LoggingConfig Logging { get; init; }

    /// <summary>The hardcoded defaults mirrored from config.example.json, used when no file exists yet.</summary>
    public static GhostConfig Default => new()
    {
        ApiKey = "",
        Models = new ModelsConfig
        {
            Planner = "claude-sonnet-5",
            Resolver = "claude-haiku-4-5-20251001",
            Vision = "claude-sonnet-5",
        },
        Pricing = new Dictionary<string, ModelPricing>
        {
            ["claude-sonnet-5"] = new ModelPricing(2.00m, 10.00m),
            ["claude-haiku-4-5-20251001"] = new ModelPricing(1.00m, 5.00m),
        },
        Hotkey = "Ctrl+G",
        Tiers = new TiersConfig { LlmDisambiguation = true, Ocr = false, Vision = false },
        Timeouts = new TimeoutsConfig { CaptureMs = 2000, ResolveMs = 3000, PlanMs = 8000, SettleMs = 3000 },
        Thresholds = new ThresholdsConfig { AcceptScore = 0.80, AcceptMargin = 0.15, NearMissPx = 40 },
        Overlay = new OverlayConfig { SpriteSize = 24, FollowOffset = [18, 18], ShowRing = true },
        Logging = new LoggingConfig { Level = "Information", FileRetentionDays = 7 },
    };
}
