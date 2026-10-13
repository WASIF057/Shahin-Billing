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
import { AuthMood } from '../../shared/auth-mood';
import { AuthScene } from '../../shared/auth-scene';
import { Tilt } from '../../shared/motion';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, PasswordModule, MessageModule, IconFieldModule, InputIconModule, AppLogo, Tilt, AuthScene],
  styleUrl: './auth.scss',
  template: `
    <div class="wrap">
      <section class="side" [appTilt]="9">
        <app-auth-scene />
        <div class="brand-logo"><app-logo /></div>
        <div class="pitch">
          <span class="eyebrow">Get started in minutes</span>
          <h1>Set up once, then bill every day.</h1>
          <p>Add your business details, products and clients. After that, every invoice takes just a few clicks.</p>
          <ul class="perks">
            <li><i class="pi pi-building"></i> Your business, your branding</li>
            <li><i class="pi pi-box"></i> Products with client-specific rates</li>
            <li><i class="pi pi-shield"></i> Your data stays private to your account</li>
          </ul>
          <div class="mock-3d"><div class="mock" aria-hidden="true">
            <div class="mock-head"><b>TAX INVOICE</b><span>INV-0042</span></div>
            <div class="mock-row"><span class="w60"></span><span class="w20"></span></div>
            <div class="mock-row"><span class="w50"></span><span class="w20"></span></div>
            <div class="mock-row"><span class="w40"></span><span class="w20"></span></div>
            <div class="mock-total"><span>Grand total</span><b>&#8377; 24,780.00</b></div>
            <span class="mock-badge"><i class="pi pi-check"></i> Paid</span>
          </div></div>
        </div>
        <small>&copy; {{ year }} Shahin Billing &middot; Secure and private</small>
      </section>
      <section class="form-side">
        @if (!challengeId()) {
          <form [appTilt]="2.5" [class.shake]="shake()" (focusin)="focusIn($event)" (focusout)="focusOut()" (ngSubmit)="submit()" #f="ngForm">
            <div class="form-head">
              <span class="form-icon"><i class="pi pi-user-plus"></i></span>
              <h2>Create your account</h2>
              <p>It takes less than a minute. No card needed.</p>
            </div>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <div class="field">
              <label for="biz">Business name</label>
              <p-iconfield>
                <p-inputicon class="pi pi-building" />
                <input pInputText id="biz" name="biz" [(ngModel)]="model.businessName" required placeholder="Your business name" />
              </p-iconfield>
            </div>
            <div class="field">
              <label for="name">Your name</label>
              <p-iconfield>
                <p-inputicon class="pi pi-user" />
                <input pInputText id="name" name="name" [(ngModel)]="model.name" required autocomplete="name" placeholder="Full name" />
              </p-iconfield>
            </div>
            <div class="field">
              <label for="email">Email</label>
              <p-iconfield>
                <p-inputicon class="pi pi-envelope" />
                <input pInputText id="email" name="email" type="email" [(ngModel)]="model.email" required autocomplete="email" placeholder="you@company.com" />
              </p-iconfield>
              <span class="hint">We’ll email a 6-digit code to confirm it.</span>
            </div>
            <div class="field">
              <label for="password">Password</label>
              <p-password inputId="password" name="password" [(ngModel)]="model.password" [feedback]="false" [toggleMask]="true" [fluid]="true"
                          required [minlength]="8" autocomplete="new-password" />
              <div class="meter" [attr.data-level]="strength().score" aria-live="polite">
                <div class="segs">@for (i of [0, 1, 2, 3]; track i) { <i [class.on]="strength().score > i"></i> }</div>
                <span class="mlabel">{{ model.password ? strength().label : 'At least 8 characters.' }}</span>
              </div>
            </div>
            <button pButton type="submit" label="Create account" [loading]="busy()" [disabled]="f.invalid"></button>
            <p class="alt">Already have an account? <a routerLink="/login">Log in</a></p>
          </form>
        } @else {
          <!-- Step 2: confirm the email with the code we sent. The account is created only now. -->
          <form [appTilt]="2.5" [class.shake]="shake()" (focusin)="focusIn($event)" (focusout)="focusOut()" (ngSubmit)="verify()">
            <div class="form-head"><span class="form-icon"><i class="pi pi-envelope"></i></span><h2>Confirm your email</h2></div>
            <p class="muted" style="margin: 0">We emailed a 6-digit code to <strong>{{ masked() }}</strong>. Enter it to finish creating your account. It works for 10 minutes.</p>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <input pInputText class="code" name="code" [ngModel]="code" (ngModelChange)="onCode($event)" maxlength="6"
                   inputmode="numeric" autocomplete="one-time-code" placeholder="000000" aria-label="6-digit code" />
            <button pButton type="submit" label="Verify and create account" [loading]="busy()" [disabled]="code.length !== 6"></button>
            <p class="alt">
              <button type="button" class="link-btn" (click)="resend()" [disabled]="cooldown() > 0 || busy()">
                Send a new code{{ cooldown() > 0 ? ' (' + cooldown() + 's)' : '' }}
              </button>
              ·
              <button type="button" class="link-btn" (click)="back()">Change my details</button>
            </p>
          </form>
        }
      </section>
    </div>
  `,
})
export class Register implements OnDestroy {
  mood = inject(AuthMood);
  shake = signal(false);
  private moodTimer: ReturnType<typeof setTimeout> | null = null;

  /** A password box has focus: the mattress pulls its blanket over itself. */
  focusIn(e: FocusEvent) {
    const t = e.target as HTMLElement | null;
    if (t instanceof HTMLInputElement && (t.type === 'password' || /pass|^np$|^cp$/i.test(t.id) || t.closest('p-password'))) this.mood.set('privacy');
  }
  focusOut() { if (this.mood.mood() === 'privacy') this.mood.reset(); }

  /** Shows the error, shakes the card and the mattress. */
  private fail(e: unknown) {
    this.error.set(errorMessage(e));
    this.mood.pulse('oops', 700);
    this.shake.set(true);
    setTimeout(() => this.shake.set(false), 550);
  }

  /** Lets the mattress celebrate for a moment, then carries on. */
  private celebrate(go: () => void) {
    this.mood.pulse('joy', 1100);
    this.moodTimer = setTimeout(go, 850);
  }
  private auth = inject(AuthService);
  private router = inject(Router);
  year = new Date().getFullYear();

  /** How strong the password looks: length, upper and lower case, digits, symbols. */
  strength() {
    const p = this.model.password ?? '';
    if (!p) return { score: 0, label: '' };
    if (p.length < 8) return { score: 0, label: 'Too short' };
    let score = 1;
    if (p.length >= 12) score++;
    if (/[a-z]/.test(p) && /[A-Z]/.test(p) && /\d/.test(p)) score++;
    if (/[^A-Za-z0-9]/.test(p)) score++;
    return { score: Math.min(score, 4), label: ['', 'Weak', 'Fair', 'Good', 'Strong'][Math.min(score, 4)] };
  }
  model = { businessName: '', name: '', email: '', password: '' };
  busy = signal(false);
  error = signal<string | null>(null);

  // step 2 (email code)
  challengeId = signal<string | null>(null);
  masked = signal('');
  code = '';
  cooldown = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;

  submit() {
    this.busy.set(true);
    this.error.set(null);
    this.auth.register(this.model).subscribe({
      next: r => {
        this.busy.set(false);
        if (r.otpRequired) {
          this.challengeId.set(r.challengeId);
          this.masked.set(r.maskedEmail ?? 'your email');
          this.code = '';
          this.startCooldown(30);
        } else this.done();
      },
      error: e => { this.fail(e); this.busy.set(false); },
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
    this.auth.verifyRegistration(this.challengeId()!, this.code).subscribe({
      next: () => this.done(),
      error: e => { this.fail(e); this.busy.set(false); this.code = ''; },
    });
  }

  resend() {
    if (!this.challengeId()) return;
    this.error.set(null);
    this.auth.resendOtp(this.challengeId()!).subscribe({
      next: r => { this.masked.set(r.maskedEmail); this.startCooldown(30); },
      error: e => this.fail(e),
    });
  }

  back() { this.stopTimer(); this.challengeId.set(null); this.code = ''; this.error.set(null); }

  private done() { this.celebrate(() => this.router.navigate(['/settings'], { queryParams: { welcome: 1 } })); }

  private startCooldown(seconds: number) {
    this.stopTimer();
    this.cooldown.set(seconds);
    this.timer = setInterval(() => {
      this.cooldown.update(v => Math.max(v - 1, 0));
      if (this.cooldown() === 0) this.stopTimer();
    }, 1000);
  }
  private stopTimer() { if (this.timer) { clearInterval(this.timer); this.timer = null; } }

  ngOnDestroy() { this.mood.reset(); if (this.moodTimer) clearTimeout(this.moodTimer); this.stopTimer(); }
}
