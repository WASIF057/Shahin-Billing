import { HttpErrorResponse } from '@angular/common/http';

/** Turns an API error into one readable sentence for a toast. */
export function errorMessage(err: unknown): string {
  if (typeof err === 'string') return err;   // a message written by the screen itself
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return 'Can’t reach the server. Check that the API is running.';
    if (err.status === 429) return 'Too many attempts. Please wait a minute and try again.';
    const body = err.error;
    if (body && typeof body === 'object') {
      if (body.errors && typeof body.errors === 'object') {
        const first = Object.values(body.errors as Record<string, string[]>).flat()[0];
        if (first) return first;
      }
      if (body.title) return body.title;
    }
    return `Request failed (${err.status}).`;
  }
  return 'Something went wrong.';
}

/** Field-level messages from a validation error, keyed by camelCase path. */
export function fieldErrors(err: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  if (err instanceof HttpErrorResponse && err.error?.errors) {
    for (const [k, v] of Object.entries(err.error.errors as Record<string, string[]>)) out[k] = v[0];
  }
  return out;
}
