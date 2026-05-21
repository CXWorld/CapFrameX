import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { ApiService } from './core/services/api.service';
import { BridgeEventService } from './core/services/bridge-event.service';
import {
  AppVersionDto,
  BridgeEventEnvelope,
  CapabilitiesResponse,
  CaptureStatusDto,
  RecordsListResponse,
  ServiceHealthDto
} from './core/models/bridge.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  template: `
    <header class="topbar">
      <div>
        <h1>CapFrameX</h1>
        <span>{{ version()?.platform ?? 'unknown' }} / {{ version()?.processArchitecture ?? '-' }}</span>
      </div>
      <span class="status" [class.ok]="health()?.status === 'Healthy'">{{ health()?.status ?? 'offline' }}</span>
    </header>

    <main class="workspace">
      <section class="summary">
        <div>
          <span>Version</span>
          <strong>{{ version()?.version ?? '-' }}</strong>
        </div>
        <div>
          <span>Capture</span>
          <strong>{{ captureStatus()?.state ?? '-' }}</strong>
        </div>
        <div>
          <span>Records</span>
          <strong>{{ recordCount() }}</strong>
        </div>
        <div>
          <span>Events</span>
          <strong>{{ bridgeEvents.connectionState() }}</strong>
        </div>
      </section>

      <section class="panel">
        <h2>Bridge</h2>
        <dl>
          <div>
            <dt>Service</dt>
            <dd>{{ health()?.service ?? '-' }}</dd>
          </div>
          <div>
            <dt>Runtime</dt>
            <dd>{{ version()?.targetFramework ?? '-' }}</dd>
          </div>
          <div>
            <dt>Provider</dt>
            <dd>{{ captureStatus()?.provider ?? 'none' }}</dd>
          </div>
          <div>
            <dt>Last event</dt>
            <dd>{{ lastEvent()?.type ?? '-' }} #{{ lastEvent()?.sequence ?? '-' }}</dd>
          </div>
        </dl>
      </section>

      <section class="panel">
        <h2>Capabilities</h2>
        <ul class="capabilities">
          @for (capability of capabilities()?.capabilities ?? []; track capability.id) {
            <li>
              <div>
                <strong>{{ capability.name }}</strong>
                <span>{{ capability.scope }}</span>
              </div>
              <span class="state" [class.available]="capability.state === 'available'">{{ capability.state }}</span>
            </li>
          } @empty {
            <li class="empty">No capabilities reported</li>
          }
        </ul>
      </section>
    </main>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      background: #111418;
      color: #e8edf2;
    }

    .topbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 1rem 1.25rem;
      border-bottom: 1px solid #252b33;
      background: #171b21;
    }

    h1, h2 {
      margin: 0;
      font-weight: 650;
    }

    h1 {
      font-size: 1.35rem;
    }

    h2 {
      font-size: 1rem;
      margin-bottom: 1rem;
    }

    .topbar span, dt, .summary span, .capabilities span {
      color: #98a4b3;
      font-size: .82rem;
    }

    .status, .state {
      border: 1px solid #48515e;
      border-radius: 999px;
      padding: .25rem .6rem;
      color: #d5dce5;
      white-space: nowrap;
    }

    .status.ok, .state.available {
      border-color: #2c8f6b;
      color: #58d6a4;
    }

    .workspace {
      display: grid;
      gap: 1rem;
      padding: 1rem;
      max-width: 1100px;
      margin: 0 auto;
    }

    .summary {
      display: grid;
      grid-template-columns: repeat(4, minmax(0, 1fr));
      gap: .75rem;
    }

    .summary div, .panel {
      background: #171b21;
      border: 1px solid #252b33;
      border-radius: 8px;
      padding: 1rem;
    }

    .summary strong {
      display: block;
      margin-top: .35rem;
      font-size: 1.1rem;
    }

    dl, ul {
      margin: 0;
      padding: 0;
    }

    dl {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: .75rem 1rem;
    }

    dd {
      margin: .2rem 0 0;
      overflow-wrap: anywhere;
    }

    .capabilities {
      display: grid;
      gap: .65rem;
      list-style: none;
    }

    .capabilities li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      min-height: 3rem;
      border-top: 1px solid #252b33;
      padding-top: .65rem;
    }

    .capabilities strong {
      display: block;
      margin-bottom: .15rem;
    }

    .empty {
      color: #98a4b3;
    }

    @media (max-width: 720px) {
      .summary, dl {
        grid-template-columns: 1fr;
      }

      .topbar {
        align-items: flex-start;
        flex-direction: column;
      }
    }
  `]
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  readonly bridgeEvents = inject(BridgeEventService);
  private readonly subscriptions = new Subscription();

  title = 'CapFrameX';
  readonly health = signal<ServiceHealthDto | null>(null);
  readonly version = signal<AppVersionDto | null>(null);
  readonly capabilities = signal<CapabilitiesResponse | null>(null);
  readonly captureStatus = signal<CaptureStatusDto | null>(null);
  readonly records = signal<RecordsListResponse | null>(null);
  readonly lastEvent = signal<BridgeEventEnvelope | null>(null);
  readonly recordCount = computed(() => this.records()?.records.length ?? 0);

  ngOnInit(): void {
    this.subscriptions.add(this.api.getHealth().subscribe({
      next: health => this.health.set(health),
      error: () => this.health.set(null)
    }));
    this.subscriptions.add(this.api.getVersion().subscribe({
      next: version => this.version.set(version),
      error: () => this.version.set(null)
    }));
    this.subscriptions.add(this.api.getCapabilities().subscribe({
      next: capabilities => this.capabilities.set(capabilities),
      error: () => this.capabilities.set(null)
    }));
    this.subscriptions.add(this.api.getCaptureStatus().subscribe({
      next: status => this.captureStatus.set(status),
      error: () => this.captureStatus.set(null)
    }));
    this.subscriptions.add(this.api.getRecords().subscribe({
      next: records => this.records.set(records),
      error: () => this.records.set(null)
    }));
    this.subscriptions.add(this.bridgeEvents.events$.subscribe(event => this.lastEvent.set(event)));

    this.bridgeEvents.connect();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.bridgeEvents.disconnect();
  }
}
