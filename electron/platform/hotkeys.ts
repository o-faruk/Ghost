import { globalShortcut, BrowserWindow } from 'electron';

// Alt+Shift+G — safe on Windows (avoids Win+*, Alt+Tab, Alt+F4 system shortcuts).
// VERIFY on PC: open a game or full-screen app and confirm the hotkey still fires.
const TRIGGER_HOTKEY = 'Alt+Shift+G';

/**
 * Accepts a getter so the callback always resolves the current window reference,
 * even if the window is recreated between registration and invocation.
 */
export function registerHotkeys(getWindow: () => BrowserWindow | null): void {
  const registered = globalShortcut.register(TRIGGER_HOTKEY, () => {
    const win = getWindow();
    if (!win) return;

    if (win.isVisible()) {
      win.hide();
    } else {
      win.show();
    }
  });

  if (!registered) {
    console.error(
      `[hotkeys] Failed to register ${TRIGGER_HOTKEY} — another process may have claimed it.`
    );
  }
}
