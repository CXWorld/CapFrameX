/** One entry in the navigation rail. */
export interface RailItem {
  /** The route it activates. */
  readonly path: string;

  /** What it is called, for the tooltip and the accessible name. */
  readonly label: string;

  /** The icon in the sprite. */
  readonly icon: string;

  /** Whether it sits at the bottom, away from the views. */
  readonly trailing?: boolean;
}

/**
 * The rail, in the order the mockup shows it.
 *
 * This list is the navigation: the router's routes are declared in the same order, and
 * `Ctrl+1..9` counts down it. Adding a view means adding it here and in `app.routes.ts`, and
 * nowhere else.
 */
export const RAIL_ITEMS: readonly RailItem[] = [
  { path: '/capture', label: 'Capture', icon: 'camera' },
  { path: '/analysis', label: 'Analysis', icon: 'chart-line' },
  { path: '/overlay', label: 'Overlay', icon: 'eye' },
  { path: '/comparison', label: 'Comparison', icon: 'arrows-left-right' },
  { path: '/aggregation', label: 'Aggregation', icon: 'stack-2' },
  { path: '/sensor', label: 'Sensor', icon: 'activity' },
  { path: '/report', label: 'Report', icon: 'file-description' },
  { path: '/cloud', label: 'Cloud', icon: 'cloud' },
  { path: '/settings', label: 'Settings', icon: 'settings', trailing: true },
];
