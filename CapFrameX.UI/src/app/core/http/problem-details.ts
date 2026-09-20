import { HttpErrorResponse } from '@angular/common/http';

/**
 * The body the service sends when it refuses something, as RFC 9457 describes it.
 */
export interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
  readonly status?: number;
  readonly type?: string;
}

/** A failure in the words the user should see. */
export interface DescribedError {
  readonly title: string;
  readonly detail?: string;
  readonly status: number;
}

/**
 * Turns a failed request into something worth showing.
 *
 * The service says what went wrong and why - "'loudness' is not a metric", "the capture behind
 * this record cannot be read" - and that is the sentence the user needs. Angular's own message
 * ("Http failure response for ...") is about the transport and helps nobody.
 */
export function describeError(failure: HttpErrorResponse): DescribedError {
  const problem = asProblemDetails(failure.error);

  if (problem) {
    return {
      title: problem.title ?? fallbackTitle(failure.status),
      detail: problem.detail,
      status: problem.status ?? failure.status,
    };
  }

  // Status 0 is the service not answering at all, which is its own situation: it has not started,
  // it has stopped, or the port is taken by something else.
  if (failure.status === 0) {
    return {
      title: 'The CapFrameX service is not answering.',
      detail: 'It may not be running yet, or it stopped.',
      status: 0,
    };
  }

  return { title: fallbackTitle(failure.status), status: failure.status };
}

function asProblemDetails(body: unknown): ProblemDetails | null {
  if (typeof body !== 'object' || body === null) {
    return null;
  }

  const candidate = body as ProblemDetails;

  return typeof candidate.title === 'string' || typeof candidate.detail === 'string' ? candidate : null;
}

function fallbackTitle(status: number): string {
  switch (status) {
    case 401:
    case 403:
      // The guard refused it. Worth its own wording: it is not a mistake the user made.
      return 'The service refused this request.';
    case 404:
      return 'Not found.';
    default:
      return `The request failed (${status}).`;
  }
}
