import { ChangeDetectionStrategy, Component } from '@angular/core';

/** A fact about the machine or the capture, short enough to read at a glance. */
@Component({
  selector: 'cx-chip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cx-chip' },
  template: '<ng-content />',
  styles: `
    .cx-chip {
      display: inline-flex;
      align-items: center;
      padding: 3px var(--cx-space-2);
      border-radius: var(--cx-radius-pill);
      background: var(--cx-bg-subtle);
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
      white-space: nowrap;
    }
  `,
})
export class Chip {}
