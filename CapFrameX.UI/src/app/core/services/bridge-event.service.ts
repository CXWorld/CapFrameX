import { Injectable, inject, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import { BridgeEventEnvelope } from '../models/bridge.models';
import { ApiService } from './api.service';

export type BridgeConnectionState = 'idle' | 'connecting' | 'open' | 'closed' | 'error';

@Injectable({
  providedIn: 'root'
})
export class BridgeEventService {
  private readonly api = inject(ApiService);
  private readonly eventsSubject = new Subject<BridgeEventEnvelope>();
  private source: EventSource | null = null;

  readonly connectionState = signal<BridgeConnectionState>('idle');
  readonly events$: Observable<BridgeEventEnvelope> = this.eventsSubject.asObservable();

  connect(): void {
    if (this.source) {
      return;
    }

    this.connectionState.set('connecting');

    const source = new EventSource(this.api.eventsUrl);
    this.source = source;

    source.onopen = (): void => {
      this.connectionState.set('open');
    };

    source.onerror = (): void => {
      this.connectionState.set('error');
    };

    source.onmessage = (event: MessageEvent<string>): void => {
      this.emit(event);
    };

    source.addEventListener('app.heartbeat', (event): void => {
      this.emit(event as MessageEvent<string>);
    });
  }

  disconnect(): void {
    this.source?.close();
    this.source = null;
    this.connectionState.set('closed');
  }

  private emit(event: MessageEvent<string>): void {
    if (!event.data) {
      return;
    }

    try {
      this.eventsSubject.next(JSON.parse(event.data) as BridgeEventEnvelope);
    } catch (error) {
      console.error('Failed to parse bridge event', error);
    }
  }
}
