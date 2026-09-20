import { HttpContext, HttpContextToken } from '@angular/common/http';

/**
 * Marks a request whose failure should not be shown to the user.
 *
 * For the polls that exist *in order to* notice that the service is gone. Reporting those would
 * fill the status bar with the news it is already showing, once every few seconds.
 */
export const SILENT = new HttpContextToken<boolean>(() => false);

/** The context for a request that reports its own failure. */
export function silently(): HttpContext {
  return new HttpContext().set(SILENT, true);
}
