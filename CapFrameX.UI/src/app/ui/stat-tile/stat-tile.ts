import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * One number, with what it is and what it is measured in.
 *
 * A metric the capture does not allow shows a dash rather than a zero: "not measured" and "zero
 * frames per second" are different statements, and only one of them is ever true here.
 */
@Component({
  selector: 'cx-stat-tile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cx-stat-tile' },
  template: `
    <p class="label">{{ label() }}</p>
    <p class="value">
      {{ display() }}
      @if (value() !== null && value() !== undefined) {
        <span class="unit">{{ unit() }}</span>
      }
    </p>
  `,
  styles: `
    .cx-stat-tile {
      display: block;
      padding: var(--cx-space-4) var(--cx-space-5);
      border-radius: var(--cx-radius);
      background: var(--cx-bg-subtle);
    }

    .label {
      margin-bottom: 4px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    .value {
      display: flex;
      align-items: baseline;
      gap: 3px;
      font-size: var(--cx-text-19);
      font-weight: var(--cx-weight-medium);
    }

    .unit {
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
      font-weight: var(--cx-weight-normal);
    }
  `,
})
export class StatTile {
  readonly label = input.required<string>();
  readonly value = input.required<number | null | undefined>();
  readonly unit = input('fps');

  /** How many decimals to show. The service already rounded; this only formats. */
  readonly digits = input(0);

  protected display(): string {
    const value = this.value();

    return value === null || value === undefined ? '—' : value.toFixed(this.digits());
  }
}
