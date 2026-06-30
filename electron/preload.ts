import { contextBridge, ipcRenderer } from 'electron';

export interface GhostTarget {
  x: number;       // logical px — already scaled from OmniParser physical coords
  y: number;
  label: string;   // element label from OmniParser
  tooltip: string; // human-readable instruction from Claude
}

// Exposed as window.ghostAPI in the renderer (contextIsolation: true)
contextBridge.exposeInMainWorld('ghostAPI', {
  onShowGhost: (cb: (target: GhostTarget) => void) => {
    ipcRenderer.on('ghost:show', (_e, target: GhostTarget) => cb(target));
  },
  onHideGhost: (cb: () => void) => {
    ipcRenderer.on('ghost:hide', () => cb());
  },
  onThinking: (cb: (active: boolean) => void) => {
    ipcRenderer.on('ghost:thinking', (_e, active: boolean) => cb(active));
  },
});
