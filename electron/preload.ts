import { contextBridge, ipcRenderer } from 'electron';

// GhostState mirrors overlay-renderer/src/types.ts — keep in sync manually until
// we extract a shared package (out of MVP scope).
type GhostState =
  | { state: 'idle' }
  | { state: 'input' }
  | { state: 'thinking' }
  | { state: 'result'; x: number; y: number; label: string; tooltip: string }
  | { state: 'error'; message: string };

contextBridge.exposeInMainWorld('ghostAPI', {
  /** Subscribe to state changes driven by the main process. Call once on mount. */
  onStateChange: (cb: (s: GhostState) => void) => {
    ipcRenderer.on('ghost:state', (_e, s: GhostState) => cb(s));
  },

  /** Kick off the screenshot → Python service → result pipeline. */
  analyze: (query: string): Promise<void> =>
    ipcRenderer.invoke('ghost:analyze', { query }),

  /** Hide the overlay and return to idle. */
  dismiss: (): void => {
    ipcRenderer.send('ghost:dismiss');
  },
});
