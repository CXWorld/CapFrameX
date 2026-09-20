/**
 * Serves the built frontend the way the desktop host will.
 *
 * The real host is the CEF shell from WP-H1: it starts the service, then loads the page with
 * `window.__CX__` already set. This does the same with a static server, which is what makes it
 * possible to see the whole thing work before that host exists - and what proves the frontend
 * needs nothing from the build to find the service.
 *
 * It reads the port and the token the running service published, so there is nothing to paste.
 *
 *   node tools/dev-host.mjs
 */
import { createReadStream, existsSync, readFileSync } from 'node:fs';
import { createServer } from 'node:http';
import { homedir } from 'node:os';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..', 'dist', 'capframex-ui', 'browser');

// Port 4200 because that is the origin the service's guard accepts during development.
const PORT = 4200;

const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.json': 'application/json',
  '.woff2': 'font/woff2',
};

function runtimeDirectory() {
  if (process.platform === 'win32') {
    return join(process.env.LOCALAPPDATA ?? join(homedir(), 'AppData', 'Local'), 'CapFrameX', 'run');
  }

  const runtime = process.env.XDG_RUNTIME_DIR;

  return runtime ? join(runtime, 'capframex') : join(homedir(), '.local', 'state', 'capframex');
}

function readService() {
  const folder = runtimeDirectory();
  const read = (name) => {
    const path = join(folder, name);

    return existsSync(path) ? readFileSync(path, 'utf8').trim() : '';
  };

  const port = read('service.port') || '17337';
  const token = read('service.token');

  if (!token) {
    console.warn(`No token in ${folder}. Start the service first, or the API will answer 401.`);
  }

  return { apiBaseUrl: `http://127.0.0.1:${port}`, token };
}

function indexHtml() {
  const html = readFileSync(join(root, 'index.html'), 'utf8');
  const service = readService();

  // Before any page script, which is the contract the frontend's runtime config assumes.
  const injected = `<script>window.__CX__=${JSON.stringify(service)};</script>`;

  return html.replace('</head>', `  ${injected}\n</head>`);
}

createServer((request, response) => {
  const url = new URL(request.url ?? '/', `http://localhost:${PORT}`);
  const file = normalize(join(root, decodeURIComponent(url.pathname)));

  // Resolve first, then check the result is still under the output folder. Inspecting the request
  // path for traversal means guessing every way it can be spelled; comparing the resolved path
  // does not.
  if (file.startsWith(root) && existsSync(file) && extname(file)) {
    response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' });
    createReadStream(file).pipe(response);

    return;
  }

  // Everything else is the application: the router owns the path, not this server.
  const html = indexHtml();
  response.writeHead(200, { 'content-type': types['.html'] });
  response.end(html);
}).listen(PORT, '127.0.0.1', () => {
  const service = readService();
  console.log(`CapFrameX dev host on http://localhost:${PORT} -> ${service.apiBaseUrl}`);
});
