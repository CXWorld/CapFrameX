import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';

import { RUNTIME_CONFIG } from '../config/runtime-config';
import { ErrorSurface } from './error-surface';
import { describeError } from './problem-details';
import { SILENT } from './silent';

/** The header the service authenticates by. */
export const TOKEN_HEADER = 'X-CapFrameX-Token';

/**
 * Turns a relative API path into a request the service will accept.
 *
 * Two jobs, and they belong together because both are about the one service this frontend talks
 * to: the base URL it lives at, and the token it demands. Everything else in the application can
 * then ask for `/api/records` and be right on both platforms and under `ng serve`.
 *
 * Failures are described once, here, so no caller has to know what a problem-details body looks
 * like - and so a user sees the service's own explanation rather than "Http failure response".
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(RUNTIME_CONFIG);
  const errors = inject(ErrorSurface);

  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const headers = config.token
    ? request.headers.set(TOKEN_HEADER, config.token)
    : request.headers;

  return next(request.clone({ url: config.apiBaseUrl + request.url, headers })).pipe(
    catchError((failure: unknown) => {
      if (failure instanceof HttpErrorResponse && !request.context.get(SILENT)) {
        errors.report(describeError(failure));
      }

      return throwError(() => failure);
    }),
  );
};
