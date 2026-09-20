import { DOCUMENT, Injectable, computed, effect, inject, signal } from '@angular/core';

/** What the user chose. */
export type ThemePreference = 'system' | 'light' | 'dark';

/** What is actually on screen. */
export type EffectiveTheme = 'light' | 'dark';

/**
 * Which theme is in force.
 *
 * The stylesheet answers this for anything drawn in CSS. Canvas does not read custom properties,
 * so the chart has to be told - and told again when the theme changes, which is why this is a
 * signal rather than a value read once at start-up.
 */
@Injectable({ providedIn: 'root' })
export class Theme {
  /** The attribute the stylesheet switches on. */
  static readonly Attribute = 'data-cx-theme';

  private readonly document = inject(DOCUMENT);
  private readonly systemPrefersDark = signal(false);

  /** What the user chose; 'system' follows the operating system. */
  readonly preference = signal<ThemePreference>('system');

  /** What is actually on screen. */
  readonly effective = computed<EffectiveTheme>(() => {
    const preference = this.preference();

    if (preference !== 'system') {
      return preference;
    }

    return this.systemPrefersDark() ? 'dark' : 'light';
  });

  constructor() {
    const media = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)');

    if (media) {
      this.systemPrefersDark.set(media.matches);
      media.addEventListener('change', (event) => this.systemPrefersDark.set(event.matches));
    }

    // The stylesheet needs the attribute; everything drawn in CSS follows from it.
    effect(() => {
      this.document.documentElement.setAttribute(Theme.Attribute, this.effective());
    });
  }

  /** Records what the user chose. */
  prefer(preference: ThemePreference): void {
    this.preference.set(preference);
  }
}
