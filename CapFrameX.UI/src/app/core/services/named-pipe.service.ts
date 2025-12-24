import { Injectable, signal } from '@angular/core';
import { Subject, Observable } from 'rxjs';

/**
 * Service for real-time power measurement data via Named Pipes
 * Connects to: \\.\pipe\CapFrameXPmdData
 */
@Injectable({
  providedIn: 'root'
})
export class NamedPipeService {
  private readonly dataSubject = new Subject<any>();
  private readonly connectionStatus = signal<'connected' | 'disconnected' | 'connecting'>('disconnected');

  /**
   * Observable stream of power measurement data
   */
  readonly data$: Observable<any> = this.dataSubject.asObservable();

  /**
   * Current connection status (Angular Signal)
   */
  readonly status = this.connectionStatus.asReadonly();

  /**
   * Connect to the named pipe server
   */
  async connect(): Promise<void> {
    this.connectionStatus.set('connecting');

    // TODO: Implement named pipe connection using Tauri IPC
    // This will require Tauri commands to communicate with the Windows named pipe

    console.log('Connecting to named pipe: \\\\.\\pipe\\CapFrameXPmdData');
  }

  /**
   * Disconnect from the named pipe server
   */
  disconnect(): void {
    this.connectionStatus.set('disconnected');
    // TODO: Implement disconnection logic
  }

  /**
   * Send data through the pipe (if bidirectional)
   */
  send(data: any): void {
    // TODO: Implement send logic if needed
  }
}
