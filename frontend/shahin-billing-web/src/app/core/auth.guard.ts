import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** A guard decides whether a route may open. Not logged in -> go to /login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? true : inject(Router).createUrlTree(['/login']);
};

/** Owner-only screens. Staff go to the bills list, and a client to the ordering page. */
export const ownerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isOwner() ? true : inject(Router).createUrlTree([auth.isClient() ? '/portal' : '/bills']);
};

/** Screens for the owner and staff. A client's login is sent to the ordering page. */
export const notClientGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isClient() ? inject(Router).createUrlTree(['/portal']) : true;
};

/** The ordering pages, for a client's login only. */
export const clientGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isClient() ? true : inject(Router).createUrlTree(['/']);
};

export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? inject(Router).createUrlTree(['/']) : true;
};
