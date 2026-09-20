import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AppSettingsDto, AppSettingsPatch, ImportResultDto, ImportSourcesResponse } from './contracts';

/** The settings and import endpoints. */
@Injectable({ providedIn: 'root' })
export class SettingsClient {
  private readonly http = inject(HttpClient);

  get(): Observable<AppSettingsDto> {
    return this.http.get<AppSettingsDto>('/api/settings');
  }

  patch(patch: AppSettingsPatch): Observable<AppSettingsDto> {
    return this.http.patch<AppSettingsDto>('/api/settings', patch);
  }

  importSources(): Observable<ImportSourcesResponse> {
    return this.http.get<ImportSourcesResponse>('/api/records/import/sources');
  }

  import(path: string, recursive = true): Observable<ImportResultDto> {
    return this.http.post<ImportResultDto>('/api/records/import', { path, recursive });
  }
}
