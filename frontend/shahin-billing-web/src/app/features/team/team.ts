import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { TeamApi } from '../../core/api.service';
import { fieldErrors } from '../../core/errors';
import { TeamMember } from '../../core/models';
import { Notify } from '../../core/notify.service';

/** Team: the owner adds staff logins. Staff can bill and take payments, but not change settings or see reports. */
@Component({
  selector: 'app-team',
  imports: [FormsModule, DatePipe, ButtonModule, DialogModule, InputTextModule, PasswordModule, TableModule, TagModule,
            ToggleSwitchModule, TooltipModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Team</h1>
          <p>Give your staff their own logins. Switch a login off at any time and it stops working straight away.</p>
        </div>
        <div class="actions">
          <button pButton type="button" label="Add staff member" icon="pi pi-user-plus" (click)="open()"></button>
        </div>
      </div>

      <div class="panel info">
        <strong>What staff can do:</strong> create and edit bills, record payments, download PDFs, and add or edit clients.<br />
        <strong>What staff can’t do:</strong> see the dashboard or reports, change settings, items, bill formats or email templates,
        cancel bills, remove payments, delete anything, or manage the team.
      </div>

      <div class="panel">
        <p-table [value]="members()" [loading]="loading()" dataKey="id" [rowHover]="true" responsiveLayout="scroll">
          <ng-template #header>
            <tr><th>Name</th><th>Email</th><th>Role</th><th>Added</th><th>Active</th><th></th></tr>
          </ng-template>
          <ng-template #body let-m>
            <tr [class.off]="!m.isActive">
              <td class="nm">{{ m.name }}</td>
              <td>{{ m.email }}</td>
              <td><p-tag [severity]="m.role === 'Owner' ? 'success' : 'info'" [value]="m.role" [rounded]="true" /></td>
              <td class="muted small">{{ m.createdAt | date: 'dd-MM-yyyy' }}</td>
              <td>
                @if (m.role === 'Owner') { <span class="muted small">Always on</span> }
                @else {
                  <p-toggleswitch [ngModel]="m.isActive" (ngModelChange)="setActive(m, $event)"
                                  [pTooltip]="m.isActive ? 'On: click to switch off' : 'Off: click to switch on'" />
                }
              </td>
              <td class="row-actions">
                @if (m.role !== 'Owner') {
                  <button pButton type="button" icon="pi pi-key" [text]="true" pTooltip="Set a new password" (click)="openReset(m)" aria-label="Set a new password"></button>
                  <button pButton type="button" icon="pi pi-trash" [text]="true" severity="danger" pTooltip="Remove" (click)="remove(m)" aria-label="Remove"></button>
                }
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <p-dialog header="Add staff member" [visible]="addOpen()" (visibleChange)="addOpen.set($event)" [modal]="true"
              [style]="{ width: '460px' }" [breakpoints]="{ '520px': '96vw' }" [draggable]="false">
      <form (ngSubmit)="save()" id="staffForm" class="form">
        <div class="field">
          <label for="sname">Name</label>
          <input pInputText id="sname" name="name" [(ngModel)]="form.name" required />
          @if (errors()['name']) { <span class="error">{{ errors()['name'] }}</span> }
        </div>
        <div class="field">
          <label for="semail">Email</label>
          <input pInputText id="semail" name="email" type="email" [(ngModel)]="form.email" required autocomplete="off" />
          <span class="hint">Their login email. Login codes are sent here.</span>
          @if (errors()['email']) { <span class="error">{{ errors()['email'] }}</span> }
        </div>
        <div class="field">
          <label for="spass">Password</label>
          <p-password inputId="spass" name="password" [(ngModel)]="form.password" [feedback]="false" [toggleMask]="true" [fluid]="true" required autocomplete="new-password" />
          <span class="hint">At least 8 characters. Tell them to change it in My account.</span>
          @if (errors()['password']) { <span class="error">{{ errors()['password'] }}</span> }
        </div>
      </form>
      <ng-template #footer>
        <button pButton type="button" label="Cancel" [text]="true" (click)="addOpen.set(false)"></button>
        <button pButton type="submit" form="staffForm" label="Add staff member" icon="pi pi-check" [loading]="saving()"></button>
      </ng-template>
    </p-dialog>

    <p-dialog [header]="'New password for ' + (resetting()?.name ?? '')" [visible]="resetOpen()" (visibleChange)="resetOpen.set($event)"
              [modal]="true" [style]="{ width: '420px' }" [breakpoints]="{ '480px': '96vw' }" [draggable]="false">
      <div class="field">
        <label for="rpass">New password</label>
        <p-password inputId="rpass" [(ngModel)]="newPassword" [feedback]="false" [toggleMask]="true" [fluid]="true" autocomplete="new-password" />
        <span class="hint">At least 8 characters. Their open sessions end, so they log in again with this.</span>
      </div>
      <ng-template #footer>
        <button pButton type="button" label="Cancel" [text]="true" (click)="resetOpen.set(false)"></button>
        <button pButton type="button" label="Set password" icon="pi pi-check" [disabled]="newPassword.length < 8" (click)="reset()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .info { margin-bottom: 16px; line-height: 1.7; }
    .nm { font-weight: 700; }
    tr.off td:not(:nth-child(5)):not(:last-child) { opacity: 0.55; }
    .row-actions { text-align: right; white-space: nowrap; }
    .form { display: flex; flex-direction: column; gap: 14px; }
  `,
})
export class TeamPage implements OnInit {
  private api = inject(TeamApi);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);

  members = signal<TeamMember[]>([]);
  loading = signal(true);
  addOpen = signal(false);
  saving = signal(false);
  errors = signal<Record<string, string>>({});
  form = { name: '', email: '', password: '' };

  resetOpen = signal(false);
  resetting = signal<TeamMember | null>(null);
  newPassword = '';

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: l => { this.members.set(l); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  open() { this.form = { name: '', email: '', password: '' }; this.errors.set({}); this.addOpen.set(true); }

  save() {
    this.saving.set(true);
    this.errors.set({});
    this.api.create(this.form).subscribe({
      next: () => { this.saving.set(false); this.addOpen.set(false); this.notify.ok('Staff member added'); this.load(); },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  setActive(m: TeamMember, on: boolean) {
    this.api.setActive(m.id, on).subscribe({
      next: () => { this.notify.ok(on ? `${m.name} can log in again` : `${m.name} is switched off`); this.load(); },
      error: e => { this.notify.error(e); this.load(); },
    });
  }

  openReset(m: TeamMember) { this.resetting.set(m); this.newPassword = ''; this.resetOpen.set(true); }

  reset() {
    const m = this.resetting();
    if (!m) return;
    this.api.resetPassword(m.id, this.newPassword).subscribe({
      next: () => { this.resetOpen.set(false); this.notify.ok(`New password set for ${m.name}`); },
      error: e => this.notify.error(e),
    });
  }

  remove(m: TeamMember) {
    this.confirm.confirm({
      header: 'Remove staff member?',
      message: `Remove ${m.name}? Their login stops working. Bills they made stay as they are. To pause them instead, switch them off.`,
      acceptLabel: 'Remove', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(m.id).subscribe({
        next: () => { this.notify.ok('Staff member removed'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }
}
