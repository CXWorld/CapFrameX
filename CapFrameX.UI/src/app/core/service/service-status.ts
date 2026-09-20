import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { catchError, of, switchMap, timer } from 'rxjs';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';

import { ServiceHealthDto } from '../../data-access/contracts';
import { silently } from '../http/silent';

/**
 * Whether the service is answering.
 *
 * Polled rather than taken from the event stream: the stream not delivering is exactly the case
 * this has to notice, so it cannot be the thing that reports it. The interval is slow on purpose -
 * this is a light in the status bar, not a heartbeat anything depends on.
 */
@Injectable({ providedIn: 'root' })
export class ServiceStatus {
  /** How often the service is asked. */
  static readonly IntervalMs = 5000;

  private readonly http = inject(HttpClient);

  private readonly health = toSignal(
    timer(0, ServiceStatus.IntervalMs).pipe(
      switchMap(() =>
        this.http
          .get<ServiceHealthDto>('/api/health', { context: silently() })
          .pipe(catchError(() => of(null))),
      ),
      takeUntilDestroyed(),
    ),
    { initialValue: null },
  );

  /** Whether the last question was answered. */
  readonly online = computed(() => this.health()?.status === 'Healthy');

  /** What to put in the status bar. */
  readonly description = computed(() => (this.online() ? 'Service connected' : 'Service offline'));
}
