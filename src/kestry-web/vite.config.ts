import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: { outDir: '../Kestry.Server/wwwroot', emptyOutDir: true },
  // In dev the proxy rewrites Host to the backend's, so start the backend with
  // Kestry__AllowedOrigins__0=http://localhost:5173 or its origin check rejects POSTs (see README).
  server: { port: 5173, strictPort: true, proxy: { '/api': 'http://127.0.0.1:8080' } },
  test: {
    globals: true, environment: 'jsdom', setupFiles: ['./src/test-setup.ts'], css: false },
});
