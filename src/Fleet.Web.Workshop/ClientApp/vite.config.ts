/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * In production none of this exists. The BFF serves the built bundle, so the SPA and /api are
 * already the same origin. That is why every fetch in src/ uses a relative path: an absolute URL
 * would work here and break the moment it is deployed.
 */
const bffTarget = process.env.BFF_URL ?? 'http://localhost:5180';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    proxy: {
      // The BFF removes the /api prefix itself on the way to the reference API (see its
      // appsettings.json), so - unlike week 1 - there is no rewrite here.
      '/api': { target: bffTarget, changeOrigin: true },
      '/bff': { target: bffTarget, changeOrigin: true },
      '/signin-oidc': { target: bffTarget, changeOrigin: true },
      '/signout-callback-oidc': { target: bffTarget, changeOrigin: true },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
});
