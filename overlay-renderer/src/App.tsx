import { GhostCursor } from './components/GhostCursor';

// Hardcoded target for scaffold verification.
// Replace with IPC-driven state (window.ghostAPI.onShowGhost) in the next milestone.
const DEMO_TARGET = { x: 600, y: 400, tooltip: 'Click here' };

export default function App() {
  return (
    <div className="relative w-screen h-screen overflow-hidden pointer-events-none">
      <GhostCursor x={DEMO_TARGET.x} y={DEMO_TARGET.y} tooltip={DEMO_TARGET.tooltip} />
    </div>
  );
}
