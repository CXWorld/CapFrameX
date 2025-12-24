/**
 * Power measurement data model
 */
export interface PowerMeasurement {
  timestamp: number;
  cpuPower?: number;
  gpuPower?: number;
  totalPower?: number;
}

/**
 * Service health status
 */
export interface ServiceHealth {
  status: string;
  service: string;
  timestamp: string;
}
