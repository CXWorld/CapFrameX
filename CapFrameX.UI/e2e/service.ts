import { existsSync, readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join } from 'node:path';

/** Where the running service publishes its port and token. */
function runtimeDirectory(): string {
  if (process.platform === 'win32') {
    return join(process.env.LOCALAPPDATA ?? join(homedir(), 'AppData', 'Local'), 'CapFrameX', 'run');
  }

  const runtime = process.env.XDG_RUNTIME_DIR;

  return runtime ? join(runtime, 'capframex') : join(homedir(), '.local', 'state', 'capframex');
}

function read(name: string): string {
  const path = join(runtimeDirectory(), name);

  return existsSync(path) ? readFileSync(path, 'utf8').trim() : '';
}

/** Whether a service is running for these tests to talk to. */
export function serviceIsRunning(): boolean {
  return read('service.token') !== '';
}

/** Asks the running service something, the way the frontend would. */
export async function api<T>(path: string): Promise<T> {
  const port = read('service.port') || '17337';
  const response = await fetch(`http://127.0.0.1:${port}${path}`, {
    headers: { 'X-CapFrameX-Token': read('service.token') },
  });

  if (!response.ok) {
    throw new Error(`${path} answered ${response.status}`);
  }

  return (await response.json()) as T;
}
