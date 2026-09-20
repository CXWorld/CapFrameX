import { ChangeDetectionStrategy, Component, ElementRef, input, model, viewChild } from '@angular/core';

import { Icon } from '../icon/icon';

/**
 * The search box above a context list.
 *
 * Two-way by signal, and the debounce lives in the store rather than here: what counts as "the
 * user stopped typing" depends on what the search costs, and that is the store's business.
 */
@Component({
  selector: 'cx-search-field',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  host: { class: 'cx-search-field' },
  template: `
    <cx-icon name="search" [size]="14" />
    <input
      #field
      type="search"
      [attr.placeholder]="placeholder()"
      [attr.aria-label]="placeholder()"
      [value]="value()"
      (input)="value.set($any($event.target).value)"
    />
  `,
  styles: `
    .cx-search-field {
      position: relative;
      display: block;
    }

    cx-icon {
      position: absolute;
      top: 50%;
      left: 9px;
      color: var(--cx-text-faint);
      pointer-events: none;
      transform: translateY(-50%);
    }

    input {
      width: 100%;
      height: 30px;
      padding-left: 28px;
      font-size: var(--cx-text-12);
    }
  `,
})
export class SearchField {
  readonly value = model('');
  readonly placeholder = input('Search');

  private readonly field = viewChild.required<ElementRef<HTMLInputElement>>('field');

  /** Puts the cursor in the box; the shell binds this to the '/' key. */
  focus(): void {
    this.field().nativeElement.focus();
  }
}
