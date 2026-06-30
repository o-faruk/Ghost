import type { GhostState } from './types';

export {};

declare global {
  interface Window {
    ghostAPI: {
      onStateChange: (cb: (s: GhostState) => void) => void;
      analyze: (query: string) => Promise<void>;
      dismiss: () => void;
    };
  }
}
