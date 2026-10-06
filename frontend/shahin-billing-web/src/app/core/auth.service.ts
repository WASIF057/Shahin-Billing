import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, LoginResponse, UserInfo } from './models';

const STORAGE_KEY = 'shahin-billing-auth';

/**
 * Holds the logged-in user in a *signal*. A signal is a value Angular watches:
 * when it changes, every template that reads it updates automatically.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private url = `${environment.apiUrl}/auth`;

  private session = signal<AuthResponse | null>(this.load());
  readonly user = computed<UserInfo | null>(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => {
    const s = this.session();
    return !!s && new Date(s.expiresAt).getTime() > Date.now();
  });

  /** Staff can bill and take payments only; the owner can do everything. */
  /** A client's ordering login: sees only the ordering pages. */
  readonly isClient = computed(() => this.user()?.role === 'Client');
  readonly isOwner = computed(() => (this.user()?.role ?? 'Owner') === 'Owner');

  get token(): string | null { return this.isLoggedIn() ? this.session()!.token : null; }

  /** Step 1. Logs in straight away, or (when email codes are on) says a code was emailed. */
  login(email: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.url}/login`, { email, password }).pipe(tap(r => { if (r.auth) this.store(r.auth); }));
  }

  /** Step 2: the 6-digit code from the email. */
  verifyOtp(challengeId: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.url}/verify-otp`, { challengeId, code }).pipe(tap(r => this.store(r)));
  }

  resendOtp(challengeId: string): Observable<{ maskedEmail: string }> {
    return this.http.post<{ maskedEmail: string }>(`${this.url}/resend-otp`, { challengeId });
  }

  forgotPassword(email: string) {
    return this.http.post<{ message: string }>(`${this.url}/forgot-password`, { email });
  }

  resetPassword(email: string, code: string, newPassword: string) {
    return this.http.post<void>(`${this.url}/reset-password`, { email, code, newPassword });
  }

  googleLogin(idToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.url}/google`, { idToken }).pipe(tap(r => this.store(r)));
  }

  /** Tells the login screen whether to show the Google button. */
  config(): Observable<{ googleClientId: string | null }> {
    return this.http.get<{ googleClientId: string | null }>(`${this.url}/config`);
  }

  /** Step 1 of sign-up. With email set up, a code is emailed and nothing is created until it is entered. */
  register(data: { name: string; email: string; password: string; businessName: string }): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.url}/register`, data).pipe(tap(r => { if (r.auth) this.store(r.auth); }));
  }

  /** Step 2 of sign-up: the emailed code creates the account. */
  verifyRegistration(challengeId: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.url}/verify-registration`, { challengeId, code }).pipe(tap(r => this.store(r)));
  }

  /** Ends every open session of this user on every device. */
  logoutAll() { return this.http.post<void>(`${this.url}/logout-all`, {}); }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.post<void>(`${this.url}/change-password`, { currentPassword, newPassword });
  }

  /** Called after the business name changes so the top bar stays correct. */
  setBusinessName(name: string) {
    const s = this.session();
    if (s) this.store({ ...s, user: { ...s.user, businessName: name } });
  }

  logout(redirect = true) {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
    if (redirect) this.router.navigate(['/login']);
  }

  private store(r: AuthResponse) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(r));
    this.session.set(r);
  }

  private load(): AuthResponse | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AuthResponse) : null;
    } catch {
      return null;
    }
  }
}
