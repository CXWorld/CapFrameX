import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { RUNTIME_CONFIG } from '../../core/config/runtime-config';
import { AnalysisStore } from './analysis-store';
import { RecordLibraryStore } from './record-library-store';

/** jsdom has no EventSource, and the library store opens the event stream when it is created. */
class FakeEventSource {
  close(): void {
    // Nothing to close.
  }
}

describe('AnalysisStore', () => {
  let store: AnalysisStore;
  let library: RecordLibraryStore;
  let http: HttpTestingController;

  beforeEach(() => {
    (globalThis as { EventSource?: unknown }).EventSource = FakeEventSource;

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RUNTIME_CONFIG, useValue: { apiBaseUrl: 'http://127.0.0.1:17337', token: 't' } },
      ],
    });

    store = TestBed.inject(AnalysisStore);
    library = TestBed.inject(RecordLibraryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    // The library asks for the list on its own; this is not what these tests are about.
    http.match((request) => request.url === '/api/records').forEach((request) => request.flush({ records: [], total: 0 }));
    http.verify();
  });

  it('asks for nothing while nothing is selected', async () => {
    await TestBed.tick();

    http.expectNone((request) => request.url.startsWith('/api/records/'));
    expect(store.detail()).toBeNull();
  });

  it('reads the record and its numbers as soon as one is selected', async () => {
    library.select('abc');
    await TestBed.tick();

    http.expectOne('/api/records/abc').flush({ chips: [{ key: 'gpu', label: 'RTX 5090' }] });
    http.expectOne('/api/records/abc/analysis').flush({ metrics: [{ key: 'average', label: 'Avg', value: 113, unit: 'fps' }] });
    await TestBed.tick();

    expect(store.chips()).toEqual([{ key: 'gpu', label: 'RTX 5090' }]);
    expect(store.metrics()[0].value).toBe(113);
    expect(store.loading()).toBe(false);
  });

  it('follows the selection to another record', async () => {
    library.select('first');
    await TestBed.tick();
    http.expectOne('/api/records/first').flush({ chips: [] });
    http.expectOne('/api/records/first/analysis').flush({ metrics: [] });
    await TestBed.tick();

    library.select('second');
    await TestBed.tick();

    http.expectOne('/api/records/second').flush({ chips: [] });
    http.expectOne('/api/records/second/analysis').flush({ metrics: [] });
  });

  it('holds nothing when the record cannot be read, rather than throwing', async () => {
    // Reading a resource's value while it is in an error state throws; the view asks for these
    // on every change detection.
    library.select('gone');
    await TestBed.tick();

    http.expectOne('/api/records/gone').flush('missing', { status: 409, statusText: 'Conflict' });
    http.expectOne('/api/records/gone/analysis').flush('missing', { status: 409, statusText: 'Conflict' });
    await TestBed.tick();

    expect(store.detail()).toBeNull();
    expect(store.metrics()).toEqual([]);
    expect(store.pacing()).toBeNull();
  });
});
