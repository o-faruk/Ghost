export function ThinkingIndicator() {
  return (
    // Outer: position only, no transform conflict with ghost-appear animation
    <div
      style={{
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
      }}
    >
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 10,
          background: 'rgba(12, 8, 24, 0.9)',
          borderRadius: 99,
          padding: '10px 20px 10px 16px',
          boxShadow: '0 4px 28px rgba(0,0,0,0.55), 0 0 0 1px rgba(139,92,246,0.2)',
          animation: 'ghost-appear 0.2s ease-out forwards',
          whiteSpace: 'nowrap',
        }}
      >
        <div
          style={{
            width: 8,
            height: 8,
            borderRadius: '50%',
            background: 'rgba(139,92,246,0.95)',
            flexShrink: 0,
            animation: 'ghost-pulse 1s ease-in-out infinite',
          }}
        />
        <span
          style={{
            color: 'rgba(255,255,255,0.75)',
            fontSize: 14,
            fontFamily: 'system-ui, -apple-system, sans-serif',
          }}
        >
          Analyzing…
        </span>
      </div>
    </div>
  );
}
