import { defineConfig } from 'vite';

const apiTarget = () => process.env.SERVICES__API__HTTPS__0
  ?? process.env.SERVICES__API__HTTP__0
  ?? process.env.SERVER_HTTPS
  ?? process.env.SERVER_HTTP
  ?? 'http://localhost:5280';

// https://vite.dev/config/
export default defineConfig({
  server: {
    proxy: {
      '/domain-model': {
        target: apiTarget(),
        changeOrigin: true,
      },
    },
  },
});
