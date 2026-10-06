import { Component, inject, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  styleUrl: './auth.scss',
  template: `
    <div class="wrap">
      <section class="side">
        <div class="brand"><span class="mark" aria-hidden="true"></span> Shahin Billing</div>
        <div>
          <h1>Set up once, bill every day.</h1>
          <p>After signing up, add your business details, your items with their rates, and your clients. Then every bill is a few clicks.</p>
          <ul class="perks">
            <li><i class="pi pi-bolt"></i> Rates and GST fill in automatically</li>
            <li><i class="pi pi-file-pdf"></i> Professional PDF tax invoices</li>
            <li><i class="pi pi-envelope"></i> Bills emailed to you and your client</li>
          </ul>
        </div>
        <small>Your data stays private to your account.</small>
      </section>
      <section class="form-side">
        @if (!challengeId()) {
          <form (ngSubmit)="submit()" #f="ngForm">
            <h2>Create your account</h2>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <div class="field">
              <label for="biz">Business name</label>
              <input pInputText id="biz" name="biz" [(ngModel)]="model.businessName" required placeholder="Shahin Enterprises" />
            </div>
            <div class="field">
              <label for="name">Your name</label>
              <input pInputText id="name" name="name" [(ngModel)]="model.name" required autocomplete="name" />
            </div>
            <div class="field">
              <label for="email">Email</label>
              <input pInputText id="email" name="email" type="email" [(ngModel)]="model.email" required autocomplete="email" />
              <span class="hint">We’ll email a 6-digit code to confirm it.</span>
            </div>
            <div class="field">
              <label for="password">Password</label>
              <p-password inputId="password" name="password" [(ngModel)]="model.password" [toggleMask]="true" [fluid]="true"
                          required [minlength]="8" autocomplete="new-password" />
              <span class="hint">At least 8 characters.</span>
            </div>
            <button pButton type="submit" label="Create account" [loading]="busy()" [disabled]="f.invalid"></button>
            <p class="alt">Already have an account? <a routerLink="/login">Log in</a></p>
          </form>
        } @else {
          <!-- Step 2: confirm the email with the code we sent. The account is created only now. -->
          <form (ngSubmit)="verify()">
            <h2>Confirm your email</h2>
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
  private auth = inject(AuthService);
  private router = inject(Router);
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
    this.auth.verifyRegistration(this.challengeId()!, this.code).subscribe({
      next: () => this.done(),
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

  back() { this.stopTimer(); this.challengeId.set(null); this.code = ''; this.error.set(null); }

  private done() { this.router.navigate(['/settings'], { queryParams: { welcome: 1 } }); }

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
