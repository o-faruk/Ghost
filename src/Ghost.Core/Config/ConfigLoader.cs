using System.Text.Json;

namespace Ghost.Core.Config;

/// <summary>
/// Loads GhostConfig from %APPDATA%\Ghost\config.json, falling back to the ANTHROPIC_API_KEY
/// environment variable for the API key, and to GhostConfig.Default for everything else when
/// no config file exists.
/// </summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string DefaultConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ghost", "config.json");

    public static GhostConfig Load(string? path = null)
    {
        path ??= DefaultConfigPath;

        var config = File.Exists(path)
            ? JsonSerializer.Deserialize<GhostConfig>(File.ReadAllText(path), JsonOptions) ?? GhostConfig.Default
            : GhostConfig.Default;

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                config = config with { ApiKey = envKey };
            }
        }

        return config;
    }
}
