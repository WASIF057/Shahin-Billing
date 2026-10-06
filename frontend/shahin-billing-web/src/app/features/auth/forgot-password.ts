import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../core/auth.service';
import { errorMessage } from '../../core/errors';
import { Notify } from '../../core/notify.service';

/** Forgot password: 1) enter your email, 2) type the 6-digit code we emailed and choose a new password. */
@Component({
  selector: 'app-forgot-password',
  imports: [FormsModule, RouterLink, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  styleUrl: './auth.scss',
  template: `
    <div class="wrap">
      <section class="side">
        <div class="brand"><span class="mark" aria-hidden="true"></span> Shahin Billing</div>
        <div>
          <h1>Forgot your password?</h1>
          <p>No problem. We’ll email a 6-digit code to your business email, and you can choose a new password.</p>
        </div>
        <small>Your data stays private to your account.</small>
      </section>
      <section class="form-side">
        @if (step() === 1) {
          <form (ngSubmit)="send()" #f="ngForm">
            <h2>Reset your password</h2>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <div class="field">
              <label for="email">Your account email</label>
              <input pInputText id="email" name="email" type="email" [(ngModel)]="email" required autocomplete="username" />
            </div>
            <button pButton type="submit" label="Email me a code" [loading]="busy()" [disabled]="f.invalid"></button>
            <p class="alt"><a routerLink="/login">Back to log in</a></p>
          </form>
        } @else {
          <form (ngSubmit)="reset()" #g="ngForm">
            <h2>Choose a new password</h2>
            <p class="muted" style="margin: 0">If <strong>{{ email }}</strong> has an account, a 6-digit code was emailed to its business email. It works for 10 minutes.</p>
            @if (error()) { <p-message severity="error" [text]="error()!" /> }
            <input pInputText class="code" name="code" [(ngModel)]="code" maxlength="6" inputmode="numeric"
                   autocomplete="one-time-code" placeholder="000000" aria-label="6-digit code" required />
            <div class="field">
              <label for="np">New password</label>
              <p-password inputId="np" name="np" [(ngModel)]="newPassword" [feedback]="false" [toggleMask]="true" [fluid]="true"
                          required autocomplete="new-password" />
              <span class="hint">At least 8 characters.</span>
            </div>
            <div class="field">
              <label for="cp">Type it again</label>
              <p-password inputId="cp" name="cp" [(ngModel)]="confirm" [feedback]="false" [toggleMask]="true" [fluid]="true"
                          required autocomplete="new-password" />
              @if (confirm && confirm !== newPassword) { <span class="error">The two passwords don’t match.</span> }
            </div>
            <button pButton type="submit" label="Change password" [loading]="busy()"
                    [disabled]="g.invalid || code.length !== 6 || newPassword.length < 8 || newPassword !== confirm"></button>
            <p class="alt">
              <button type="button" class="link-btn" (click)="step.set(1)">Use a different email</button>
              ·
              <a routerLink="/login">Back to log in</a>
            </p>
          </form>
        }
      </section>
    </div>
  `,
})
export class ForgotPassword {
  private auth = inject(AuthService);
  private router = inject(Router);
  private notify = inject(Notify);

  step = signal<1 | 2>(1);
  busy = signal(false);
  error = signal<string | null>(null);
  email = '';
  code = '';
  newPassword = '';
  confirm = '';

  send() {
    this.busy.set(true);
    this.error.set(null);
    this.auth.forgotPassword(this.email).subscribe({
      next: () => { this.busy.set(false); this.step.set(2); },
      error: e => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }

  reset() {
    this.busy.set(true);
    this.error.set(null);
    this.auth.resetPassword(this.email, this.code, this.newPassword).subscribe({
      next: () => {
        this.notify.ok('Password changed. Log in with your new password.');
        this.router.navigate(['/login']);
      },
      error: e => { this.error.set(errorMessage(e)); this.busy.set(false); },
    });
  }
}
