import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { RUNTIME_CONFIG } from '../../core/config/runtime-config';
import { RecordLibraryStore } from './record-library-store';

/** jsdom has no EventSource, and the store opens the event stream when it is created. */
class FakeEventSource {
  onopen: (() => void) | null = null;
  onmessage: ((event: MessageEvent<string>) => void) | null = null;
  onerror: (() => void) | null = null;

  close(): void {
    // Nothing to close.
  }
}

describe('RecordLibraryStore', () => {
  let store: RecordLibraryStore;
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

    store = TestBed.inject(RecordLibraryStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks the service for the records without being told to', async () => {
    // The list is the first thing the view shows, and nothing else triggers it.
    await TestBed.tick();
    await new Promise((resolve) => setTimeout(resolve, 400));
    await TestBed.tick();

    const request = http.expectOne((candidate) => candidate.url === '/api/records');
    request.flush({ records: [{ id: 'a', name: 'one', sparkline: [] }], total: 1 });

    // Twice: the first settles the resource, the second runs the effect that reads it.
    await TestBed.tick();
    await TestBed.tick();

    expect(store.loading()).toBe(false);
    expect(store.total()).toBe(1);

    // Nothing selected would mean an empty workspace beside a full list.
    expect(store.selectedId()).toBe('a');
  });
});
