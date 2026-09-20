import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * One icon out of the sprite.
 *
 * A sprite rather than one request per icon, and `currentColor` rather than a fill, so an icon
 * follows the text it sits next to through every theme change without anybody wiring it up.
 */
@Component({
  selector: 'cx-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'cx-icon',
    '[style.--cx-icon-size.px]': 'size()',
  },
  template: `
    <svg aria-hidden="true" focusable="false" [attr.width]="size()" [attr.height]="size()">
      <use [attr.href]="'icons.svg#' + name()" />
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      color: inherit;
    }

    svg {
      display: block;
      stroke: currentColor;
      fill: none;
    }
  `,
})
export class Icon {
  /** Which icon, by the name it has in the sprite. */
  readonly name = input.required<string>();

  /** Its edge length in pixels. */
  readonly size = input(18);
}
