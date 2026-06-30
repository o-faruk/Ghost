import { app, BrowserWindow, globalShortcut, ipcMain, desktopCapturer } from 'electron';
import * as path from 'path';
import { registerHotkeys, getPrimaryDisplayInfo } from './platform';

const PYTHON_URL = 'http://127.0.0.1:8000';

let overlayWindow: BrowserWindow | null = null;

// ─── Window creation ──────────────────────────────────────────────────────────

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
    focusable: false,       // default: click-through; enabled temporarily during input
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  // forward:true is Windows-only — forwards mouse move msgs to the process below.
  // Silently ignored on macOS.
  overlayWindow.setIgnoreMouseEvents(true, { forward: true });
  overlayWindow.setAlwaysOnTop(true, 'screen-saver');

  const isDev = !app.isPackaged;
  if (isDev) {
    overlayWindow.loadURL('http://localhost:5173');
  } else {
    overlayWindow.loadFile(path.join(__dirname, '../renderer/index.html'));
  }

  overlayWindow.webContents.once('did-finish-load', () => {
    if (!app.isPackaged) {
      // Dev-only: open straight into input mode so the panel is visible immediately.
      overlayWindow?.show();
      // Small delay so React finishes mounting before we send the first IPC event.
      setTimeout(() => enterInputMode(), 150);
    }
  });

  overlayWindow.on('closed', () => {
    overlayWindow = null;
  });
}

// ─── State transitions ────────────────────────────────────────────────────────

function enterInputMode(): void {
  if (!overlayWindow) return;
  overlayWindow.setFocusable(true);
  overlayWindow.setIgnoreMouseEvents(false);
  overlayWindow.focus();
  overlayWindow.webContents.send('ghost:state', { state: 'input' });
}

function enterClickThrough(): void {
  if (!overlayWindow) return;
  overlayWindow.setFocusable(false);
  overlayWindow.setIgnoreMouseEvents(true, { forward: true });
}

function dismissOverlay(): void {
  enterClickThrough();
  overlayWindow?.hide();
  overlayWindow?.webContents.send('ghost:state', { state: 'idle' });
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function sendError(message: string): void {
  overlayWindow?.webContents.send('ghost:state', { state: 'error', message });
}

// ─── App lifecycle ────────────────────────────────────────────────────────────

app.whenReady().then(() => {
  createOverlayWindow();

  registerHotkeys(
    () => overlayWindow,
    () => { overlayWindow?.show(); enterInputMode(); },   // onTrigger
    () => dismissOverlay(),                                // onDismiss
  );

  // Renderer asks to dismiss (Escape key in QueryPanel, etc.)
  ipcMain.on('ghost:dismiss', () => dismissOverlay());

  // Main pipeline: screenshot → Python service → result
  ipcMain.handle('ghost:analyze', async (_event, { query }: { query: string }) => {
    if (!overlayWindow) return;

    // 1. Switch to click-through and show thinking state BEFORE hiding the window,
    //    so the renderer can update. Then hide to avoid capturing the overlay itself.
    enterClickThrough();
    overlayWindow.webContents.send('ghost:state', { state: 'thinking' });
    overlayWindow.hide();
    await sleep(150); // let the OS composite the real screen before we capture

    // 2. Capture the primary display at physical resolution
    const { width, height, scaleFactor } = getPrimaryDisplayInfo();
    const physW = Math.round(width * scaleFactor);
    const physH = Math.round(height * scaleFactor);

    let screenshotB64: string;
    try {
      const sources = await desktopCapturer.getSources({
        types: ['screen'],
        thumbnailSize: { width: physW, height: physH },
      });
      if (!sources.length) throw new Error('No screen sources returned');
      screenshotB64 = sources[0].thumbnail.toDataURL()
        .replace(/^data:image\/\w+;base64,/, '');
    } catch (err) {
      overlayWindow.show();
      sendError('Screen capture failed — check Windows screen recording permissions.');
      return;
    }

    // 3. Show overlay again in thinking state while the HTTP request is in flight
    overlayWindow.show();

    // 4. POST to Python service
    try {
      const res = await fetch(`${PYTHON_URL}/analyze`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ screenshot_b64: screenshotB64, query }),
      });

      const text = await res.text();

      if (!res.ok) {
        sendError(`Service error ${res.status} — ${text.slice(0, 120)}`);
        return;
      }

      const result = JSON.parse(text);

      // 5. Convert physical-px coords → logical-px for the overlay window
      overlayWindow.webContents.send('ghost:state', {
        state: 'result',
        x: Math.round(result.target_x / scaleFactor),
        y: Math.round(result.target_y / scaleFactor),
        label: result.label,
        tooltip: result.tooltip,
      });
    } catch {
      sendError(
        'Cannot reach the Python service on port 8000. ' +
        'Start it with: cd python-service && uvicorn main:app --host 127.0.0.1 --port 8000',
      );
    }
  });
});

app.on('will-quit', () => {
  globalShortcut.unregisterAll();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
