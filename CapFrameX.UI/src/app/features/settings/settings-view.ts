import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';

import { Card } from '../../ui/card/card';
import { PageHeader } from '../../ui/page-header/page-header';
import { SettingsStore } from './settings-store';

/** The themes the frontend offers, as the service names them. */
const THEMES = ['system', 'light', 'dark'] as const;

/** The analysis settings this view edits as numbers. */
interface AnalysisNumbers {
  stutteringFactor: number;
  stutteringThreshold: number;
  fpsValuesRoundingDigits: number;
}

/**
 * Settings, and the one-off import.
 *
 * Every value here is the service's: a change is sent, validated as a whole and answered with what
 * the settings now are. Nothing is kept locally and reconciled afterwards, so the form cannot show
 * something the service rejected.
 */
@Component({
  selector: 'cx-settings-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Card, PageHeader],
  template: `
    <section class="workspace">
      <cx-page-header heading="Settings" subheading="Analysis options, capture folder, appearance" />

      @if (store.settings(); as settings) {
        <cx-card heading="Import">
          <p class="lead">
            Captures are read into the database, so a record opens whether or not the file it came
            from is still there.
          </p>

          @if (store.sources().length > 0) {
            <ul class="sources">
              @for (source of store.sources(); track source.path) {
                <li>
                  <div>
                    <p class="path">{{ source.path }}</p>
                    <p class="origin">{{ source.origin }} · {{ source.captureCount }} captures</p>
                  </div>
                  <button type="button" [disabled]="store.importing()" (click)="importFrom(source.path)">
                    Import
                  </button>
                </li>
              }
            </ul>
          } @else {
            <p class="lead">Nothing to suggest. Name a folder below.</p>
          }

          <div class="row">
            <input
              [value]="folder()"
              (input)="folder.set($any($event.target).value)"
              placeholder="Folder holding capture files"
              aria-label="Folder to import from"
            />
            <button type="button" [disabled]="store.importing() || !folder()" (click)="importFrom(folder())">
              {{ store.importing() ? 'Importing…' : 'Import folder' }}
            </button>
          </div>

          @if (store.lastImport(); as result) {
            <p class="result">
              {{ result.imported }} imported, {{ result.alreadyKnown }} already known,
              {{ result.failed }} unreadable.
            </p>
          }
        </cx-card>

        <cx-card heading="Capture folder">
          <p class="lead">Where the service watches for new captures.</p>
          <div class="row">
            <input
              [value]="settings.paths.captureDirectory"
              (change)="saveDirectory($any($event.target).value)"
              aria-label="Capture folder"
            />
          </div>
        </cx-card>

        <cx-card heading="Analysis">
          <div class="fields">
            <label>
              <span>Stuttering factor</span>
              <input
                type="number"
                step="0.1"
                min="1.1"
                [value]="settings.analysis.stutteringFactor"
                (change)="saveNumber('stutteringFactor', $any($event.target).value)"
              />
            </label>
            <label>
              <span>Low FPS threshold</span>
              <input
                type="number"
                step="1"
                min="1"
                [value]="settings.analysis.stutteringThreshold"
                (change)="saveNumber('stutteringThreshold', $any($event.target).value)"
              />
            </label>
            <label>
              <span>Decimal places</span>
              <input
                type="number"
                step="1"
                min="0"
                max="15"
                [value]="settings.analysis.fpsValuesRoundingDigits"
                (change)="saveNumber('fpsValuesRoundingDigits', $any($event.target).value)"
              />
            </label>
          </div>
          <p class="lead">
            Changing the decimal places re-reads every record, so the list and the open record
            agree.
          </p>
        </cx-card>

        <cx-card heading="Appearance">
          <div class="row">
            @for (theme of themes; track theme) {
              <button
                type="button"
                [class.active]="theme === settings.appearance.theme"
                (click)="saveTheme(theme)"
              >
                {{ theme }}
              </button>
            }
          </div>
        </cx-card>
      } @else if (store.loading()) {
        <p class="empty">Loading…</p>
      } @else {
        <p class="empty">The service did not answer.</p>
      }
    </section>
  `,
  styles: `
    :host {
      display: flex;
      flex: 1;
      min-width: 0;
    }

    .workspace {
      display: flex;
      flex: 1;
      flex-direction: column;
      gap: var(--cx-space-4);
      max-width: 760px;
      padding: var(--cx-space-7);
      overflow-y: auto;
    }

    .lead {
      margin-bottom: var(--cx-space-3);
      color: var(--cx-text-muted);
      font-size: var(--cx-text-12);
    }

    .sources {
      display: flex;
      flex-direction: column;
      gap: var(--cx-space-2);
      margin: 0 0 var(--cx-space-4);
      padding: 0;
      list-style: none;
    }

    .sources li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--cx-space-4);
      padding: var(--cx-space-3);
      border: 0.5px solid var(--cx-line);
      border-radius: var(--cx-radius);
      background: var(--cx-bg);
    }

    .path {
      font-size: var(--cx-text-12);
      word-break: break-all;
    }

    .origin {
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    .row {
      display: flex;
      gap: var(--cx-space-3);
    }

    .row input {
      flex: 1;
    }

    .fields {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: var(--cx-space-3);
      margin-bottom: var(--cx-space-4);
    }

    label {
      display: flex;
      flex-direction: column;
      gap: 4px;
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    button {
      height: var(--cx-control-height);
      padding: 0 var(--cx-space-5);
      border: 0.5px solid var(--cx-line);
      border-radius: var(--cx-radius);
      background: var(--cx-bg);
      color: var(--cx-text);
      font-size: var(--cx-text-12);
      text-transform: capitalize;
      cursor: pointer;
    }

    button.active {
      border-color: var(--cx-line-accent);
      background: var(--cx-bg-accent);
      color: var(--cx-text-accent);
    }

    button:disabled {
      color: var(--cx-text-faint);
      cursor: default;
    }

    .result {
      color: var(--cx-text-muted);
      font-size: var(--cx-text-11);
    }

    .empty {
      margin: auto;
      color: var(--cx-text-faint);
    }
  `,
})
export class SettingsView {
  protected readonly store = inject(SettingsStore);
  protected readonly themes = THEMES;
  protected readonly folder = signal('');

  protected importFrom(path: string): void {
    void this.store.import(path);
  }

  protected saveTheme(theme: string): void {
    void this.store.save({ appearance: { theme } });
  }

  protected saveDirectory(captureDirectory: string): void {
    void this.store.save({ paths: { captureDirectory } });
  }

  protected saveNumber(field: keyof AnalysisNumbers, value: string): void {
    const parsed = Number(value);

    if (Number.isFinite(parsed)) {
      void this.store.save({ analysis: { [field]: parsed } });
    }
  }
}
