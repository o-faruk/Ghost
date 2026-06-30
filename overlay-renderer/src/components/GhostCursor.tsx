import { motion } from 'framer-motion';

interface GhostCursorProps {
  x: number;
  y: number;
  tooltip?: string;
}

export function GhostCursor({ x, y, tooltip }: GhostCursorProps) {
  return (
    <motion.div
      className="absolute pointer-events-none"
      style={{ left: x, top: y }}
      initial={{ opacity: 0, scale: 0.6 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.25, ease: 'easeOut' }}
    >
      {/* Pulsing glow behind the cursor tip */}
      <motion.div
        className="absolute rounded-full"
        style={{
          width: 40,
          height: 40,
          left: -12,
          top: -12,
          background: 'radial-gradient(circle, rgba(139,92,246,0.4) 0%, transparent 70%)',
        }}
        animate={{ scale: [1, 1.7, 1], opacity: [0.6, 0.1, 0.6] }}
        transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
      />

      {/* Floating cursor with bob animation */}
      <motion.div
        animate={{ y: [0, -6, 0] }}
        transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut' }}
      >
        <svg
          width="26"
          height="30"
          viewBox="0 0 26 30"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
        >
          {/*
            Arrow cursor — hotspot is at (0,0) so the tip aligns with the target coordinate.
            Path traces: tip → down left edge → notch diagonal → tail spike → back up → tail top → close.
          */}
          <path
            d="M 1 1 L 1 21 L 6 16 L 9 23 L 12 22 L 9 15 L 15 15 Z"
            fill="white"
            stroke="rgba(109,40,217,0.85)"
            strokeWidth="1.4"
            strokeLinejoin="round"
          />
        </svg>
      </motion.div>

      {/* Tooltip pill */}
      {tooltip && (
        <motion.div
          className="absolute whitespace-nowrap text-white text-sm font-medium px-3 py-1.5 rounded-lg backdrop-blur-sm"
          style={{
            left: 30,
            top: -4,
            background: 'rgba(109, 40, 217, 0.85)',
            boxShadow: '0 4px 16px rgba(109,40,217,0.45)',
          }}
          initial={{ opacity: 0, x: -8 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ delay: 0.18, duration: 0.2, ease: 'easeOut' }}
        >
          {tooltip}
        </motion.div>
      )}
    </motion.div>
  );
}
