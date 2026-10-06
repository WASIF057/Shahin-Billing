import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * An interceptor runs on every HTTP request. This one adds the JWT token,
 * and logs the user out if the API says the token is no longer valid (401).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  const authed = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(authed).pipe(
    catchError((err: HttpErrorResponse) => {
      // A 401 from the login-type calls just means a wrong password or code, not an ended session
      const loginCall = /\/auth\/(login|register|verify-otp|verify-registration|resend-otp|forgot-password|reset-password|google)$/.test(req.url);
      if (err.status === 401 && !loginCall) auth.logout();
      return throwError(() => err);
    }),
  );
};
