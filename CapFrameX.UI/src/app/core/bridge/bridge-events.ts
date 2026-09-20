import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';

import { RUNTIME_CONFIG } from '../config/runtime-config';

/** One event as the service sends it. */
export interface BridgeEvent<T = unknown> {
  readonly type: string;
  readonly version: number;
  readonly sequence: number;
  readonly timestamp: string;
  readonly payload: T;
}

/** What the status bar reports about the stream. */
export type BridgeConnection = 'idle' | 'connecting' | 'open' | 'reconnecting';

/**
 * The service's event stream.
 *
 * Server-sent events rather than a socket: everything here goes one way, and an `EventSource`
 * reconnects and resumes by itself where a socket would need both written by hand.
 *
 * The parts that are written by hand are the two the browser does not do: the token, which cannot
 * go in a header because `EventSource` sends none, and the backoff, because the browser's own
 * retry is a fixed short interval that hammers a service which is simply not running yet.
 */
@Injectable({ providedIn: 'root' })
export class BridgeEvents {
  /** How long to wait before the first retry. */
  static readonly FirstDelayMs = 500;

  /** The longest it will ever wait between retries. */
  static readonly MaximumDelayMs = 15_000;

  private readonly config = inject(RUNTIME_CONFIG);
  private readonly stream = new Subject<BridgeEvent>();

  private source: EventSource | null = null;
  private retry: ReturnType<typeof setTimeout> | null = null;
  private delay = BridgeEvents.FirstDelayMs;
  private lastEventId = '';

  /** State of the connection, for the status bar. */
  readonly connection = signal<BridgeConnection>('idle');

  /** Every event the service sends. */
  readonly events = this.stream.asObservable();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.disconnect());
  }

  /** Opens the stream, or does nothing if it is already open. */
  connect(): void {
    if (this.source) {
      return;
    }

    this.connection.set(this.delay === BridgeEvents.FirstDelayMs ? 'connecting' : 'reconnecting');

    // The token goes in the query string because EventSource cannot set a header, and the guard
    // accepts it there for exactly this reason. Last-Event-ID likewise: the browser only sends it
    // on its own reconnects, not on ours.
    const url = new URL(this.config.apiBaseUrl + '/api/events');
    url.searchParams.set('access_token', this.config.token);

    if (this.lastEventId) {
      url.searchParams.set('lastEventId', this.lastEventId);
    }

    const source = new EventSource(url.toString());
    this.source = source;

    source.onopen = () => {
      // Only a connection that actually opened resets the backoff. Resetting on the attempt would
      // turn a service that accepts and immediately drops into a tight loop.
      this.delay = BridgeEvents.FirstDelayMs;
      this.connection.set('open');
    };

    source.onmessage = (event) => this.receive(event);
    source.onerror = () => this.reconnect();
  }

  /** Closes the stream and stops retrying. */
  disconnect(): void {
    this.source?.close();
    this.source = null;

    if (this.retry !== null) {
      clearTimeout(this.retry);
      this.retry = null;
    }

    this.connection.set('idle');
  }

  private receive(event: MessageEvent<string>): void {
    if (event.lastEventId) {
      this.lastEventId = event.lastEventId;
    }

    try {
      this.stream.next(JSON.parse(event.data) as BridgeEvent);
    } catch {
      // A frame that will not parse is one event lost, not a reason to drop the connection.
    }
  }

  private reconnect(): void {
    this.source?.close();
    this.source = null;

    if (this.retry !== null) {
      return;
    }

    this.connection.set('reconnecting');
    this.retry = setTimeout(() => {
      this.retry = null;
      this.connect();
    }, this.delay);

    this.delay = Math.min(this.delay * 2, BridgeEvents.MaximumDelayMs);
  }
}
