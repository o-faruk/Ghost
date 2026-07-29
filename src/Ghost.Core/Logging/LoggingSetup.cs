using Ghost.Core.Config;
using Serilog;
using Serilog.Events;

namespace Ghost.Core.Logging;

/// <summary>
/// Configures the process-wide Serilog logger: console for dev, plus a 7-day rolling file
/// sink under %LOCALAPPDATA%\Ghost\logs. Callers must never log UiElement.Value contents at
/// Information level or above, since text fields can contain what the user typed, including
/// passwords in some applications.
/// </summary>
public static class LoggingSetup
{
    public static string DefaultLogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ghost", "logs");

    public static void Configure(LoggingConfig config, string? logDirectory = null)
    {
        logDirectory ??= DefaultLogDirectory;
        Directory.CreateDirectory(logDirectory);

        var level = Enum.TryParse<LogEventLevel>(config.Level, ignoreCase: true, out var parsed)
            ? parsed
            : LogEventLevel.Information;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, "ghost-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: config.FileRetentionDays)
            .CreateLogger();
    }
}
