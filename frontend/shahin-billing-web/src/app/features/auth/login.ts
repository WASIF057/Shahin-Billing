import { Component, inject, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { AppLogo } from '../../shared/app-logo';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, PasswordModule, MessageModule, IconFieldModule, InputIconModule, AppLogo],
  styleUrl: './auth.scss',
  template: `
    <div class="wrap">
      <section class="side">
        <div class="brand-logo"><app-logo /></div>
        <div class="pitch">
          <span class="eyebrow">GST billing software</span>
          <h1>Smart GST billing for your business.</h1>
          <p>Create accurate tax invoices, manage your clients and track every payment from one secure place.</p>
          <ul class="perks">
            <li><i class="pi pi-calculator"></i> Accurate GST on every invoice</li>
            <li><i class="pi pi-file-pdf"></i> Professional PDF tax invoices</li>
            <li><i class="pi pi-wallet"></i> Payment tracking and reminders</li>
          </ul>
          <div class="mock" aria-hidden="true">
            <div class="mock-head"><b>TAX INVOICE</b><span>INV-0042</span></div>
            <div class="mock-row"><span class="w60"></span><span class="w20"></span></div>
            <div class="mock-row"><span class="w50"></span><span class="w20"></span></div>
            <div class="mock-row"><span class="w40"></span><span class="w20"></span></div>
            <div class="mock-total"><span>Grand total</span><b>&#8377; 24,780.00</b></div>
            <span class="mock-badge"><i class="pi pi-check"></i> Paid</span>
          </div>
        </div>
        <small>&copy; {{ year }} Shahin Billing &middot; Secure and private</small>
      </section>
      <section class="form-side">
        @if (!challengeId()) {
          <form (ngSubmit)="submit()" #f="ngForm">
            <div class="form-head">
              <span class="form-icon"><i class="pi pi-lock"></i></span>
              <h2>Welcome back</h2>
              <p>Log in to continue to your billing dashboard.</p>
            </div>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <div class="field">
              <label for="email">Email</label>
              <p-iconfield>
                <p-inputicon class="pi pi-envelope" />
                <input pInputText id="email" name="email" type="email" [(ngModel)]="email" required autocomplete="username" placeholder="you@company.com" />
              </p-iconfield>
            </div>
            <div class="field">
              <label for="password">Password</label>
              <p-password inputId="password" name="password" [(ngModel)]="password" [feedback]="false" [toggleMask]="true"
                          [fluid]="true" required autocomplete="current-password" />
              <a class="forgot" routerLink="/forgot-password">Forgot password?</a>
            </div>
            <button pButton type="submit" label="Log in" [loading]="busy()" [disabled]="f.invalid"></button>

            <p class="alt">New here? <a routerLink="/register">Create an account</a></p>
          </form>
        } @else {
          <!-- Step 2: the 6-digit code we emailed -->
          <form (ngSubmit)="verify()">
            <div class="form-head"><span class="form-icon"><i class="pi pi-shield"></i></span><h2>Enter your code</h2></div>
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
export class Login implements OnDestroy {
  private auth = inject(AuthService);
  private router = inject(Router);

  year = new Date().getFullYear();
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

  ngOnDestroy() { this.stopTimer(); }
}
