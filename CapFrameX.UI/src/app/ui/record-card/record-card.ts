import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { RecordSummaryDto } from '../../data-access/contracts';
import { Sparkline } from '../sparkline/sparkline';

/**
 * One capture in the library list.
 *
 * The sparkline is only drawn for the selected record. A column of them turns the list into a wall
 * of ink that says nothing at a glance, and the mockup shows exactly one.
 */
@Component({
  selector: 'cx-record-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Sparkline],
  host: {
    class: 'cx-record-card',
    '[class.selected]': 'selected()',
    '[attr.aria-current]': 'selected() ? "true" : null',
    role: 'option',
    '[attr.aria-selected]': 'selected()',
  },
  template: `
    <p class="title">{{ record().name }}</p>
    <p class="meta">{{ meta() }}</p>

    @if (selected() && record().sparkline.length > 1) {
      <cx-sparkline class="spark" [points]="record().sparkline" />
    }
  `,
  styles: `
    :host {
      display: block;
      padding: 9px var(--cx-space-4);
      border: 0.5px solid var(--cx-line);
      border-radius: var(--cx-radius);
      background: var(--cx-bg);
      cursor: pointer;
    }

    :host(:hover) {
      border-color: var(--cx-line-strong);
    }

    :host(.selected) {
      border-color: var(--cx-line-accent);
      background: var(--cx-bg-accent);
    }

    .title {
      overflow: hidden;
      font-size: var(--cx-text-12);
      font-weight: var(--cx-weight-medium);
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    :host(.selected) .title {
      color: var(--cx-text-accent);
    }

    .meta {
      margin-top: 1px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    :host(.selected) .meta {
      color: var(--cx-text-accent);
      opacity: 0.75;
    }

    .spark {
      height: 20px;
      margin-top: 7px;
    }
  `,
})
export class RecordCard {
  readonly record = input.required<RecordSummaryDto>();
  readonly selected = input(false);

  protected readonly meta = computed(() => {
    const record = this.record();
    const when = new Date(record.createdAt);
    const parts = [formatDay(when), formatTime(when)];

    if (record.durationSeconds > 0) {
      parts.push(`${Math.round(record.durationSeconds)} s`);
    }

    return parts.join(' · ');
  });
}

function formatDay(when: Date): string {
  const today = new Date();
  const sameDay =
    when.getFullYear() === today.getFullYear() &&
    when.getMonth() === today.getMonth() &&
    when.getDate() === today.getDate();

  // "Today" is what the mockup shows, and it is what a user scanning a list of this morning's runs
  // actually reads for.
  return sameDay ? 'Today' : when.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

function formatTime(when: Date): string {
  return when.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}
