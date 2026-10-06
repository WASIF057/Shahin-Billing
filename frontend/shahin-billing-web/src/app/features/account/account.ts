import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { PasswordModule } from 'primeng/password';
import { TagModule } from 'primeng/tag';
import { AuthService } from '../../core/auth.service';
import { Notify } from '../../core/notify.service';

/** My account: for every login (owner or staff). Change your password, or end your sessions on all devices. */
@Component({
  selector: 'app-account',
  imports: [FormsModule, ButtonModule, PasswordModule, TagModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>My account</h1>
          <p>Your own login details and security.</p>
        </div>
      </div>

      <section class="panel">
        <h2>You</h2>
        <div class="who">
          <strong>{{ auth.user()?.name }}</strong>
          <span class="muted">{{ auth.user()?.email }}</span>
          <p-tag [severity]="auth.isOwner() ? 'success' : 'info'" [value]="auth.user()?.role ?? ''" [rounded]="true" />
        </div>
      </section>

      <section class="panel">
        <h2>Change password</h2>
        <div class="grid">
          <div class="field">
            <label for="cur">Current password</label>
            <p-password inputId="cur" [(ngModel)]="pw.current" [feedback]="false" [toggleMask]="true" [fluid]="true" autocomplete="current-password" />
          </div>
          <div class="field">
            <label for="new">New password</label>
            <p-password inputId="new" [(ngModel)]="pw.next" [feedback]="false" [toggleMask]="true" [fluid]="true" autocomplete="new-password" />
            <span class="hint">At least 8 characters. You’ll be logged out everywhere and log in again with the new one.</span>
          </div>
        </div>
        <button pButton type="button" label="Change password" [outlined]="true" [disabled]="!pw.current || pw.next.length < 8" (click)="changePassword()"></button>
      </section>

      <section class="panel">
        <h2>Log out of all devices</h2>
        <p class="muted small">Ends every session of yours on every phone and computer, including this one. Use it if you left yourself logged in somewhere or lost a device.</p>
        <button pButton type="button" label="Log out of all devices" icon="pi pi-sign-out" severity="danger" [outlined]="true" (click)="logoutAll()"></button>
      </section>
    </div>
  `,
  styles: `
    .panel + .panel { margin-top: 16px; }
    .who { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 14px; }
    @media (max-width: 760px) { .grid { grid-template-columns: 1fr; } }
  `,
})
export class AccountPage {
  auth = inject(AuthService);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);
  pw = { current: '', next: '' };

  changePassword() {
    this.auth.changePassword(this.pw.current, this.pw.next).subscribe({
      next: () => { this.notify.ok('Password changed. Please log in again.'); this.auth.logout(); },
      error: e => this.notify.error(e),
    });
  }

  logoutAll() {
    this.confirm.confirm({
      header: 'Log out of all devices?',
      message: 'You will be logged out here too and need to log in again.',
      acceptLabel: 'Log out everywhere', rejectLabel: 'Cancel',
      accept: () => this.auth.logoutAll().subscribe({
        next: () => { this.notify.ok('Logged out of all devices'); this.auth.logout(); },
        error: e => this.notify.error(e),
      }),
    });
  }
}
