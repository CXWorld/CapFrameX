import { Injectable, computed, inject } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';

import { AnalysisDto, RecordDetailDto } from '../../data-access/contracts';
import { RecordsClient } from '../../data-access/records-client';
import { RecordLibraryStore } from './record-library-store';

/** What the workspace has for the open record. */
interface OpenRecord {
  readonly detail: RecordDetailDto | null;
  readonly analysis: AnalysisDto | null;
}

/**
 * The open record: what it is, and what the numbers say about it.
 *
 * Both requests go out together because the view needs both before it has anything to show, and
 * neither depends on the other. The tiles and the L-shape come from the service's configured
 * defaults, so a user's chosen metrics arrive without this having to know what they are.
 */
@Injectable({ providedIn: 'root' })
export class AnalysisStore {
  private readonly client = inject(RecordsClient);
  private readonly library = inject(RecordLibraryStore);

  private readonly open = toSignal(
    toObservable(this.library.selectedId).pipe(
      switchMap((id) => {
        if (id === null) {
          return of<OpenRecord>({ detail: null, analysis: null });
        }

        return combineLatest([
          this.client.detail(id).pipe(catchError(() => of(null))),
          this.client.analysis(id).pipe(catchError(() => of(null))),
        ]).pipe(
          map(([detail, analysis]) => ({ detail, analysis })),
          startWith<OpenRecord | null>(null),
        );
      }),
      takeUntilDestroyed(),
    ),
    { initialValue: null },
  );

  /** Whether an answer is still on its way. */
  readonly loading = computed(() => this.library.selectedId() !== null && this.open() === null);

  /** What the capture recorded about itself. */
  readonly detail = computed(() => this.open()?.detail ?? null);

  /** The numbers. */
  readonly analysis = computed(() => this.open()?.analysis ?? null);

  /** The pills above the chart. */
  readonly chips = computed(() => this.detail()?.chips ?? []);

  /** The tiles, in the order the user configured them. */
  readonly metrics = computed(() => this.analysis()?.metrics ?? []);

  /** How evenly the frames arrived. */
  readonly pacing = computed(() => this.analysis()?.framePacing ?? null);

  /** Latency, where the capture carries it. */
  readonly latency = computed(() => this.analysis()?.pcLatency ?? null);
}
