import { Injectable, signal } from '@angular/core';

import { DescribedError } from './problem-details';

/** One failure, with what it takes to show and dismiss it. */
export interface SurfacedError extends DescribedError {
  readonly id: number;
}

/**
 * The one place failures go.
 *
 * Every refused request ends up here rather than in a `console.error` nobody reads or a dialog per
 * caller. What renders it is the shell's error surface; what fills it is the HTTP interceptor, so
 * a feature never has to think about it.
 */
@Injectable({ providedIn: 'root' })
export class ErrorSurface {
  /** How many failures are kept. Older ones are dropped: a wall of them helps nobody. */
  static readonly Capacity = 3;

  private readonly entries = signal<readonly SurfacedError[]>([]);
  private next = 1;

  /** The failures worth showing, newest first. */
  readonly errors = this.entries.asReadonly();

  /** Records a failure. */
  report(error: DescribedError): void {
    const entry: SurfacedError = { ...error, id: this.next++ };

    this.entries.update((current) => [entry, ...current].slice(0, ErrorSurface.Capacity));
  }

  /** Removes one failure, because the user acknowledged it. */
  dismiss(id: number): void {
    this.entries.update((current) => current.filter((error) => error.id !== id));
  }

  /** Removes all of them. */
  clear(): void {
    this.entries.set([]);
  }
}
