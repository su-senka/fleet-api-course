/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * The dev server, and the one piece of configuration that changes between week 1 and week 2.
 *
 * Week 1: /api goes straight to the Fleet API on :5101, with the /api prefix stripped, because
 * that API serves /depots rather than /api/depots. There is no session yet and the API has no
 * authentication yet, so this is the whole story.
 *
 * Week 2: /api goes to the BFF on :5180 instead, and the prefix is no longer stripped here - the
 * BFF removes it itself (see its appsettings.json) on the way to the API, and attaches the
 * signed-in user's access token while it is there.
 *
 * In production none of this exists. The BFF serves the built bundle, so the SPA and /api are
 * already the same origin. That is why every fetch in src/ uses a relative path: an absolute URL
 * would work here and break the moment it is deployed.
 */
const apiTarget = process.env.API_URL ?? 'http://localhost:5101';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },

      // TODO(week-2): point /api at the BFF instead and delete the rewrite above, then forward
      // the session routes too - without these three the OIDC round trip 404s in development
      // while working perfectly in production, which is a confusing hour.
      //
      //   '/api':                   { target: 'http://localhost:5180' },
      //   '/bff':                   { target: 'http://localhost:5180' },
      //   '/signin-oidc':           { target: 'http://localhost:5180' },
      //   '/signout-callback-oidc': { target: 'http://localhost:5180' },
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
