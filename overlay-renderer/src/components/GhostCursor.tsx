interface GhostCursorProps {
  x: number;
  y: number;
  tooltip?: string;
}

export function GhostCursor({ x, y, tooltip }: GhostCursorProps) {
  return (
    <div
      style={{
        position: 'absolute',
        left: x,
        top: y,
        pointerEvents: 'none',
        animation: 'ghost-appear 0.25s ease-out forwards',
      }}
    >
      {/* Pulsing glow ring */}
      <div
        style={{
          position: 'absolute',
          width: 40,
          height: 40,
          left: -12,
          top: -12,
          borderRadius: '50%',
          background: 'radial-gradient(circle, rgba(139,92,246,0.4) 0%, transparent 70%)',
          animation: 'ghost-pulse 2s ease-in-out infinite',
        }}
      />

      {/* Cursor with floating bob */}
      <div style={{ animation: 'ghost-bob 1.8s ease-in-out infinite' }}>
        <svg width="26" height="30" viewBox="0 0 26 30" fill="none" xmlns="http://www.w3.org/2000/svg">
          {/* Arrow hotspot at (0,0) so left/top aligns with the target point */}
          <path
            d="M 1 1 L 1 21 L 6 16 L 9 23 L 12 22 L 9 15 L 15 15 Z"
            fill="white"
            stroke="rgba(109,40,217,0.85)"
            strokeWidth="1.4"
            strokeLinejoin="round"
          />
        </svg>
      </div>

      {/* Tooltip */}
      {tooltip && (
        <div
          style={{
            position: 'absolute',
            left: 30,
            top: -4,
            background: 'rgba(109, 40, 217, 0.92)',
            color: 'white',
            padding: '6px 12px',
            borderRadius: 8,
            fontSize: 14,
            fontFamily: 'system-ui, -apple-system, sans-serif',
            whiteSpace: 'nowrap',
            boxShadow: '0 4px 16px rgba(109,40,217,0.5)',
            animation: 'ghost-tooltip-in 0.2s 0.15s ease-out both',
          }}
        >
          {tooltip}
        </div>
      )}
    </div>
  );
}
