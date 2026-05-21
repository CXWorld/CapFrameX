export interface ServiceHealthDto {
  status: string;
  service: string;
  timestamp: string;
}

export interface AppVersionDto {
  applicationName: string;
  version: string;
  informationalVersion: string;
  targetFramework: string;
  processArchitecture: string;
  platform: string;
}

export type CapabilityState = 'available' | 'unavailable' | 'planned' | string;

export interface CapabilityDto {
  id: string;
  name: string;
  state: CapabilityState;
  scope: string;
  reason?: string | null;
}

export interface CapabilitiesResponse {
  platform: string;
  osDescription: string;
  processArchitecture: string;
  capabilities: CapabilityDto[];
}

export interface CaptureStatusDto {
  state: string;
  provider?: string | null;
  providerAvailable: boolean;
  startedAt?: string | null;
  activeProcessName?: string | null;
  unavailableReason?: string | null;
}

export interface RecordSummaryDto {
  id: string;
  name: string;
  gameName?: string | null;
  processName?: string | null;
  createdAt: string;
  averageFps?: number | null;
  p1Fps?: number | null;
  p99Fps?: number | null;
  runCount: number;
}

export interface RecordsListResponse {
  records: RecordSummaryDto[];
}

export interface BridgeEventEnvelope<TPayload = unknown> {
  type: string;
  version: number;
  sequence: number;
  timestamp: string;
  payload: TPayload;
}

export interface AppHeartbeatDto {
  timestamp: string;
}
