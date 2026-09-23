/** The colours a canvas renderer needs, resolved from the token layer. */
export interface ChartColors {
  readonly series: readonly string[];
  readonly axis: string;
  readonly grid: string;
  readonly alert: string;
}

/** How many series colours the token layer defines. */
const SERIES_COUNT = 8;

/**
 * Reads the design tokens as actual colours.
 *
 * Canvas cannot use `var(--cx-series-1)`, so the values have to be resolved from the element the
 * chart sits in - which is also what makes them follow a theme change: after the attribute flips,
 * the same call returns the other palette.
 */
export function readChartColors(element: Element): ChartColors {
  const style = getComputedStyle(element);
  const token = (name: string, fallback: string) => style.getPropertyValue(name).trim() || fallback;

  const series: string[] = [];

  for (let index = 1; index <= SERIES_COUNT; index++) {
    series.push(token(`--cx-series-${index}`, '#185fa5'));
  }

  return {
    series,
    // Muted, not faint: axis values are read, not merely noticed, and the faint tier falls below
    // 4.5:1 on both backgrounds.
    axis: token('--cx-text-muted', '#5f5e5a'),
    grid: token('--cx-line', 'rgba(0,0,0,0.1)'),
    alert: token('--cx-alert', '#d85a30'),
  };
}
