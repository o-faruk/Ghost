import { useEffect, useRef, useState } from 'react';

interface QueryPanelProps {
  onSubmit: (query: string) => void;
  onDismiss: () => void;
}

export function QueryPanel({ onSubmit, onDismiss }: QueryPanelProps) {
  const [query, setQuery] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    // Small delay so Electron finishes making the window focusable before we focus the input
    const t = setTimeout(() => inputRef.current?.focus(), 50);
    return () => clearTimeout(t);
  }, []);

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && query.trim()) {
      onSubmit(query.trim());
    } else if (e.key === 'Escape') {
      onDismiss();
    }
  }

  return (
    <div
      style={{
        position: 'absolute',
        top: 80,
        left: '50%',
        transform: 'translateX(-50%)',
        width: 580,
        background: 'rgba(12, 8, 24, 0.94)',
        borderRadius: 14,
        padding: '18px 22px 16px',
        boxShadow: '0 8px 48px rgba(0,0,0,0.7), 0 0 0 1px rgba(139,92,246,0.35)',
        pointerEvents: 'auto',
        animation: 'ghost-appear 0.2s ease-out forwards',
      }}
    >
      <div
        style={{
          color: 'rgba(255,255,255,0.4)',
          fontSize: 12,
          fontFamily: 'system-ui, -apple-system, sans-serif',
          marginBottom: 12,
          letterSpacing: '0.02em',
        }}
      >
        👻 Ghost · Alt+Shift+G to dismiss
      </div>

      <input
        ref={inputRef}
        value={query}
        onChange={e => setQuery(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="What do you need help with?"
        style={{
          width: '100%',
          background: 'rgba(255,255,255,0.07)',
          border: '1.5px solid rgba(139,92,246,0.45)',
          borderRadius: 9,
          color: 'white',
          fontSize: 16,
          fontFamily: 'system-ui, -apple-system, sans-serif',
          padding: '11px 14px',
          outline: 'none',
          boxSizing: 'border-box',
          caretColor: 'rgba(139,92,246,1)',
          transition: 'border-color 0.15s',
        }}
        onFocus={e => { e.target.style.borderColor = 'rgba(139,92,246,0.8)'; }}
        onBlur={e => { e.target.style.borderColor = 'rgba(139,92,246,0.45)'; }}
      />

      <div
        style={{
          marginTop: 10,
          fontSize: 11,
          fontFamily: 'system-ui, -apple-system, sans-serif',
          color: 'rgba(255,255,255,0.25)',
          textAlign: 'right',
          height: 16,
          transition: 'opacity 0.15s',
          opacity: query.trim() ? 1 : 0,
        }}
      >
        Press Enter to analyze
      </div>
    </div>
  );
}
