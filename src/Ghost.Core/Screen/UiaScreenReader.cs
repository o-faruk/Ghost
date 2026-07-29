using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Ghost.Core.Models;
using Serilog;

namespace Ghost.Core.Screen;

/// <summary>
/// Reads the foreground window's UIA accessibility tree into a ScreenSnapshot. All UIA/COM work
/// runs on the supplied UiaThread. See Section 8.1 of the build spec for the capture algorithm
/// this implements: a single cached FindAllDescendants-equivalent walk, managed-code filtering,
/// a 400-element cap, and a 2000ms hard timeout.
/// </summary>
public sealed class UiaScreenReader : IScreenReader, IDisposable
{
    private const int MaxElements = 400;

    private static readonly HashSet<ControlType> NoiseControlTypes =
    [
        ControlType.Pane, ControlType.Group, ControlType.Custom, ControlType.Thumb, ControlType.Separator,
    ];

    private readonly UiaThread _uiaThread;
    private readonly SnapshotCache _cache;
    private readonly TimeSpan _timeout;
    private readonly Lazy<UIA3Automation> _automation;

    public UiaScreenReader(UiaThread uiaThread, SnapshotCache cache, TimeSpan? timeout = null)
    {
        _uiaThread = uiaThread;
        _cache = cache;
        _timeout = timeout ?? TimeSpan.FromMilliseconds(2000);
        // FlaUI's Automation object is itself a COM object; construct it lazily, on the UIA thread,
        // the first time it's needed, rather than at DI-construction time on an arbitrary thread.
        _automation = new Lazy<UIA3Automation>(() => _uiaThread.RunAsync(() => new UIA3Automation()).GetAwaiter().GetResult());
    }

    /// <inheritdoc />
    public async Task<ScreenSnapshot> GetSnapshotAsync(bool forceRefresh, CancellationToken ct)
    {
        var hwnd = GetForegroundWindowChecked();

        if (!forceRefresh && _cache.TryGet(hwnd, out var cached))
        {
            return cached;
        }

        var snapshot = await CaptureAsync(hwnd, ct);
        _cache.Set(hwnd, snapshot);
        return snapshot;
    }

    /// <summary>Captures a specific window regardless of what's currently foreground. Used by Ghost.Eval's `capture` verb.</summary>
    public Task<ScreenSnapshot> CaptureAsync(nint hwnd, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_timeout);
        return _uiaThread.RunAsync(() => Capture(hwnd), timeoutCts.Token);
    }

    private ScreenSnapshot Capture(nint hwnd)
    {
        var sw = Stopwatch.StartNew();
        var automation = _automation.Value;
        var window = automation.FromHandle(hwnd);

        var windowRect = window.Properties.BoundingRectangle.ValueOrDefault;
        var windowBounds = new PhysicalRect((int)windowRect.Left, (int)windowRect.Top, (int)windowRect.Width, (int)windowRect.Height);
        var processName = SafeProcessName(window.Properties.ProcessId.ValueOrDefault);
        var windowTitle = window.Properties.Name.ValueOrDefault ?? string.Empty;

        var mapped = new List<UiElement>();

        var cacheRequest = BuildCacheRequest(automation);
        using (cacheRequest.Activate())
        {
            Walk(window, depth: 1, parentRuntimeId: null, windowBounds, mapped);
        }

        var uncappedCount = mapped.Count;
        var elements = uncappedCount > MaxElements
            ? mapped.OrderBy(e => e.Depth).Take(MaxElements).ToList()
            : mapped;

        if (uncappedCount > MaxElements)
        {
            Log.Warning(
                "Ghost UIA capture for {ProcessName} hit the {Cap}-element cap ({Actual} found before capping); kept shallowest",
                processName, MaxElements, uncappedCount);
        }

        return new ScreenSnapshot
        {
            WindowHandle = hwnd,
            ProcessName = processName,
            WindowTitle = windowTitle,
            WindowBounds = windowBounds,
            Elements = elements,
            CapturedAt = DateTimeOffset.Now,
            StructureHash = ComputeStructureHash(elements),
            CaptureDuration = sw.Elapsed,
        };
    }

    private static CacheRequest BuildCacheRequest(UIA3Automation automation)
    {
        var cacheRequest = new CacheRequest
        {
            TreeScope = TreeScope.Element,
            AutomationElementMode = AutomationElementMode.None,
        };
        cacheRequest.Add(automation.PropertyLibrary.Element.Name);
        cacheRequest.Add(automation.PropertyLibrary.Element.ControlType);
        cacheRequest.Add(automation.PropertyLibrary.Element.AutomationId);
        cacheRequest.Add(automation.PropertyLibrary.Element.HelpText);
        cacheRequest.Add(automation.PropertyLibrary.Element.AccessKey);
        cacheRequest.Add(automation.PropertyLibrary.Element.ClassName);
        cacheRequest.Add(automation.PropertyLibrary.Element.BoundingRectangle);
        cacheRequest.Add(automation.PropertyLibrary.Element.IsEnabled);
        cacheRequest.Add(automation.PropertyLibrary.Element.IsOffscreen);
        cacheRequest.Add(automation.PropertyLibrary.Element.IsKeyboardFocusable);
        cacheRequest.Add(automation.PropertyLibrary.Element.ProcessId);
        cacheRequest.Add(automation.PropertyLibrary.Element.RuntimeId);
        cacheRequest.Add(automation.PatternLibrary.ValuePattern);
        return cacheRequest;
    }

    /// <summary>
    /// Depth-first walk of the window's children. Each FindAllChildren call is one COM round trip
    /// that returns every requested property for that level already cached, which is the
    /// performance-critical part; we still recurse level by level (rather than one flat
    /// FindAllDescendants) so Depth can be tracked exactly for the 400-element cap and the
    /// StructureHash ordering.
    /// </summary>
    private void Walk(AutomationElement node, int depth, string? parentRuntimeId, PhysicalRect windowBounds, List<UiElement> into)
    {
        AutomationElement[] children;
        try
        {
            children = node.FindAllChildren();
        }
        catch
        {
            return;
        }

        foreach (var child in children)
        {
            var mapped = Map(child, windowBounds, depth, parentRuntimeId);
            if (mapped is not null)
            {
                into.Add(mapped);
            }

            // Recurse even when this node was filtered out (e.g. an unnamed Pane) — it can still contain named children.
            Walk(child, depth + 1, mapped?.RuntimeId ?? parentRuntimeId, windowBounds, into);
        }
    }

    private static UiElement? Map(AutomationElement e, PhysicalRect windowBounds, int depth, string? parentRuntimeId)
    {
        string name;
        ControlType controlType;
        bool isEnabled;
        bool isOffscreen;
        bool isKeyboardFocusable;
        string? automationId;
        string? helpText;
        string? accessKey;
        string? className;
        string? value;
        PhysicalRect bounds;
        string runtimeId;

        try
        {
            var rect = e.Properties.BoundingRectangle.ValueOrDefault;
            bounds = new PhysicalRect((int)rect.Left, (int)rect.Top, (int)rect.Width, (int)rect.Height);
            name = e.Properties.Name.ValueOrDefault ?? string.Empty;
            controlType = e.Properties.ControlType.ValueOrDefault;
            isEnabled = e.Properties.IsEnabled.ValueOrDefault;
            isOffscreen = e.Properties.IsOffscreen.ValueOrDefault;
            isKeyboardFocusable = e.Properties.IsKeyboardFocusable.ValueOrDefault;
            automationId = e.Properties.AutomationId.ValueOrDefault;
            helpText = e.Properties.HelpText.ValueOrDefault;
            accessKey = e.Properties.AccessKey.ValueOrDefault;
            className = e.Properties.ClassName.ValueOrDefault;
            value = e.Patterns.Value.PatternOrDefault?.Value.ValueOrDefault;

            var runtimeIdInts = e.Properties.RuntimeId.ValueOrDefault;
            runtimeId = runtimeIdInts is { Length: > 0 } ? string.Join(".", runtimeIdInts) : Guid.NewGuid().ToString("N");
        }
        catch
        {
            // Some providers throw on properties they don't actually support despite being cached.
            return null;
        }

        if (bounds.IsDegenerate || isOffscreen)
        {
            return null;
        }

        if (!Intersects(bounds, windowBounds))
        {
            return null;
        }

        if (NoiseControlTypes.Contains(controlType) && name.Length == 0)
        {
            return null;
        }

        return new UiElement
        {
            RuntimeId = runtimeId,
            Name = name,
            ControlType = controlType.ToString(),
            AutomationId = string.IsNullOrEmpty(automationId) ? null : automationId,
            HelpText = string.IsNullOrEmpty(helpText) ? null : helpText,
            AccessKey = string.IsNullOrEmpty(accessKey) ? null : accessKey,
            ClassName = string.IsNullOrEmpty(className) ? null : className,
            Value = string.IsNullOrEmpty(value) ? null : value,
            Bounds = bounds,
            IsEnabled = isEnabled,
            IsOffscreen = isOffscreen,
            IsKeyboardFocusable = isKeyboardFocusable,
            Depth = depth,
            ParentRuntimeId = parentRuntimeId,
        };
    }

    private static bool Intersects(PhysicalRect a, PhysicalRect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private static string ComputeStructureHash(IEnumerable<UiElement> elements)
    {
        var ordered = elements.OrderBy(e => e.Depth).ThenBy(e => e.Bounds.Left).ThenBy(e => e.Bounds.Top);
        var sb = new StringBuilder();
        foreach (var e in ordered)
        {
            sb.Append(e.ControlType).Append('|').Append(e.Name).Append('|')
              .Append(e.Bounds.Left).Append(',').Append(e.Bounds.Top).Append(',')
              .Append(e.Bounds.Width).Append(',').Append(e.Bounds.Height).Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string SafeProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return "unknown";
        }
    }

    private nint GetForegroundWindowChecked()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == 0)
        {
            throw new InvalidOperationException("GetForegroundWindow returned no window");
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Environment.ProcessId)
        {
            throw new InvalidOperationException("refusing to read Ghost's own foreground window");
        }

        return hwnd;
    }

    public void Dispose()
    {
        if (_automation.IsValueCreated)
        {
            _automation.Value.Dispose();
        }
    }
}
