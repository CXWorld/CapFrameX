import { InjectionToken } from '@angular/core';

/**
 * What the frontend has to be told about the service it talks to.
 */
export interface RuntimeConfig {
  /** Where the service answers, without a trailing slash. */
  readonly apiBaseUrl: string;

  /**
   * The session token the service accepts.
   *
   * The service generates a fresh one every start and refuses every request without it, so this
   * cannot be baked into the build - it is handed over at start-up.
   */
  readonly token: string;
}

/** What the desktop host sets on `window` before any page script runs. */
export interface CxHostGlobals {
  readonly apiBaseUrl?: string;
  readonly token?: string;
}

declare global {
  interface Window {
    __CX__?: CxHostGlobals;
  }
}

export const RUNTIME_CONFIG = new InjectionToken<RuntimeConfig>('CapFrameX runtime configuration');

/** Where the service listens unless the host says otherwise. */
export const DEFAULT_API_BASE_URL = 'http://127.0.0.1:1337';

/**
 * Reads the configuration the host left on `window`.
 *
 * Under `ng serve` there is no host, so the base URL falls back to the port the service uses and
 * the token is empty - which is exactly what a developer gets: the service refuses the request and
 * says so, rather than the frontend silently talking to nothing. Paste the token from the service's
 * run directory into `window.__CX__` to work against a running one.
 */
export function readRuntimeConfig(globals: CxHostGlobals | undefined = globalThis.window?.__CX__): RuntimeConfig {
  return {
    apiBaseUrl: trimTrailingSlash(globals?.apiBaseUrl ?? DEFAULT_API_BASE_URL),
    token: globals?.token ?? '',
  };
}

function trimTrailingSlash(url: string): string {
  return url.endsWith('/') ? url.slice(0, -1) : url;
}
