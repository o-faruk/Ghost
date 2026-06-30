import { globalShortcut, BrowserWindow } from 'electron';

// Alt+Shift+G — safe on Windows (avoids Win+*, Alt+Tab, Alt+F4).
// VERIFY on PC: test inside fullscreen apps and games to confirm the hotkey still fires.
const TRIGGER_HOTKEY = 'Alt+Shift+G';

export function registerHotkeys(
  getWindow: () => BrowserWindow | null,
  onTrigger: () => void,
  onDismiss: () => void,
): void {
  const registered = globalShortcut.register(TRIGGER_HOTKEY, () => {
    const win = getWindow();
    if (!win) return;
    if (win.isVisible()) {
      onDismiss();
    } else {
      onTrigger();
    }
  });

  if (!registered) {
    console.error(
      `[hotkeys] Failed to register ${TRIGGER_HOTKEY} — another process may have claimed it.`,
    );
  }
}
