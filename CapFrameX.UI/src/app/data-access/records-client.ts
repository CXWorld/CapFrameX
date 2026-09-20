import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AnalysisDto,
  RecordDetailDto,
  RecordEdit,
  RecordsListResponse,
  SeriesResponse,
} from './contracts';

/** What the record list can be narrowed and ordered by. */
export interface RecordQuery {
  readonly search?: string;
  readonly game?: string;
  readonly from?: string;
  readonly to?: string;
  readonly sort?: string;
  readonly skip?: number;
  readonly take?: number;
}

/** What one analysis asks for. */
export interface AnalysisQuery {
  readonly run?: number;
  readonly start?: number;
  readonly end?: number;
  readonly outliers?: string;
  readonly metrics?: readonly string[];
  readonly lshape?: string;
}

/**
 * The records endpoints.
 *
 * Paths only - the base URL and the token are the interceptor's business, which is what lets the
 * same client work under `ng serve`, in the desktop host and on either platform.
 */
@Injectable({ providedIn: 'root' })
export class RecordsClient {
  private readonly http = inject(HttpClient);

  list(query: RecordQuery = {}): Observable<RecordsListResponse> {
    return this.http.get<RecordsListResponse>('/api/records', { params: toParams(query) });
  }

  games(): Observable<string[]> {
    return this.http.get<string[]>('/api/records/games');
  }

  detail(id: string): Observable<RecordDetailDto> {
    return this.http.get<RecordDetailDto>(`/api/records/${id}`);
  }

  analysis(id: string, query: AnalysisQuery = {}): Observable<AnalysisDto> {
    return this.http.get<AnalysisDto>(`/api/records/${id}/analysis`, {
      params: toParams({ ...query, metrics: query.metrics?.join(',') }),
    });
  }

  series(id: string, kinds: readonly string[] = [], run?: number): Observable<SeriesResponse> {
    return this.http.get<SeriesResponse>(`/api/records/${id}/series`, {
      params: toParams({ kinds: kinds.join(','), run }),
    });
  }

  edit(id: string, patch: RecordEdit): Observable<RecordDetailDto> {
    return this.http.patch<RecordDetailDto>(`/api/records/${id}`, patch);
  }

  /** Moves the capture to the platform's trash and drops the record. */
  remove(id: string): Observable<void> {
    return this.http.delete<void>(`/api/records/${id}`);
  }
}

/**
 * Leaves out what was not asked for.
 *
 * An empty parameter is not the same as an absent one: `?search=` would narrow the list to records
 * whose name contains nothing in particular, and `?run=` is not a run.
 */
function toParams(query: Record<string, unknown>): HttpParams {
  let params = new HttpParams();

  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') {
      params = params.set(key, String(value));
    }
  }

  return params;
}
