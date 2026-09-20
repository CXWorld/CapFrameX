import { HttpErrorResponse } from '@angular/common/http';

import { describeError } from './problem-details';

describe('describeError', () => {
  it('uses what the service said, because it is the sentence the user needs', () => {
    const described = describeError(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'The settings were not changed.', detail: 'stutteringFactor has to be greater than 1.' },
      }),
    );

    expect(described.title).toBe('The settings were not changed.');
    expect(described.detail).toBe('stutteringFactor has to be greater than 1.');
  });

  it('calls a service that is not answering what it is', () => {
    // Status 0 is its own situation: not started, stopped, or the port taken by something else.
    const described = describeError(new HttpErrorResponse({ status: 0 }));

    expect(described.title).toContain('not answering');
  });

  it('says a refusal is a refusal rather than a mistake the user made', () => {
    expect(describeError(new HttpErrorResponse({ status: 401 })).title).toContain('refused');
  });

  it('falls back to the status when the body is not problem details', () => {
    expect(describeError(new HttpErrorResponse({ status: 500, error: 'oops' })).title).toContain('500');
  });
});
