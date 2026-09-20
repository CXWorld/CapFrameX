import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';

import { RUNTIME_CONFIG, readRuntimeConfig } from './core/config/runtime-config';
import { apiInterceptor } from './core/http/api.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),

    // Zoneless: every view in this application is driven by signals, and the live ones update
    // several times a second. Zone.js would re-check the whole tree on every timer the charts use.
    provideZonelessChangeDetection(),

    provideRouter(routes, withComponentInputBinding()),

    // The interceptor is what turns '/api/...' into a request the service accepts, so no caller
    // has to know the base URL or the token.
    provideHttpClient(withInterceptors([apiInterceptor])),

    { provide: RUNTIME_CONFIG, useFactory: () => readRuntimeConfig() },
  ],
};
