import { defineConfig, type Plugin } from 'vite';
import path from 'node:path';

const apiTarget = () => process.env.SERVICES__API__HTTPS__0
  ?? process.env.SERVICES__API__HTTP__0
  ?? process.env.SERVER_HTTPS
  ?? process.env.SERVER_HTTP
  ?? 'http://localhost:5280';

/** Deep links like /app/features should serve the SPA entry (same as production). */
function workbenchSpaFallback(): Plugin {
  return {
    name: 'workbench-spa-fallback',
    configureServer(server) {
      return () => {
        server.middlewares.use((req, _res, next) => {
          const raw = req.url ?? '';
          const pathOnly = raw.split('?')[0] ?? '';
          if (pathOnly === '/app/features' || pathOnly === '/app/features/') {
            const q = raw.includes('?') ? '?' + raw.split('?').slice(1).join('?') : '';
            req.url = '/app/' + q;
          }
          next();
        });
      };
    },
  };
}

// https://vite.dev/config/
export default defineConfig({
  base: '/app/',
  publicDir: false,
  plugins: [workbenchSpaFallback()],
  resolve: {
    alias: {
      '@explorer': path.resolve(__dirname, '../../DomainModeling.AspNetCore/wwwroot'),
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    fs: {
      allow: [path.resolve(__dirname, '../..')],
    },
    proxy: {
      '/domain-model': {
        target: apiTarget(),
        changeOrigin: true,
      },
    },
  },
});
