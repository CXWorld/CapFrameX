import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { Icon } from '../../ui/icon/icon';
import { RAIL_ITEMS } from './rail-items';

/**
 * The navigation rail.
 *
 * Icons only, so the labels live in `aria-label` and `title` rather than on screen - which means
 * they have to be there, not merely nice to have. The list is ordered once in `RAIL_ITEMS` and
 * rendered in two groups, because Settings belongs at the bottom and is not a view like the others.
 */
@Component({
  selector: 'cx-rail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, RouterLink, RouterLinkActive],
  host: { class: 'cx-rail' },
  template: `
    <div class="logo" aria-hidden="true">CX</div>

    <nav aria-label="Views">
      @for (item of leading; track item.path) {
        <a
          [routerLink]="item.path"
          routerLinkActive="active"
          [attr.aria-label]="item.label"
          [title]="item.label"
        >
          <cx-icon [name]="item.icon" />
        </a>
      }
    </nav>

    <div class="spacer"></div>

    <nav aria-label="Application">
      @for (item of trailing; track item.path) {
        <a
          [routerLink]="item.path"
          routerLinkActive="active"
          [attr.aria-label]="item.label"
          [title]="item.label"
        >
          <cx-icon [name]="item.icon" />
        </a>
      }
    </nav>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--cx-space-1);
      width: var(--cx-rail-width);
      padding: var(--cx-space-5) 0;
      background: var(--cx-bg-subtle);
      border-right: 0.5px solid var(--cx-line);
    }

    nav {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--cx-space-1);
    }

    .logo {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      margin-bottom: var(--cx-space-4);
      border-radius: var(--cx-radius);
      background: var(--cx-text);
      color: var(--cx-bg);
      font-size: var(--cx-text-11);
      font-weight: var(--cx-weight-medium);
      letter-spacing: 0.04em;
    }

    a {
      display: flex;
      align-items: center;
      justify-content: center;
      width: var(--cx-rail-button);
      height: var(--cx-rail-button);
      border-radius: var(--cx-radius);
      color: var(--cx-text-muted);
    }

    a:hover {
      background: var(--cx-line);
      color: var(--cx-text);
    }

    a.active {
      background: var(--cx-bg-accent);
      color: var(--cx-text-accent);
    }

    .spacer {
      flex: 1;
    }
  `,
})
export class Rail {
  protected readonly leading = RAIL_ITEMS.filter((item) => !item.trailing);
  protected readonly trailing = RAIL_ITEMS.filter((item) => item.trailing);
}
