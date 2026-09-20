import { httpResource } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';

import { AnalysisDto, RecordDetailDto } from '../../data-access/contracts';
import { RecordLibraryStore } from './record-library-store';

/**
 * The open record: what it is, and what the numbers say about it.
 *
 * Two resources rather than one request pipeline. Both are eager and both re-run when the
 * selection changes, so neither depends on an effect getting a tick - which is what left the
 * record list loading forever before. With nothing selected the request function returns
 * `undefined`, which is how a resource says "do not ask".
 *
 * The tiles and the L-shape come from the service's configured defaults, so a user's chosen
 * metrics arrive without this having to know what they are.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisStore {
  private readonly library = inject(RecordLibraryStore);

  private readonly detailResource = httpResource<RecordDetailDto>(() => {
    const id = this.library.selectedId();

    return id === null ? undefined : `/api/records/${id}`;
  });

  private readonly analysisResource = httpResource<AnalysisDto>(() => {
    const id = this.library.selectedId();

    return id === null ? undefined : `/api/records/${id}/analysis`;
  });

  /** Whether an answer is still on its way. */
  readonly loading = computed(() => this.detailResource.isLoading() || this.analysisResource.isLoading());

  /** What the capture recorded about itself. */
  readonly detail = computed(() => (this.detailResource.hasValue() ? this.detailResource.value() : null));

  /** The numbers. */
  readonly analysis = computed(() =>
    this.analysisResource.hasValue() ? this.analysisResource.value() : null,
  );

  /** The pills above the chart. */
  readonly chips = computed(() => this.detail()?.chips ?? []);

  /** The tiles, in the order the user configured them. */
  readonly metrics = computed(() => this.analysis()?.metrics ?? []);

  /** How evenly the frames arrived. */
  readonly pacing = computed(() => this.analysis()?.framePacing ?? null);

  /** Latency, where the capture carries it. */
  readonly latency = computed(() => this.analysis()?.pcLatency ?? null);

  /** Reads both again, after the record was edited. */
  refresh(): void {
    this.detailResource.reload();
    this.analysisResource.reload();
  }
}
