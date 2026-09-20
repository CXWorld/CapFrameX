import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The overlay view. Placeholder until its work package lands. */
@Component({
  selector: 'cx-overlay-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <h1>Overlay</h1>
      <p>This view is not built yet.</p>
    </section>
  `,
  styles: `
    section {
      padding: var(--cx-space-7);
      color: var(--cx-text-muted);
    }

    h1 {
      font-size: var(--cx-text-17);
      color: var(--cx-text);
    }
  `,
})
export class OverlayView {}
