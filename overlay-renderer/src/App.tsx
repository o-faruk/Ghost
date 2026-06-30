import { useEffect, useState } from 'react';
import type { GhostState } from './types';
import { QueryPanel } from './components/QueryPanel';
import { ThinkingIndicator } from './components/ThinkingIndicator';
import { GhostCursor } from './components/GhostCursor';

export default function App() {
  const [gs, setGs] = useState<GhostState>({ state: 'idle' });

  useEffect(() => {
    window.ghostAPI.onStateChange(setGs);
  }, []);

  return (
    <div
      style={{
        position: 'relative',
        width: '100vw',
        height: '100vh',
        overflow: 'hidden',
        pointerEvents: 'none',
      }}
    >
      {gs.state === 'input' && (
        <QueryPanel
          onSubmit={query => window.ghostAPI.analyze(query)}
          onDismiss={() => window.ghostAPI.dismiss()}
        />
      )}

      {gs.state === 'thinking' && <ThinkingIndicator />}

      {gs.state === 'result' && (
        <GhostCursor x={gs.x} y={gs.y} tooltip={gs.tooltip} />
      )}

      {gs.state === 'error' && (
        // Outer: position only — keeps transform: translate() out of animation conflict
        <div
          style={{
            position: 'absolute',
            top: '50%',
            left: '50%',
            transform: 'translate(-50%, -50%)',
            width: 520,
          }}
        >
          {/* Inner: animation (ghost-appear uses transform: scale, no conflict here) */}
          <div
            style={{
              background: 'rgba(24, 8, 8, 0.94)',
              border: '1px solid rgba(239,68,68,0.45)',
              borderRadius: 10,
              padding: '12px 18px',
              boxShadow: '0 4px 24px rgba(0,0,0,0.55)',
              animation: 'ghost-appear 0.2s ease-out forwards',
            }}
          >
            <span
              style={{
                color: 'rgba(252,165,165,0.95)',
                fontSize: 13,
                fontFamily: 'system-ui, -apple-system, sans-serif',
              }}
            >
              ⚠ {gs.message}
            </span>
            <div
              style={{
                color: 'rgba(255,255,255,0.3)',
                fontSize: 11,
                fontFamily: 'system-ui',
                marginTop: 6,
              }}
            >
              Press Alt+Shift+G to try again
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
