import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, debounceTime, distinctUntilChanged, filter, of, startWith, switchMap } from 'rxjs';

import { BridgeEvents } from '../../core/bridge/bridge-events';
import { RecordSummaryDto } from '../../data-access/contracts';
import { RecordsClient } from '../../data-access/records-client';

/** How long the list waits after a keystroke before asking the service. */
const SEARCH_DEBOUNCE_MS = 200;

/**
 * The record library: what is in it, what is selected, and what the user narrowed it to.
 *
 * A signal store rather than a state library, per the architecture plan. It reloads on
 * `records.changed`, so importing or deleting a record - in this window or another one - shows up
 * without anybody pressing refresh.
 */
@Injectable({ providedIn: 'root' })
export class RecordLibraryStore {
  private readonly client = inject(RecordsClient);
  private readonly bridge = inject(BridgeEvents);

  private readonly reload = signal(0);

  /** What the user typed into the search box. */
  readonly search = signal('');

  /** Which record is open, by id. */
  readonly selectedId = signal<string | null>(null);

  private readonly page = toSignal(
    toObservable(computed(() => ({ search: this.search(), reload: this.reload() }))).pipe(
      debounceTime(SEARCH_DEBOUNCE_MS),
      distinctUntilChanged((a, b) => a.search === b.search && a.reload === b.reload),
      switchMap(({ search }) =>
        this.client.list({ search, take: 200 }).pipe(
          // The interceptor already told the user; the list simply stays empty rather than
          // tearing the view down.
          catchError(() => of({ records: [], total: 0 })),
        ),
      ),
      startWith(null),
      takeUntilDestroyed(),
    ),
    { initialValue: null },
  );

  /** The records the list shows. */
  readonly records = computed<readonly RecordSummaryDto[]>(() => this.page()?.records ?? []);

  /** How many match, across all pages. */
  readonly total = computed(() => this.page()?.total ?? 0);

  /** Whether the first answer is still on its way. */
  readonly loading = computed(() => this.page() === null);

  /** The open record, or nothing. */
  readonly selected = computed(() => {
    const id = this.selectedId();

    return id === null ? null : (this.records().find((record) => record.id === id) ?? null);
  });

  constructor() {
    this.bridge.connect();

    this.bridge.events
      .pipe(
        filter((event) => event.type === 'records.changed'),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.refresh());

    // Opening the view with nothing selected would show an empty workspace beside a full list.
    effect(() => {
      const records = this.records();

      if (records.length > 0 && !records.some((record) => record.id === this.selectedId())) {
        this.selectedId.set(records[0].id);
      }
    });
  }

  /** Asks the service again. */
  refresh(): void {
    this.reload.update((value) => value + 1);
  }

  /** Opens one record. */
  select(id: string): void {
    this.selectedId.set(id);
  }
}
