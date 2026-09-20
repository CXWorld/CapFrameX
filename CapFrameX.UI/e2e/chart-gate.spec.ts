import { expect, test } from '@playwright/test';

import { api, serviceIsRunning } from './service';

/** The gate from section 2.5 of the UI implementation plan. */
const FIRST_PAINT_BUDGET_MS = 150;
const INTERACTION_BUDGET_FPS = 50;

/** What the record library asks for, so the test measures what the list can open. */
const PAGE_SIZE = 200;

interface RecordSummary {
  id: string;
  name: string;
  frameCount: number;
  runCount: number;
}

interface RecordsPage {
  records: RecordSummary[];
  total: number;
}

/**
 * The chart gate.
 *
 * Against a running service and the user's real captures, because that is what the gate is a
 * statement about: uPlot on the largest thing CapFrameX actually produces. It reports itself
 * skipped where no service is running, so it never fails a machine that simply has not started
 * one - which also means a green run is no evidence that it measured anything.
 */
test.describe('chart gate', () => {
  test.skip(!serviceIsRunning(), 'No CapFrameX service is running.');

  test('the largest capture paints quickly and stays smooth under a drag', async ({ page }) => {
    // The list loads one page, newest first, and has no sort control yet - so the biggest capture
    // in the database is not necessarily one a user can open. The gate measures the biggest one
    // that is reachable, and reports the other number so the gap is on the record.
    const shown = await api<RecordsPage>(`/api/records?take=${PAGE_SIZE}`);
    const biggestOverall = await api<RecordsPage>('/api/records?take=1&sort=-frames');

    const largest = shown.records.reduce((a, b) => (b.frameCount > a.frameCount ? b : a));

    expect(largest, 'the service has no records to measure').toBeTruthy();
    console.log(
      `largest reachable capture: ${largest.name} - ${largest.frameCount} frames in ` +
        `${largest.runCount} run(s); largest in the database: ` +
        `${biggestOverall.records[0].frameCount} frames`,
    );

    await page.goto('/');

    // Found by the text on it rather than through the search box: what is measured here is the
    // chart, and a gate that depends on another feature reports that feature's bugs as its own.
    const card = page.getByRole('option').filter({ hasText: largest.name });
    await expect(card).toHaveCount(1, { timeout: 15_000 });
    await card.scrollIntoViewIfNeeded();

    // A chart for the record that opened by itself is already on screen, and its mark and its
    // request would otherwise be what gets measured - a pass on a capture forty times smaller.
    await page.evaluate(() => {
      performance.clearMarks('cx-chart-drawn');
      performance.clearResourceTimings();
    });

    await card.click();

    const canvas = page.locator('cx-chart canvas').first();
    await expect(canvas).toBeVisible({ timeout: 15_000 });

    // Wait for the chart that carries *this* capture, identified by the point count it recorded.
    await expect
      .poll(
        () =>
          page.evaluate(() => {
            const drawn = performance.getEntriesByName('cx-chart-drawn').at(-1) as
              | PerformanceMark
              | undefined;

            return (drawn?.detail?.points as number | undefined) ?? 0;
          }),
        { timeout: 30_000, message: 'the chart never drew the capture under test' },
      )
      .toBe(largest.frameCount);

    // First paint: from the moment the series response finished to the moment the canvas carried
    // it. Both come from the page's own performance timeline, so neither includes anything the
    // test itself did.
    const paint = await page.evaluate(() => {
      const drawn = performance.getEntriesByName('cx-chart-drawn').at(-1);
      const series = performance
        .getEntriesByType('resource')
        .filter((entry) => entry.name.includes('/series'))
        .at(-1);

      if (!drawn || !series) {
        return null;
      }

      return {
        afterDataMs: drawn.startTime - series.responseEnd,
        buildMs: (drawn as PerformanceMark).detail?.buildMs ?? 0,
        points: (drawn as PerformanceMark).detail?.points ?? 0,
      };
    });

    expect(paint, 'no chart was drawn').not.toBeNull();
    console.log(
      `first paint: ${paint!.afterDataMs.toFixed(1)} ms after the series arrived ` +
        `(${paint!.buildMs.toFixed(1)} ms building, ${paint!.points} points)`,
    );

    // Drag across the chart the way a user selects a zoom range, sampling the page's own frames.
    //
    // Over uPlot's own overlay layer, not the canvas: the canvas includes the axes, so a drag
    // measured from its box starts inside the y axis, where uPlot does not listen - which looks
    // exactly like a chart that zooms for free.
    const overlay = page.locator('cx-chart .u-over').first();
    const box = (await overlay.boundingBox())!;
    const y = box.y + box.height / 2;
    const steps = 40;

    await page.evaluate(() => {
      const frames: number[] = [];
      (window as unknown as { __frames: number[] }).__frames = frames;

      let previous = performance.now();
      const tick = (now: number) => {
        frames.push(now - previous);
        previous = now;
        requestAnimationFrame(tick);
      };

      requestAnimationFrame(tick);
    });

    // uPlot moves only its selection rectangle while the button is down and redraws the series
    // when it is released. Counting its draws is what separates "the zoom cost nothing" from
    // "nothing was drawn": without this, a chart that ignored the drag would pass at 60 fps.
    const drawsBefore = await page.evaluate(
      () => performance.getEntriesByName('cx-chart-drawn').length,
    );

    await page.mouse.move(box.x + box.width * 0.1, y);
    await page.mouse.down();

    for (let step = 1; step <= steps; step++) {
      await page.mouse.move(box.x + box.width * (0.1 + (0.8 * step) / steps), y);
    }

    // Releasing is what applies the zoom, and rescaling redraws every point. Sampling has to run
    // past it, or the measurement covers only the cheap half of the interaction.
    await page.mouse.up();
    await page.waitForTimeout(500);

    const redraws =
      (await page.evaluate(() => performance.getEntriesByName('cx-chart-drawn').length)) -
      drawsBefore;

    const frameRate = await page.evaluate(() => {
      const frames = (window as unknown as { __frames: number[] }).__frames.slice(1);

      if (frames.length < 10) {
        return null;
      }

      const sorted = [...frames].sort((a, b) => a - b);
      const median = sorted[Math.floor(sorted.length / 2)];
      const worst = sorted.at(-1)!;

      return {
        median: 1000 / median,
        // The slowest single frame, which is where a zoom of a hundred thousand points shows up
        // if it shows up anywhere.
        worstFrameMs: worst,
        worstFps: 1000 / worst,
        samples: frames.length,
      };
    });

    expect(frameRate, 'no frames were rendered during the drag').not.toBeNull();
    expect(redraws, 'the drag never redrew the chart, so the frame rate measures nothing')
      .toBeGreaterThan(0);
    console.log(`drag redrew the chart ${redraws} time(s)`);
    console.log(
      `drag: ${frameRate!.median.toFixed(1)} fps median over ${frameRate!.samples} frames, ` +
        `worst frame ${frameRate!.worstFrameMs.toFixed(1)} ms ` +
        `(${frameRate!.worstFps.toFixed(1)} fps)`,
    );

    expect(paint!.afterDataMs).toBeLessThan(FIRST_PAINT_BUDGET_MS);
    expect(frameRate!.median).toBeGreaterThanOrEqual(INTERACTION_BUDGET_FPS);
  });
});
