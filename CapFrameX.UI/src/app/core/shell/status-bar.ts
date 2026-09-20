import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { ErrorSurface } from '../http/error-surface';
import { Icon } from '../../ui/icon/icon';
import { ServiceStatus } from '../service/service-status';

/**
 * The status bar along the bottom.
 *
 * It answers one question at a glance - is the service there - and carries the failure surface,
 * because a refused request is the other thing a user needs to see without going looking for it.
 */
@Component({
  selector: 'cx-status-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'cx-status-bar' },
  template: `
    <span class="service" [class.online]="status.online()">
      <span class="dot" aria-hidden="true"></span>
      {{ status.description() }}
    </span>

    <span class="spacer"></span>

    @for (error of errors.errors(); track error.id) {
      <span class="error" role="status">
        <span class="title">{{ error.title }}</span>
        @if (error.detail) {
          <span class="detail">{{ error.detail }}</span>
        }
        <button type="button" (click)="errors.dismiss(error.id)" aria-label="Dismiss">
          <cx-icon name="x" [size]="14" />
        </button>
      </span>
    }
  `,
  styles: `
    .cx-status-bar {
      display: flex;
      align-items: center;
      gap: var(--cx-space-3);
      height: 26px;
      padding: 0 var(--cx-space-5);
      border-top: 0.5px solid var(--cx-line);
      background: var(--cx-bg-subtle);
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    .service {
      display: inline-flex;
      align-items: center;
      gap: var(--cx-space-2);
    }

    .dot {
      width: 7px;
      height: 7px;
      border-radius: var(--cx-radius-pill);
      background: var(--cx-text-faint);
    }

    .service.online .dot {
      background: var(--cx-good);
    }

    .spacer {
      flex: 1;
    }

    .error {
      display: inline-flex;
      align-items: center;
      gap: var(--cx-space-2);
      padding: 1px var(--cx-space-2);
      border-radius: var(--cx-radius);
      background: var(--cx-alert-bg);
      color: var(--cx-alert-text);
      max-width: 60ch;
    }

    .title {
      font-weight: var(--cx-weight-medium);
    }

    .detail {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    button {
      display: inline-flex;
      align-items: center;
      padding: 0;
      border: 0;
      background: transparent;
      color: inherit;
      cursor: pointer;
    }
  `,
})
export class StatusBar {
  protected readonly status = inject(ServiceStatus);
  protected readonly errors = inject(ErrorSurface);
}
