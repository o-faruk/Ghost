import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // './' base is required when Electron loads the built output via file:// protocol
  base: './',
  build: {
    outDir: '../dist/renderer',
    emptyOutDir: true,
  },
});
