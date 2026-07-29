using Ghost.Core.Models;

namespace Ghost.Core.Screen;

/// <summary>
/// Holds the last snapshot per window handle with a 5-second TTL. Phase 2 adds invalidation on
/// StructureChanged via ForegroundWatcher; for now entries simply expire.
/// </summary>
public sealed class SnapshotCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    private readonly Dictionary<nint, ScreenSnapshot> _snapshots = new();
    private readonly Lock _lock = new();

    public bool TryGet(nint hwnd, out ScreenSnapshot snapshot)
    {
        lock (_lock)
        {
            if (_snapshots.TryGetValue(hwnd, out var cached) &&
                DateTimeOffset.Now - cached.CapturedAt < Ttl)
            {
                snapshot = cached;
                return true;
            }
        }

        snapshot = null!;
        return false;
    }

    public void Set(nint hwnd, ScreenSnapshot snapshot)
    {
        lock (_lock)
        {
            _snapshots[hwnd] = snapshot;
        }
    }

    public void Invalidate(nint hwnd)
    {
        lock (_lock)
        {
            _snapshots.Remove(hwnd);
        }
    }
}
