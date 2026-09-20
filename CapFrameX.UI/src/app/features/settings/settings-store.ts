import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Injectable, computed, effect, inject, signal, untracked } from '@angular/core';

import {
  AppSettingsDto,
  AppSettingsPatch,
  ImportResultDto,
  ImportSourcesResponse,
} from '../../data-access/contracts';
import { RecordLibraryStore } from '../analysis/record-library-store';
import { Theme, ThemePreference } from '../../core/theme/theme';

/**
 * The user's settings, and what importing does.
 *
 * The service owns them - it validates a patch as a whole and answers with what the settings now
 * are - so this keeps no copy of its own beyond what the last answer said. A change takes effect
 * in the service at once, which is why the analysis recomputes without anything here asking it to.
 */
@Injectable({ providedIn: 'root' })
export class SettingsStore {
  private readonly http = inject(HttpClient);
  private readonly theme = inject(Theme);
  private readonly library = inject(RecordLibraryStore);

  private readonly resource = httpResource<AppSettingsDto>(() => '/api/settings');
  private readonly sourcesResource = httpResource<ImportSourcesResponse>(() => '/api/records/import/sources');

  /** Whether a change is on its way to the service. */
  readonly saving = signal(false);

  /** Whether an import is running. */
  readonly importing = signal(false);

  /** What the last import did, until the next one. */
  readonly lastImport = signal<ImportResultDto | null>(null);

  /** The settings as the service last reported them. */
  readonly settings = computed(() => (this.resource.hasValue() ? this.resource.value() : null));

  /** Whether the first answer is still on its way. */
  readonly loading = this.resource.isLoading;

  /** Folders worth offering to import from. */
  readonly sources = computed(() =>
    this.sourcesResource.hasValue() ? this.sourcesResource.value().sources : [],
  );

  /** Whether the user has already been asked about the first import. */
  readonly importOffered = computed(() =>
    this.sourcesResource.hasValue() ? this.sourcesResource.value().offered : true,
  );

  constructor() {
    // The service remembers the theme so it follows the user between the windows; the stylesheet
    // has to be told what it says.
    effect(() => {
      const theme = this.settings()?.appearance.theme as ThemePreference | undefined;

      if (theme) {
        untracked(() => this.theme.prefer(theme));
      }
    });
  }

  /** Sends a patch and keeps what the service answers. */
  async save(patch: AppSettingsPatch): Promise<void> {
    this.saving.set(true);

    try {
      // What the service answers is what the settings now are: it validates the patch as a
      // whole, so a value it refused never reaches the form.
      this.resource.set(await firstValueFrom(this.http.patch<AppSettingsDto>('/api/settings', patch)));
    } finally {
      this.saving.set(false);
    }
  }

  /** Imports every capture in a folder. */
  async import(path: string): Promise<void> {
    this.importing.set(true);

    try {
      const result = await firstValueFrom(
        this.http.post<ImportResultDto>('/api/records/import', { path, recursive: true }),
      );

      this.lastImport.set(result);

      if (result.imported > 0) {
        // The service announces this on the event stream as well; asking now means the list is
        // right by the time the user switches back to it.
        this.library.refresh();
      }

      this.sourcesResource.reload();
    } finally {
      this.importing.set(false);
    }
  }
}
