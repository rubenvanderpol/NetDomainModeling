import * as esbuild from 'esbuild';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(__dirname, '..');
const entry = path.join(webRoot, 'src', 'explorer', 'app', 'explorer-embed-entry.ts');
const outfile = path.resolve(webRoot, '../../DomainModeling.AspNetCore/wwwroot/js/explorer-bundle.js');

await esbuild.build({
  entryPoints: [entry],
  outfile,
  bundle: true,
  format: 'esm',
  platform: 'browser',
  target: 'es2020',
  logLevel: 'info',
});
