import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, tap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { ServerStatus } from './server-status.service';

/**
 * An interceptor runs on every HTTP request. This one adds the JWT token,
 * and logs the user out if the API says the token is no longer valid (401).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const server = inject(ServerStatus);
  const token = auth.token;
  const authed = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authed).pipe(
    tap(ev => { if (ev instanceof HttpResponse) server.markUp(); }),   // any answer at all means the server is there
    catchError((err: HttpErrorResponse) => {
      // no connection, or a 502 / 503 / 504 from the proxy in front of the server: show the "can't reach the server" screen
      if (err.status === 0 || err.status === 502 || err.status === 503 || err.status === 504) server.markDown();
      else server.markUp();
      // A 401 from the login-type calls just means a wrong password or code, not an ended session
      const loginCall = /\/auth\/(login|register|verify-otp|verify-registration|resend-otp|forgot-password|reset-password)$/.test(req.url);
      if (err.status === 401 && !loginCall) auth.logout();
      return throwError(() => err);
    }),
  );
};
