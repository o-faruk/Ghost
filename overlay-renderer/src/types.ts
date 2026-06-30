export type GhostState =
  | { state: 'idle' }
  | { state: 'input' }
  | { state: 'thinking' }
  | { state: 'result'; x: number; y: number; label: string; tooltip: string }
  | { state: 'error'; message: string };
