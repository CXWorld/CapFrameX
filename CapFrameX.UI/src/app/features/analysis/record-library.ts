import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { RecordCard } from '../../ui/record-card/record-card';
import { SearchField } from '../../ui/search-field/search-field';
import { RecordLibraryStore } from './record-library-store';

/**
 * The context list of the analysis view: every capture the service knows.
 */
@Component({
  selector: 'cx-record-library',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RecordCard, SearchField],
  host: { class: 'cx-record-library' },
  template: `
    <cx-search-field [(value)]="store.search" placeholder="Search captures" />

    @if (store.loading()) {
      <p class="note">Loading…</p>
    } @else if (store.records().length === 0) {
      <p class="note">
        @if (store.search()) {
          Nothing matches “{{ store.search() }}”.
        } @else {
          No captures yet. Import some from the settings view.
        }
      </p>
    } @else {
      <p class="label">{{ store.total() }} capture{{ store.total() === 1 ? '' : 's' }}</p>

      <div class="records" role="listbox" aria-label="Captures">
        @for (record of store.records(); track record.id) {
          <cx-record-card
            [record]="record"
            [selected]="record.id === store.selectedId()"
            tabindex="0"
            (click)="store.select(record.id)"
            (keydown.enter)="store.select(record.id)"
            (keydown.space)="store.select(record.id)"
          />
        }
      </div>
    }
  `,
  styles: `
    .cx-record-library {
      display: flex;
      flex-direction: column;
      gap: var(--cx-space-3);
      width: var(--cx-list-width);
      padding: var(--cx-space-6) var(--cx-space-5);
      border-right: 0.5px solid var(--cx-line);
      overflow: hidden;
    }

    .label,
    .note {
      padding: var(--cx-space-2) 2px 0;
      color: var(--cx-text-faint);
      font-size: var(--cx-text-11);
      letter-spacing: 0.06em;
    }

    .note {
      letter-spacing: normal;
      line-height: 1.5;
    }

    .records {
      display: flex;
      flex-direction: column;
      gap: var(--cx-space-3);
      overflow-y: auto;
    }
  `,
})
export class RecordLibrary {
  protected readonly store = inject(RecordLibraryStore);
}
