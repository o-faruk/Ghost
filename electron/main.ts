import { app, BrowserWindow, globalShortcut } from 'electron';
import * as path from 'path';
import { registerHotkeys, getPrimaryDisplayInfo } from './platform';

let overlayWindow: BrowserWindow | null = null;

function createOverlayWindow(): void {
  const { width, height, x, y } = getPrimaryDisplayInfo();

  overlayWindow = new BrowserWindow({
    width,
    height,
    x,
    y,
    frame: false,
    transparent: true,
    alwaysOnTop: true,
    skipTaskbar: true,
    resizable: false,
    movable: false,
    // focusable:false keeps the overlay out of the focus chain so Alt+Tab is unaffected.
    // Change to true when the text-input panel is added in the next milestone.
    focusable: false,
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  // forward:true is Windows-only — passes mouse move events through to the window below.
  // On macOS this option is silently ignored; setIgnoreMouseEvents still makes the window
  // non-interactive, which is fine for dev/testing.
  overlayWindow.setIgnoreMouseEvents(true, { forward: true });

  // 'screen-saver' level keeps the overlay above fullscreen apps on Windows.
  // VERIFY on PC: open a fullscreen game and confirm the overlay is still visible on hotkey press.
  overlayWindow.setAlwaysOnTop(true, 'screen-saver');

  const isDev = !app.isPackaged;
  if (isDev) {
    overlayWindow.loadURL('http://localhost:5173');
  } else {
    overlayWindow.loadFile(path.join(__dirname, '../renderer/index.html'));
  }

  overlayWindow.on('closed', () => {
    overlayWindow = null;
  });
}

app.whenReady().then(() => {
  createOverlayWindow();
  registerHotkeys(() => overlayWindow);
});

app.on('will-quit', () => {
  globalShortcut.unregisterAll();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
