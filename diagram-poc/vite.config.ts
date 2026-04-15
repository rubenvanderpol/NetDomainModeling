import { defineConfig } from "vite";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = dirname(fileURLToPath(import.meta.url));
const rendererRoot = resolve(projectRoot, "renderer");

export default defineConfig({
  root: rendererRoot,
  base: "./",
  build: {
    outDir: "dist",
    emptyOutDir: true,
    rollupOptions: {
      input: resolve(rendererRoot, "index.html"),
    },
  },
});
