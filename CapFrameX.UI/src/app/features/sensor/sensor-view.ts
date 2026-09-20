import { ChangeDetectionStrategy, Component } from '@angular/core';

/** The sensor view. Placeholder until its work package lands. */
@Component({
  selector: 'cx-sensor-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section>
      <h1>Sensor</h1>
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
export class SensorView {}
