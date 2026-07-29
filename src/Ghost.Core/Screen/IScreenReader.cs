using Ghost.Core.Models;

namespace Ghost.Core.Screen;

/// <summary>Produces a ScreenSnapshot of the current foreground window.</summary>
public interface IScreenReader
{
    /// <summary>
    /// Returns the current foreground window's snapshot. When <paramref name="forceRefresh"/> is
    /// false and a fresh cached snapshot exists for that window, the cache is used instead of a
    /// live capture.
    /// </summary>
    Task<ScreenSnapshot> GetSnapshotAsync(bool forceRefresh, CancellationToken ct);
}
