import { httpResource } from '@angular/common/http';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

import { BridgeEvents } from '../../core/bridge/bridge-events';
import { RecordSummaryDto, RecordsListResponse } from '../../data-access/contracts';

/** How long the list waits after a keystroke before asking the service. */
const SEARCH_DEBOUNCE_MS = 200;

/** How many records one page holds. */
const PAGE_SIZE = 200;

/**
 * The record library: what is in it, what is selected, and what the user narrowed it to.
 *
 * A signal store rather than a state library, per the architecture plan. The list itself is an
 * `httpResource`, which is eager and re-requests when the search changes - a hand-rolled pipeline
 * over `toObservable` has to be pushed by an effect, and an effect that never gets a tick leaves
 * the list loading forever. This has no such dependency.
 *
 * It reloads on `records.changed`, so importing or deleting a record - in this window or another
 * one - shows up without anybody pressing refresh.
 */
@Injectable({ providedIn: 'root' })
export class RecordLibraryStore {
  private readonly bridge = inject(BridgeEvents);

  /** What the user typed into the search box. */
  readonly search = signal('');

  /** The search as the service sees it: only after the typing stops. */
  private readonly settledSearch = signal('');

  /** Which record is open, by id. */
  readonly selectedId = signal<string | null>(null);

  private readonly page = httpResource<RecordsListResponse>(() => ({
    url: '/api/records',
    params: { search: this.settledSearch(), take: PAGE_SIZE },
  }));

  /** The records the list shows. */
  readonly records = computed<readonly RecordSummaryDto[]>(() => this.page.value()?.records ?? []);

  /** How many match, across all pages. */
  readonly total = computed(() => this.page.value()?.total ?? 0);

  /** Whether an answer is still on its way. */
  readonly loading = this.page.isLoading;

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

    // Typing narrows the list, but not on every keystroke: each one would cancel the request the
    // one before it started.
    effect((onCleanup) => {
      const typed = this.search();
      const handle = setTimeout(() => this.settledSearch.set(typed), SEARCH_DEBOUNCE_MS);

      onCleanup(() => clearTimeout(handle));
    });

    // Opening the view with nothing selected would show an empty workspace beside a full list.
    effect(() => {
      const records = this.records();

      untracked(() => {
        if (records.length > 0 && !records.some((record) => record.id === this.selectedId())) {
          this.selectedId.set(records[0].id);
        }
      });
    });
  }

  /** Asks the service again. */
  refresh(): void {
    this.page.reload();
  }

  /** Opens one record. */
  select(id: string): void {
    this.selectedId.set(id);
  }
}
