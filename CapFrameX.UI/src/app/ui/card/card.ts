import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** A titled block on the secondary surface. */
@Component({
  selector: 'cx-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cx-card' },
  template: `
    <h4>{{ heading() }}</h4>
    <ng-content />
  `,
  styles: `
    :host {
      display: block;
      padding: var(--cx-space-5) var(--cx-space-6);
      border-radius: var(--cx-radius);
      background: var(--cx-bg-subtle);
    }

    h4 {
      margin-bottom: var(--cx-space-3);
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
      font-weight: var(--cx-weight-medium);
      letter-spacing: 0.04em;
    }
  `,
})
export class Card {
  readonly heading = input.required<string>();
}
