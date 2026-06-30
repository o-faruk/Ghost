import { useState, useEffect } from 'react';
import { GhostCursor } from './components/GhostCursor';

export default function App() {
  const [pos, setPos] = useState({ x: 0, y: 0 });

  useEffect(() => {
    setPos({
      x: Math.round(window.innerWidth / 2),
      y: Math.round(window.innerHeight / 2),
    });
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
      <GhostCursor x={pos.x} y={pos.y} tooltip="Click here" />

      {/* Debug label — remove once position is confirmed correct */}
      <div
        style={{
          position: 'fixed',
          top: 12,
          left: 12,
          color: 'white',
          fontSize: 11,
          fontFamily: 'monospace',
          background: 'rgba(0,0,0,0.55)',
          padding: '3px 8px',
          borderRadius: 4,
          pointerEvents: 'none',
        }}
      >
        cursor: {pos.x}×{pos.y} | window: {window.innerWidth}×{window.innerHeight}
      </div>
    </div>
  );
}
