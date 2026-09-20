import { ErrorSurface } from './error-surface';

describe('ErrorSurface', () => {
  it('shows the newest failure first', () => {
    const surface = new ErrorSurface();

    surface.report({ title: 'first', status: 500 });
    surface.report({ title: 'second', status: 500 });

    expect(surface.errors().map((error) => error.title)).toEqual(['second', 'first']);
  });

  it('keeps only a few, because a wall of them helps nobody', () => {
    const surface = new ErrorSurface();

    for (let i = 0; i < ErrorSurface.Capacity + 3; i++) {
      surface.report({ title: `failure ${i}`, status: 500 });
    }

    expect(surface.errors().length).toBe(ErrorSurface.Capacity);
  });

  it('removes the one that was acknowledged and leaves the rest', () => {
    const surface = new ErrorSurface();
    surface.report({ title: 'keep', status: 500 });
    surface.report({ title: 'dismiss', status: 500 });

    surface.dismiss(surface.errors()[0].id);

    expect(surface.errors().map((error) => error.title)).toEqual(['keep']);
  });
});
