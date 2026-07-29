using System.Runtime.InteropServices;

namespace Ghost.Core.Screen;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);
}
