import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';

import { ChartData } from '../../visualization/chart';
import { SeriesResponse } from '../../data-access/contracts';
import { AnalysisStore } from './analysis-store';
import { RecordLibraryStore } from './record-library-store';

/** Which curve the workspace is showing. */
export type AnalysisTab = 'frametimes' | 'fps' | 'lshape' | 'distribution';

/** The tabs, in the order the mockup shows them. */
export const ANALYSIS_TABS: readonly { readonly id: AnalysisTab; readonly label: string }[] = [
  { id: 'frametimes', label: 'Frame times' },
  { id: 'fps', label: 'FPS' },
  { id: 'lshape', label: 'L-shape' },
  { id: 'distribution', label: 'Distribution' },
];

/**
 * The curves behind the chart.
 *
 * Only the two that need the frame data are fetched, and only when one of them is showing: the
 * series of a ten-minute capture is a megabyte of numbers, and the L-shape and the distribution
 * already arrived with the analysis.
 */
@Injectable({ providedIn: 'root' })
export class SeriesStore {
  private readonly library = inject(RecordLibraryStore);
  private readonly analysis = inject(AnalysisStore);

  /** Which curve is showing. */
  readonly tab = signal<AnalysisTab>('frametimes');

  private readonly series = httpResource<SeriesResponse>(() => {
    const id = this.library.selectedId();
    const tab = this.tab();

    if (id === null || (tab !== 'frametimes' && tab !== 'fps')) {
      return undefined;
    }

    return { url: `/api/records/${id}/series`, params: { kinds: tab } };
  });

  /** Whether the curve is still on its way. */
  readonly loading = computed(() => this.series.isLoading() || this.analysis.loading());

  /** What to draw for the tab that is showing. */
  readonly chart = computed<ChartData | null>(() => {
    switch (this.tab()) {
      case 'frametimes':
        return this.fromSeries('frametimes', 'Frame time (ms)');
      case 'fps':
        return this.fromSeries('fps', 'FPS');
      case 'lshape':
        return this.fromPoints('lShape', 'Percentile', this.lShapeUnit());
      default:
        return this.fromPoints('distribution', 'Frame time (ms)', 'Share');
    }
  });

  /** The worst spike, to point at on the frame time curve. */
  readonly marker = computed(() => {
    const spike = this.analysis.pacing()?.worstSpike;

    return this.tab() === 'frametimes' && spike
      ? { x: spike.timeSeconds, y: spike.milliseconds, label: `${Math.round(spike.milliseconds)} ms spike` }
      : null;
  });

  /** Opens another curve. */
  show(tab: AnalysisTab): void {
    this.tab.set(tab);
  }

  private lShapeUnit(): string {
    // The service says which curve it drew; frame times run 90 to 99.95, frame rate 0.05 to 10.
    const metric = this.analysis.analysis()?.thresholds;

    return metric ? 'Frame time (ms)' : 'Value';
  }

  private fromSeries(kind: 'frametimes' | 'fps', label: string): ChartData | null {
    const response = this.series.hasValue() ? this.series.value() : null;
    const values = response?.[kind];

    if (!response || !values) {
      return null;
    }

    return {
      x: Float64Array.from(response.time),
      series: [{ label, values: Float64Array.from(values) }],
      xLabel: 'Time (s)',
      yLabel: label,
    };
  }

  private fromPoints(
    key: 'lShape' | 'distribution',
    xLabel: string,
    yLabel: string,
  ): ChartData | null {
    const points = this.analysis.analysis()?.[key];

    if (!points || points.length === 0) {
      return null;
    }

    return {
      x: Float64Array.from(points.map((point) => point.x)),
      series: [{ label: yLabel, values: Float64Array.from(points.map((point) => point.y)) }],
      xLabel,
      yLabel,
    };
  }
}
