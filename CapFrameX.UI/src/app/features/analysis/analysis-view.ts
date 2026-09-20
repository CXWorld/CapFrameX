import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';

import { Card } from '../../ui/card/card';
import { Chip } from '../../ui/chip/chip';
import { PageHeader } from '../../ui/page-header/page-header';
import { StatTile } from '../../ui/stat-tile/stat-tile';
import { Tabs } from '../../ui/tabs/tabs';
import { Chart } from '../../visualization/chart';
import { AnalysisStore } from './analysis-store';
import { ANALYSIS_TABS, AnalysisTab, SeriesStore } from './series-store';
import { RecordLibrary } from './record-library';
import { RecordLibraryStore } from './record-library-store';

/**
 * The analysis workspace: what one capture says about itself.
 *
 * The chart is WP-F6 and is not here yet; everything around it - the tiles, the chips, the frame
 * pacing and the latency card - reads the service's own analysis, so the numbers are the ones the
 * desktop application computes.
 */
@Component({
  selector: 'cx-analysis-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Card, Chart, Chip, PageHeader, RecordLibrary, StatTile, Tabs],
  host: { class: 'cx-analysis-view' },
  template: `
    <cx-record-library />

    <section class="workspace">
      @if (record(); as open) {
        <cx-page-header [heading]="open.summary.name" [subheading]="subheading()" />

        @if (store.chips().length > 0) {
          <div class="chips">
            @for (chip of store.chips(); track chip.key) {
              <cx-chip>{{ chip.label }}</cx-chip>
            }
          </div>
        }

        <cx-tabs
          [tabs]="tabs"
          [selected]="series.tab()"
          label="Analysis charts"
          (selectedChange)="series.show($any($event))"
        />

        <div class="chart-slot">
          @if (series.chart(); as chart) {
            <cx-chart [data]="chart" [marker]="series.marker()" [zeroBased]="zeroBased()" />
          } @else if (series.loading()) {
            <p class="placeholder">Loading…</p>
          } @else {
            <p class="placeholder">This capture has nothing to draw here.</p>
          }
        </div>

        <div class="tiles">
          @for (metric of store.metrics(); track metric.key) {
            <cx-stat-tile [label]="metric.label" [value]="metric.value" [unit]="metric.unit" />
          }
        </div>

        <div class="cards">
          <cx-card heading="Frame pacing">
            @if (store.pacing(); as pacing) {
              <p class="big">
                {{ pacing.smoothPercent.toFixed(1) }}<span class="unit">% smooth</span>
              </p>
              <p class="sub">
                {{ pacing.stutterPercent.toFixed(1) }}% stutter ·
                {{ pacing.lowFpsPercent.toFixed(1) }}% low fps ·
                {{ pacing.spikeCount }} spike{{ pacing.spikeCount === 1 ? '' : 's' }}
              </p>
            } @else {
              <p class="sub">Not available.</p>
            }
          </cx-card>

          <cx-card heading="PC latency">
            @if (store.latency(); as latency) {
              <p class="big">{{ latency.averageMs.toFixed(1) }}<span class="unit">ms</span></p>
              <p class="sub">PresentMon PC latency, input to display</p>
            } @else {
              <p class="sub">This capture does not carry latency.</p>
            }
          </cx-card>
        </div>
      } @else if (store.loading()) {
        <p class="empty">Loading…</p>
      } @else {
        <p class="empty">Select a capture to analyse it.</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: flex;
      flex: 1;
      min-width: 0;
    }

    .workspace {
      display: flex;
      flex: 1;
      flex-direction: column;
      gap: var(--cx-space-6);
      min-width: 0;
      padding: var(--cx-space-7);
      overflow-y: auto;
    }

    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: var(--cx-space-2);
    }

    .chart-slot {
      display: flex;
      align-items: center;
      justify-content: center;
      height: 220px;
      color: var(--cx-text-faint);
      font-size: var(--cx-text-12);
    }

    cx-chart {
      width: 100%;
      height: 100%;
    }

    .tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
      gap: var(--cx-space-3);
    }

    .cards {
      display: grid;
      grid-template-columns: 1.3fr 1fr;
      gap: var(--cx-space-4);
    }

    .big {
      font-size: var(--cx-text-20);
      font-weight: var(--cx-weight-medium);
      line-height: 1.2;
    }

    .unit {
      margin-left: 3px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-12);
      font-weight: var(--cx-weight-normal);
    }

    .sub {
      margin-top: 3px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    .empty {
      margin: auto;
      color: var(--cx-text-faint);
    }
  `,
})
export class AnalysisView {
  protected readonly store = inject(AnalysisStore);
  protected readonly series = inject(SeriesStore);
  protected readonly tabs = ANALYSIS_TABS;

  private readonly library = inject(RecordLibraryStore);

  /**
   * Frame times and frame rate are read against zero; the L-shape and the distribution are not.
   * A percentile curve forced to start at zero wastes most of its height on empty space.
   */
  protected readonly zeroBased = computed<boolean>(() => {
    const tab: AnalysisTab = this.series.tab();

    return tab === 'frametimes' || tab === 'fps';
  });

  protected readonly record = computed(() => this.store.detail());

  protected readonly subheading = computed(() => {
    const summary = this.library.selected();

    if (!summary) {
      return '';
    }

    const when = new Date(summary.createdAt).toLocaleString();
    const runs = `${summary.runCount} run${summary.runCount === 1 ? '' : 's'}`;

    return `Analysis · captured ${when} · ${runs}`;
  });
}
