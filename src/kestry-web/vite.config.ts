import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: { outDir: '../Kestry.Server/wwwroot', emptyOutDir: true },
  server: { proxy: { '/api': 'http://127.0.0.1:8080', '/health': 'http://127.0.0.1:8080' } },
  test: {
    globals: true, environment: 'jsdom', setupFiles: ['./src/test-setup.ts'], css: false },
});
