import { Component, inject, NgZone, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';

/** Minimal shape of Google's sign-in script (loaded only when a Google client id is configured). */
interface GoogleId {
  accounts: { id: {
    initialize(o: { client_id: string; callback: (r: { credential: string }) => void }): void;
    renderButton(el: HTMLElement, o: Record<string, unknown>): void;
  } };
}

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  styleUrl: './auth.scss',
  template: `
    <div class="wrap">
      <section class="side">
        <div class="brand"><span class="mark" aria-hidden="true"></span> Shahin Billing</div>
        <div>
          <h1>Bills in seconds, not in a bill book.</h1>
          <p>Pick a client, pick your items. Rates, GST and totals fill in by themselves, and the PDF is ready to send.</p>
          <ul class="perks">
            <li><i class="pi pi-bolt"></i> Rates and GST fill in automatically</li>
            <li><i class="pi pi-file-pdf"></i> Professional PDF tax invoices</li>
            <li><i class="pi pi-envelope"></i> Bills emailed to you and your client</li>
          </ul>
        </div>
        <small>GST tax invoices for B2B sales</small>
      </section>
      <section class="form-side">
        @if (!challengeId()) {
          <form (ngSubmit)="submit()" #f="ngForm">
            <h2>Log in</h2>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <div class="field">
              <label for="email">Email</label>
              <input pInputText id="email" name="email" type="email" [(ngModel)]="email" required autocomplete="username" />
            </div>
            <div class="field">
              <label for="password">Password</label>
              <p-password inputId="password" name="password" [(ngModel)]="password" [feedback]="false" [toggleMask]="true"
                          [fluid]="true" required autocomplete="current-password" />
              <a class="forgot" routerLink="/forgot-password">Forgot password?</a>
            </div>
            <button pButton type="submit" label="Log in" [loading]="busy()" [disabled]="f.invalid"></button>

            @if (googleOn()) {
              <div class="divider"><span>or</span></div>
              <div id="google-btn" class="google-wrap"></div>
            }
            <p class="alt">New here? <a routerLink="/register">Create an account</a></p>
          </form>
        } @else {
          <!-- Step 2: the 6-digit code we emailed -->
          <form (ngSubmit)="verify()">
            <h2>Enter your code</h2>
            <p class="muted" style="margin: 0">We emailed a 6-digit code to <strong>{{ masked() }}</strong>. It works for 10 minutes.</p>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <input pInputText class="code" name="code" [ngModel]="code" (ngModelChange)="onCode($event)" maxlength="6"
                   inputmode="numeric" autocomplete="one-time-code" placeholder="000000" aria-label="6-digit code" />
            <button pButton type="submit" label="Verify and log in" [loading]="busy()" [disabled]="code.length !== 6"></button>
            <p class="alt">
              <button type="button" class="link-btn" (click)="resend()" [disabled]="cooldown() > 0 || busy()">
                Send a new code{{ cooldown() > 0 ? ' (' + cooldown() + 's)' : '' }}
              </button>
              ·
              <button type="button" class="link-btn" (click)="back()">Use a different account</button>
            </p>
          </form>
        }
      </section>
    </div>
  `,
})
export class Login implements OnInit, OnDestroy {
  private auth = inject(AuthService);
  private router = inject(Router);
  private zone = inject(NgZone);

  email = '';
  password = '';
  code = '';
  busy = signal(false);
  error = signal<string | null>(null);

  // step 2 (email code)
  challengeId = signal<string | null>(null);
  masked = signal('');
  cooldown = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;

  // Google
  googleOn = signal(false);

  ngOnInit() {
    // The Google button appears only when the server has a Google client id
    this.auth.config().subscribe({
      next: c => { if (c.googleClientId) { this.googleOn.set(true); setTimeout(() => this.loadGoogle(c.googleClientId!), 0); } },
      error: () => {},
    });
  }

  submit() {
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.email, this.password).subscribe({
      next: r => {
        this.busy.set(false);
        if (r.otpRequired) {
          this.challengeId.set(r.challengeId);
          this.masked.set(r.maskedEmail ?? 'your email');
          this.code = '';
          this.startCooldown(30);
        } else this.router.navigate(['/']);
      },
      error: e => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }

  /** Sends the code as soon as the sixth digit is typed. */
  onCode(v: string) {
    this.code = (v ?? '').replace(/\D/g, '').slice(0, 6);
    if (this.code.length === 6 && !this.busy()) this.verify();
  }

  verify() {
    if (this.code.length !== 6 || !this.challengeId() || this.busy()) return;   // already sending: a code works only once
    this.busy.set(true);
    this.error.set(null);
    this.auth.verifyOtp(this.challengeId()!, this.code).subscribe({
      next: () => this.router.navigate(['/']),
      error: e => { this.error.set(errorMessage(e)); this.busy.set(false); this.code = ''; },
    });
  }

  resend() {
    if (!this.challengeId()) return;
    this.error.set(null);
    this.auth.resendOtp(this.challengeId()!).subscribe({
      next: r => { this.masked.set(r.maskedEmail); this.startCooldown(30); },
      error: e => this.error.set(errorMessage(e)),
    });
  }

  back() { this.stopTimer(); this.challengeId.set(null); this.code = ''; this.error.set(null); this.password = ''; }

  private startCooldown(seconds: number) {
    this.stopTimer();
    this.cooldown.set(seconds);
    this.timer = setInterval(() => {
      this.cooldown.update(v => Math.max(v - 1, 0));
      if (this.cooldown() === 0) this.stopTimer();
    }, 1000);
  }
  private stopTimer() { if (this.timer) { clearInterval(this.timer); this.timer = null; } }

  // ---- Google sign-in ----
  private loadGoogle(clientId: string) {
    const w = window as unknown as { google?: GoogleId };
    const init = () => {
      const el = document.getElementById('google-btn');
      if (!w.google || !el) return;
      w.google.accounts.id.initialize({
        client_id: clientId,
        callback: r => this.zone.run(() => this.googleSignIn(r.credential)),
      });
      w.google.accounts.id.renderButton(el, { theme: 'outline', size: 'large', width: 340, text: 'signin_with', shape: 'pill' });
    };
    if (w.google) { init(); return; }
    const s = document.createElement('script');
    s.src = 'https://accounts.google.com/gsi/client';
    s.async = true;
    s.onload = init;
    document.head.appendChild(s);
  }

  private googleSignIn(credential: string) {
    this.busy.set(true);
    this.error.set(null);
    this.auth.googleLogin(credential).subscribe({
      next: () => this.router.navigate(['/']),
      error: e => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }

  ngOnDestroy() { this.stopTimer(); }
}
