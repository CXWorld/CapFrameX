import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import uPlot from 'uplot';

import { Theme } from '../core/theme/theme';
import { readChartColors } from './chart-colors';

/** One curve. */
export interface ChartSeries {
  readonly label: string;
  readonly values: Float64Array | readonly (number | null)[];
}

/** What to draw. */
export interface ChartData {
  /** The shared x axis. */
  readonly x: Float64Array | readonly number[];

  /** The curves, in palette order. */
  readonly series: readonly ChartSeries[];

  /** What the x axis measures, for the label. */
  readonly xLabel?: string;

  /** What the y axis measures. */
  readonly yLabel?: string;
}

/** A single point worth pointing at. */
export interface ChartMarker {
  readonly x: number;
  readonly y: number;
  readonly label: string;
}

/**
 * A chart.
 *
 * uPlot on canvas, because the CapFrameX range is ten minutes at five hundred frames a second -
 * three hundred thousand points in one series - and an SVG chart stops being interactive long
 * before that.
 *
 * It is built and updated outside Angular's change detection: uPlot owns its canvas, and letting
 * the framework re-render around it would cost a full rebuild per frame during a zoom. What the
 * component does is hand it data, size and colours, and rebuild when those change.
 */
@Component({
  selector: 'cx-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div #host class="plot"></div>

    @if (markerAt(); as at) {
      <span class="marker-dot" [style.left.px]="at.left" [style.top.px]="at.top"></span>
      <span class="marker" [class.flip]="at.flip" [style.left.px]="at.left" [style.top.px]="at.top">
        {{ at.label }}
      </span>
    }
  `,
  styles: `
    :host {
      display: block;
      position: relative;
    }

    .plot {
      width: 100%;
      height: 100%;
    }

    /*
     * uPlot's own stylesheet, inlined: the component owns its canvas, and a global import would
     * put chart rules in the initial bundle for every view that has no chart.
     */
    ::ng-deep .uplot,
    ::ng-deep .uplot * {
      box-sizing: border-box;
    }

    ::ng-deep .u-wrap {
      position: relative;
      user-select: none;
    }

    ::ng-deep .u-over,
    ::ng-deep .u-under {
      position: absolute;
    }

    ::ng-deep .u-under {
      overflow: hidden;
    }

    ::ng-deep .u-over {
      z-index: 1;
    }

    ::ng-deep .u-axis {
      position: absolute;
    }

    ::ng-deep .uplot canvas {
      display: block;
      position: relative;
      width: 100%;
      height: 100%;
    }

    ::ng-deep .u-select {
      position: absolute;
      background: var(--cx-bg-accent);
      opacity: 0.45;
      pointer-events: none;
    }

    /*
     * pointer-events on the crosshair is load-bearing, not cosmetic: the lines follow the pointer,
     * so without it they become the event target, and uPlot only accepts mouse events whose target
     * is its overlay. Dropping it disables drag-to-zoom outright.
     */
    ::ng-deep .u-cursor-x,
    ::ng-deep .u-cursor-y {
      position: absolute;
      left: 0;
      top: 0;
      border-right: 1px dashed var(--cx-line-strong);
      pointer-events: none;
      will-change: transform;
    }

    ::ng-deep .u-hz .u-cursor-x,
    ::ng-deep .u-vt .u-cursor-y {
      height: 100%;
    }

    ::ng-deep .u-hz .u-cursor-y,
    ::ng-deep .u-vt .u-cursor-x {
      width: 100%;
      border-right: 0;
      border-bottom: 1px dashed var(--cx-line-strong);
    }

    ::ng-deep .u-cursor-pt {
      position: absolute;
      border-radius: 50%;
      border: 1px solid;
      pointer-events: none;
      will-change: transform;
    }

    ::ng-deep .u-legend {
      display: none;
    }

    /* How uPlot hides the crosshair and the selection; without it they stay on screen for good. */
    ::ng-deep .u-axis.u-off,
    ::ng-deep .u-select.u-off,
    ::ng-deep .u-cursor-x.u-off,
    ::ng-deep .u-cursor-y.u-off,
    ::ng-deep .u-cursor-pt.u-off {
      display: none;
    }

    /* A dot on the peak with the label beside it, as the mockup draws it. */
    .marker-dot {
      position: absolute;
      width: 7px;
      height: 7px;
      margin: -3.5px 0 0 -3.5px;
      border-radius: 50%;
      background: var(--cx-alert);
      pointer-events: none;
    }

    .marker {
      position: absolute;
      transform: translate(7px, -50%);
      padding: 1px 6px;
      border-radius: var(--cx-radius);
      background: var(--cx-alert-bg);
      color: var(--cx-alert-text);
      font-size: var(--cx-text-11);
      pointer-events: none;
      white-space: nowrap;
    }

    .marker.flip {
      transform: translate(calc(-100% - 7px), -50%);
    }
  `,
})
export class Chart {
  /** What to draw. */
  readonly data = input.required<ChartData>();

  /** A point worth pointing at - the worst spike, usually. */
  readonly marker = input<ChartMarker | null>(null);

  /** Whether the y axis starts at zero. */
  readonly zeroBased = input(true);

  private readonly host = viewChild.required<ElementRef<HTMLDivElement>>('host');
  private readonly theme = inject(Theme);

  /** Where to put the marker, once the plot knows where its points are. */
  protected readonly markerAt = signal<{ left: number; top: number; label: string; flip: boolean } | null>(null);

  private plot: uPlot | null = null;
  private observer: ResizeObserver | null = null;

  /** Name of the mark left when a chart finishes drawing. Read by the chart gate. */
  static readonly DrawnMark = 'cx-chart-drawn';

  /** How long the last build took, in milliseconds. For the chart gate. */
  lastBuildMs = 0;

  constructor() {
    afterNextRender(() => {
      this.observe();
      this.build();
    });

    // Data and theme both change what is on the canvas; neither can be pushed into uPlot without
    // rebuilding its scales, so both go through the same path.
    effect(() => {
      this.data();
      this.marker();
      this.theme.effective();

      if (this.plot) {
        this.build();
      }
    });

    inject(DestroyRef).onDestroy(() => {
      this.observer?.disconnect();
      this.plot?.destroy();
    });
  }

  private observe(): void {
    const element = this.host().nativeElement;

    this.observer = new ResizeObserver(() => {
      const { width, height } = element.getBoundingClientRect();

      if (this.plot && width > 0 && height > 0) {
        this.plot.setSize({ width, height });
      }
    });

    this.observer.observe(element);
  }

  private build(): void {
    const element = this.host().nativeElement;
    const { width, height } = element.getBoundingClientRect();

    if (width === 0 || height === 0) {
      return;
    }

    const started = performance.now();
    const data = this.data();
    const colors = readChartColors(element);

    this.plot?.destroy();
    element.replaceChildren();

    const axis = {
      stroke: colors.axis,
      grid: { stroke: colors.grid, width: 1 },
      ticks: { stroke: colors.grid, width: 1 },
      font: '11px "Inter", system-ui, sans-serif',
    };

    this.plot = new uPlot(
      {
        width,
        height,
        // The legend is the tile row above the chart; a second one inside it only takes space.
        legend: { show: false },
        // uPlot settles its geometry across the draw, not in the constructor, so the marker is
        // placed from its own hooks rather than from a measurement taken too early.
        hooks: {
          // After uPlot has put pixels on the canvas, not after its constructor returned: the
          // constructor schedules work, and a mark taken there reports a draw that has not
          // happened yet.
          draw: [
            () => {
              this.lastBuildMs = performance.now() - started;
              performance.mark(Chart.DrawnMark, {
                detail: {
                  points: data.x.length,
                  series: data.series.length,
                  buildMs: this.lastBuildMs,
                },
              });
            },
          ],
          ready: [() => this.placeMarker()],
          setScale: [() => this.placeMarker()],
          setSize: [() => this.placeMarker()],
        },
        // No `cursor` option on purpose. uPlot merges it shallowly, so passing a partial
        // `drag` replaces the whole default object - and losing `dist: 0` alone makes
        // `rawDX >= drag.dist` false for every drag, which silently disables zooming. The
        // defaults are already x-only.
        scales: {
          // Seconds from the start of the capture, not a point in time: without this uPlot reads
          // the axis as unix timestamps and labels it 1/1/1970.
          x: { time: false },
          y: { range: this.zeroBased() ? (_, min, max) => [Math.min(0, min), max] : undefined },
        },
        axes: [
          { ...axis, label: data.xLabel, labelSize: data.xLabel ? 18 : 0 },
          { ...axis, label: data.yLabel, labelSize: data.yLabel ? 22 : 0, size: 48 },
        ],
        series: [
          {},
          ...data.series.map((series, index) => ({
            label: series.label,
            stroke: colors.series[index % colors.series.length],
            width: 1.2,
            points: { show: false },
          })),
        ],
      },
      [toTyped(data.x), ...data.series.map((series) => toTyped(series.values))] as uPlot.AlignedData,
      element,
    );

  }

  /**
   * Puts the marker over the point it names.
   *
   * In the DOM rather than on the canvas: it is a label with text in it, and text drawn on canvas
   * is text a screen reader cannot reach and a user cannot select.
   */
  private placeMarker(): void {
    const marker = this.marker();

    if (!this.plot || !marker) {
      this.markerAt.set(null);

      return;
    }

    const plot = this.plot;
    const { x, y } = plot.scales;

    // After a zoom the point can lie outside the visible range, and a position computed for it
    // then lands anywhere on the page.
    if (!within(marker.x, x.min, x.max) || !within(marker.y, y.min, y.max)) {
      this.markerAt.set(null);

      return;
    }

    // valToPos answers relative to the plotting area; the overlay sits exactly on it, so its
    // offset is what places that area inside the component, whatever the device pixel ratio.
    const offsetX = plot.valToPos(marker.x, 'x');
    const left = offsetX + plot.over.offsetLeft;
    const top = plot.valToPos(marker.y, 'y') + plot.over.offsetTop;

    // Beside the peak, like the mockup - flipped to the left where the label would run past the
    // plot's right edge.
    const flip = offsetX + MarkerLabelRoom > plot.over.clientWidth;

    this.markerAt.set({ left, top, label: marker.label, flip });
  }
}

/** Width reserved right of a marker for its label, in CSS pixels; room for "1234 ms spike". */
const MarkerLabelRoom = 96;

function within(value: number, min: number | undefined, max: number | undefined): boolean {
  return min !== undefined && max !== undefined && value >= min && value <= max;
}

function toTyped(values: Float64Array | readonly (number | null)[]): Float64Array | (number | null)[] {
  return values instanceof Float64Array ? values : [...values];
}
