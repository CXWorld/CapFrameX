import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** The title row every workspace starts with. */
@Component({
  selector: 'cx-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cx-page-header' },
  template: `
    <div class="titles">
      <h1>{{ heading() }}</h1>
      @if (subheading()) {
        <p>{{ subheading() }}</p>
      }
    </div>
    <ng-content />
  `,
  styles: `
    .cx-page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--cx-space-5);
    }

    h1 {
      font-size: var(--cx-text-17);
      font-weight: var(--cx-weight-medium);
    }

    p {
      margin-top: 2px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-12);
    }
  `,
})
export class PageHeader {
  readonly heading = input.required<string>();
  readonly subheading = input<string>();
}
