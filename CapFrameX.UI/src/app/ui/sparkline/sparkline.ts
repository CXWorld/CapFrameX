import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The shape of a capture, in a strip a few pixels tall.
 *
 * The points arrive already decimated by the service - min and max per bucket, so a single long
 * frame among ten thousand survives - and this only has to place them. Drawing more than it is
 * given would smooth away the very thing it is for.
 */
@Component({
  selector: 'cx-sparkline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cx-sparkline' },
  template: `
    @if (path()) {
      <svg [attr.viewBox]="'0 0 ' + width() + ' ' + height()" preserveAspectRatio="none" aria-hidden="true">
        <path [attr.d]="path()" />
      </svg>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    svg {
      display: block;
      width: 100%;
      height: 100%;
    }

    path {
      fill: none;
      stroke: var(--cx-series-1);
      stroke-width: 1.1;
      stroke-linejoin: round;

      /* The strip is squashed horizontally, and without this the stroke is squashed with it. */
      vector-effect: non-scaling-stroke;
    }
  `,
})
export class Sparkline {
  /** The decimated frame times, in milliseconds. */
  readonly points = input.required<readonly number[]>();

  /** Width of the drawing space; the element itself scales to its container. */
  readonly width = input(160);

  /** Height of the drawing space. */
  readonly height = input(20);

  protected readonly path = computed(() => {
    const values = this.points();

    if (values.length < 2) {
      return '';
    }

    const low = Math.min(...values);
    const high = Math.max(...values);
    const span = high - low;
    const stepX = this.width() / (values.length - 1);

    // A capture with no variation at all draws through the middle rather than along an edge, which
    // reads as "nothing happened" instead of "the value was zero".
    const y = (value: number) =>
      span === 0 ? this.height() / 2 : this.height() - ((value - low) / span) * this.height();

    return values.map((value, index) => `${index === 0 ? 'M' : 'L'}${(index * stepX).toFixed(2)},${y(value).toFixed(2)}`).join(' ');
  });
}
