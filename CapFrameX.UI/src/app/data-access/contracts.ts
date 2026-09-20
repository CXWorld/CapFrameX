/**
 * What the service sends and accepts.
 *
 * Hand-written for now and kept in step with `CapFrameX.Service.Contracts` by hand. WP-B4 replaces
 * this file with types generated from the service's OpenAPI document and adds a check that fails
 * the build when the two drift; until then, a change on the service side has to be made here too.
 */

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

/* Records */

export interface RecordSummaryDto {
  id: string;
  name: string;
  gameName?: string | null;
  processName?: string | null;
  createdAt: string;
  durationSeconds: number;
  runCount: number;
  frameCount: number;
  sparkline: number[];
  processor?: string | null;
  gpu?: string | null;
  hasPcLatency: boolean;
  hasDisplayChange: boolean;
  averageFps?: number | null;
  p1Fps?: number | null;
  p99Fps?: number | null;
}

export interface RecordsListResponse {
  records: RecordSummaryDto[];
  total: number;
}

export interface ChipDto {
  key: string;
  label: string;
}

export interface RecordInfoDto {
  gameName?: string | null;
  processName?: string | null;
  comment?: string | null;
  processor?: string | null;
  motherboard?: string | null;
  systemRam?: string | null;
  gpu?: string | null;
  gpuCount?: string | null;
  gpuCoreClock?: string | null;
  gpuMemoryClock?: string | null;
  baseDriverVersion?: string | null;
  driverPackage?: string | null;
  gpuDriverVersion?: string | null;
  os?: string | null;
  apiInfo?: string | null;
  resizableBar?: boolean | null;
  winGameMode?: boolean | null;
  hags?: boolean | null;
  presentationMode?: string | null;
  resolutionInfo?: string | null;
  appVersion?: string | null;
  deviceName?: string | null;
}

export interface RecordRunDto {
  index: number;
  durationSeconds: number;
  frameCount: number;
  presentMonRuntime?: string | null;
  hasPcLatency: boolean;
  hasDisplayChange: boolean;
  hasGpuActive: boolean;
}

export interface RecordSourceDto {
  filePath?: string | null;
  fileSize?: number | null;
  modifiedUtc?: string | null;
  indexVersion: number;
}

export interface RecordDetailDto {
  summary: RecordSummaryDto;
  info: RecordInfoDto;
  runs: RecordRunDto[];
  chips: ChipDto[];
  source: RecordSourceDto;
}

/** A field left out is left alone; an empty string clears it. */
export interface RecordEdit {
  gameName?: string;
  comment?: string;
  processor?: string;
  gpu?: string;
  systemRam?: string;
  motherboard?: string;
  resolutionInfo?: string;
}

/* Analysis */

export interface PointDto {
  x: number;
  y: number;
}

export interface MetricDto {
  key: string;
  label: string;
  value?: number | null;
  unit: string;
}

export interface SpikeDto {
  timeSeconds: number;
  milliseconds: number;
}

export interface FramePacingDto {
  smoothPercent: number;
  stutterPercent: number;
  lowFpsPercent: number;
  spikeCount: number;
  worstSpike?: SpikeDto | null;
}

export interface PcLatencyDto {
  averageMs: number;
  p99Ms: number;
}

export interface AnalysisWindowDto {
  startSeconds: number;
  endSeconds: number;
  frameCount: number;
  run?: number | null;
}

export interface AnalysisThresholdsDto {
  stutteringFactor: number;
  stutteringThreshold: number;
  outlierMethod: string;
}

export interface AnalysisDto {
  metrics: MetricDto[];
  framePacing: FramePacingDto;
  pcLatency?: PcLatencyDto | null;
  lShape: PointDto[];
  distribution: PointDto[];
  window: AnalysisWindowDto;
  thresholds: AnalysisThresholdsDto;
}

/** Columnar: one time axis, every other column indexed by it, null where a frame has no value. */
export interface SeriesResponse {
  time: number[];
  frametimes?: number[] | null;
  fps?: number[] | null;
  displayChange?: (number | null)[] | null;
  pcLatency?: (number | null)[] | null;
  window: AnalysisWindowDto;
}

/* Settings and import */

export interface AnalysisSettingsDto {
  stutteringFactor: number;
  stutteringThreshold: number;
  movingAverageWindowSize: number;
  intervalAverageWindowTime: number;
  fpsValuesRoundingDigits: number;
  outlierMethod: string;
  metrics: string[];
  lShapeMetric: string;
}

export interface PathSettingsDto {
  captureDirectory: string;
}

export interface AppearanceSettingsDto {
  theme: string;
}

export interface ImportSettingsDto {
  offered: boolean;
}

export interface AppSettingsDto {
  analysis: AnalysisSettingsDto;
  paths: PathSettingsDto;
  appearance: AppearanceSettingsDto;
  import: ImportSettingsDto;
}

export interface AppSettingsPatch {
  analysis?: Partial<AnalysisSettingsDto>;
  paths?: { captureDirectory?: string };
  appearance?: { theme?: string };
  import?: { offered?: boolean };
}

export interface ImportSourceDto {
  path: string;
  origin: string;
  captureCount: number;
}

export interface ImportSourcesResponse {
  sources: ImportSourceDto[];
  offered: boolean;
}

export interface ImportResultDto {
  imported: number;
  alreadyKnown: number;
  failed: number;
  total: number;
  errors: string[];
}
