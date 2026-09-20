import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Sparkline } from './sparkline';

describe('Sparkline', () => {
  async function render(points: number[]): Promise<ComponentFixture<Sparkline>> {
    const fixture = TestBed.createComponent(Sparkline);
    fixture.componentRef.setInput('points', points);
    await fixture.whenStable();

    return fixture;
  }

  function path(fixture: ComponentFixture<Sparkline>): string {
    return fixture.nativeElement.querySelector('path')?.getAttribute('d') ?? '';
  }

  it('draws nothing for a capture with fewer than two points', async () => {
    expect(path(await render([16.6]))).toBe('');
  });

  it('draws one point per value', async () => {
    const drawn = path(await render([16, 17, 16, 33]));

    expect(drawn.split(/[ML]/).filter(Boolean).length).toBe(4);
  });

  it('puts the slowest frame at the bottom and the fastest at the top', async () => {
    // The y axis is inverted in SVG, so a longer frame time is a larger y.
    const drawn = path(await render([10, 30]));
    const [first, second] = drawn.split(' ').map((step) => Number(step.split(',')[1]));

    expect(first).toBeGreaterThan(second);
  });

  it('draws a flat capture through the middle rather than along an edge', async () => {
    // Along an edge it would read as "the value was zero" instead of "nothing happened".
    const fixture = await render([16.6, 16.6, 16.6]);
    const heights = path(fixture)
      .split(' ')
      .map((step) => Number(step.split(',')[1]));

    expect(new Set(heights).size).toBe(1);
    expect(heights[0]).toBe(10);
  });
});
