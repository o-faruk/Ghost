import { useEffect, useRef, useState } from 'react';

interface QueryPanelProps {
  onSubmit: (query: string) => void;
  onDismiss: () => void;
}

export function QueryPanel({ onSubmit, onDismiss }: QueryPanelProps) {
  const [query, setQuery] = useState('');
  const [isDragging, setIsDragging] = useState(false);
  // null = use CSS centering; once dragged, stores top-left pixel position
  const [position, setPosition] = useState<{ x: number; y: number } | null>(null);

  const inputRef = useRef<HTMLInputElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  const dragOrigin = useRef<{
    mouseX: number;
    mouseY: number;
    elemX: number;
    elemY: number;
  } | null>(null);

  useEffect(() => {
    const t = setTimeout(() => inputRef.current?.focus(), 50);
    return () => clearTimeout(t);
  }, []);

  // Global mouse tracking — only active while dragging (dragOrigin is set)
  useEffect(() => {
    function onMove(e: MouseEvent) {
      if (!dragOrigin.current) return;
      const dx = e.clientX - dragOrigin.current.mouseX;
      const dy = e.clientY - dragOrigin.current.mouseY;
      const panelW = panelRef.current?.offsetWidth ?? 580;
      const panelH = panelRef.current?.offsetHeight ?? 120;
      setPosition({
        x: Math.max(0, Math.min(window.innerWidth - panelW, dragOrigin.current.elemX + dx)),
        y: Math.max(0, Math.min(window.innerHeight - panelH, dragOrigin.current.elemY + dy)),
      });
    }

    function onUp() {
      if (dragOrigin.current) {
        dragOrigin.current = null;
        setIsDragging(false);
        // Re-focus the input after drag ends
        setTimeout(() => inputRef.current?.focus(), 0);
      }
    }

    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, []);

  function handleDragStart(e: React.MouseEvent) {
    if (!panelRef.current) return;
    const rect = panelRef.current.getBoundingClientRect();
    dragOrigin.current = {
      mouseX: e.clientX,
      mouseY: e.clientY,
      elemX: rect.left,
      elemY: rect.top,
    };
    // Switch from CSS-centering to pixel positioning immediately so the panel
    // doesn't jump when we apply the position state
    setPosition({ x: rect.left, y: rect.top });
    setIsDragging(true);
    e.preventDefault(); // prevent text selection while dragging
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && query.trim()) onSubmit(query.trim());
    else if (e.key === 'Escape') onDismiss();
  }

  // Outer div: positioning only — no background, no shadow, no animation transform conflict
  const outerStyle: React.CSSProperties = position
    ? { position: 'absolute', left: position.x, top: position.y, width: 580 }
    : { position: 'absolute', top: '50%', left: '50%', transform: 'translate(-50%, -50%)', width: 580 };

  return (
    <div ref={panelRef} style={{ ...outerStyle, pointerEvents: 'auto' }}>
      {/* Inner div: appearance + ghost-appear animation (no transform conflict with outer) */}
      <div
        style={{
          background: 'rgba(12, 8, 24, 0.94)',
          borderRadius: 14,
          boxShadow: '0 8px 48px rgba(0,0,0,0.7), 0 0 0 1px rgba(139,92,246,0.35)',
          animation: 'ghost-appear 0.2s ease-out forwards',
          overflow: 'hidden',
        }}
      >
        {/* Drag handle */}
        <div
          onMouseDown={handleDragStart}
          style={{
            padding: '13px 18px 11px',
            borderBottom: '1px solid rgba(139,92,246,0.12)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            cursor: isDragging ? 'grabbing' : 'grab',
            userSelect: 'none',
          }}
        >
          <span
            style={{
              color: 'rgba(255,255,255,0.45)',
              fontSize: 12,
              fontFamily: 'system-ui, -apple-system, sans-serif',
            }}
          >
            👻 Ghost
          </span>
          <span
            style={{
              color: 'rgba(255,255,255,0.2)',
              fontSize: 11,
              fontFamily: 'system-ui, -apple-system, sans-serif',
            }}
          >
            drag to move · Alt+Shift+G to dismiss
          </span>
        </div>

        {/* Input area */}
        <div style={{ padding: '14px 18px 16px' }}>
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
            }}
            onFocus={e => { e.target.style.borderColor = 'rgba(139,92,246,0.85)'; }}
            onBlur={e => { e.target.style.borderColor = 'rgba(139,92,246,0.45)'; }}
          />

          <div
            style={{
              marginTop: 9,
              fontSize: 11,
              fontFamily: 'system-ui, -apple-system, sans-serif',
              color: 'rgba(255,255,255,0.25)',
              textAlign: 'right',
              height: 15,
              opacity: query.trim() ? 1 : 0,
              transition: 'opacity 0.15s',
            }}
          >
            Press Enter to analyze
          </div>
        </div>
      </div>
    </div>
  );
}
