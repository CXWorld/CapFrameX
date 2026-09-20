import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** One tab. */
export interface TabItem {
  readonly id: string;
  readonly label: string;
}

/**
 * Underline tabs with a roving tabindex.
 *
 * One stop in the tab order for the whole set and the arrow keys inside it, which is what the
 * pattern asks for: tabbing through four tabs to reach the chart is four presses nobody wants.
 */
@Component({
  selector: 'cx-tabs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="tablist" [attr.aria-label]="label()">
      @for (tab of tabs(); track tab.id) {
        <button
          type="button"
          role="tab"
          [class.active]="tab.id === selected()"
          [attr.aria-selected]="tab.id === selected()"
          [attr.tabindex]="tab.id === selected() ? 0 : -1"
          (click)="selectedChange.emit(tab.id)"
          (keydown.arrowright)="move(1)"
          (keydown.arrowleft)="move(-1)"
        >
          {{ tab.label }}
        </button>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    [role='tablist'] {
      display: flex;
      gap: 2px;
      border-bottom: 0.5px solid var(--cx-line);
    }

    button {
      margin-bottom: -1px;
      padding: 7px var(--cx-space-5);
      border: 0;
      border-bottom: 2px solid transparent;
      background: transparent;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-12);
      cursor: pointer;
    }

    button.active {
      border-bottom-color: var(--cx-text);
      color: var(--cx-text);
    }
  `,
})
export class Tabs {
  readonly tabs = input.required<readonly TabItem[]>();
  readonly selected = input.required<string>();
  readonly label = input('Views');

  readonly selectedChange = output<string>();

  protected move(step: number): void {
    const tabs = this.tabs();
    const current = tabs.findIndex((tab) => tab.id === this.selected());

    if (current < 0) {
      return;
    }

    // Wraps, so the arrow keys never dead-end.
    const next = (current + step + tabs.length) % tabs.length;
    this.selectedChange.emit(tabs[next].id);
  }
}
