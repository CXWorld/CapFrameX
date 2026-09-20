import { Routes } from '@angular/router';

/**
 * One lazy route per rail item, in the order the rail shows them.
 *
 * Lazy from the start rather than later: the analysis view pulls in a charting library and the
 * overlay editor a preview renderer, and neither belongs in the bundle somebody waits for before
 * the window appears.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'analysis' },
  {
    path: 'capture',
    title: 'Capture',
    loadComponent: () => import('./features/capture/capture-view').then((m) => m.CaptureView),
  },
  {
    path: 'analysis',
    title: 'Analysis',
    loadComponent: () => import('./features/analysis/analysis-view').then((m) => m.AnalysisView),
  },
  {
    path: 'overlay',
    title: 'Overlay',
    loadComponent: () => import('./features/overlay/overlay-view').then((m) => m.OverlayView),
  },
  {
    path: 'comparison',
    title: 'Comparison',
    loadComponent: () => import('./features/comparison/comparison-view').then((m) => m.ComparisonView),
  },
  {
    path: 'aggregation',
    title: 'Aggregation',
    loadComponent: () => import('./features/aggregation/aggregation-view').then((m) => m.AggregationView),
  },
  {
    path: 'sensor',
    title: 'Sensor',
    loadComponent: () => import('./features/sensor/sensor-view').then((m) => m.SensorView),
  },
  {
    path: 'report',
    title: 'Report',
    loadComponent: () => import('./features/report/report-view').then((m) => m.ReportView),
  },
  {
    path: 'cloud',
    title: 'Cloud',
    loadComponent: () => import('./features/cloud/cloud-view').then((m) => m.CloudView),
  },
  {
    path: 'settings',
    title: 'Settings',
    loadComponent: () => import('./features/settings/settings-view').then((m) => m.SettingsView),
  },
  { path: '**', redirectTo: 'analysis' },
];
