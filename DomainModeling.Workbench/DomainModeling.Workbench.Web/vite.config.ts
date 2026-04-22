import { defineConfig, type Plugin } from 'vite';
import path from 'node:path';

const explorerMainPath = path.resolve(__dirname, 'src/explorer/domain-model-main.ts');

/** Side-effect import of explorer without pulling it into strict `tsc` project graph. */
function virtualExplorerMain(): Plugin {
  const virtualId = '\0virtual:explorer-main';
  return {
    name: 'virtual-explorer-main',
    resolveId(id) {
      if (id === 'virtual:explorer-main') return virtualId;
      return undefined;
    },
    load(id) {
      if (id === virtualId) {
        return `import ${JSON.stringify(explorerMainPath)};\n`;
      }
      return undefined;
    },
  };
}

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
  plugins: [workbenchSpaFallback(), virtualExplorerMain()],
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/domain-model': {
        target: apiTarget(),
        changeOrigin: true,
      },
    },
  },
});
