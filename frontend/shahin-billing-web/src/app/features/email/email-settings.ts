import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { EmailApi } from '../../core/api.service';
import { fieldErrors } from '../../core/errors';
import { EmailTemplate, ReminderSettings } from '../../core/models';
import { Notify } from '../../core/notify.service';

type TextKey = 'subject' | 'body';

const TRIGGERS = [
  { key: 'Generated', label: 'A bill is generated (finalized)' },
  { key: 'Downloaded', label: 'A bill PDF is downloaded' },
  { key: 'PaymentChanged', label: 'A payment status changes (Paid, Part paid, Unpaid)' },
  { key: 'Reminder', label: 'A payment reminder is sent (by the button on a bill, or automatically)' },
  { key: 'OrderPlaced', label: 'A client places an order on the ordering website' },
  { key: 'OrderAccepted', label: 'Owner or staff accept a client\u2019s order' },
  { key: 'OrderCancelled', label: 'Owner or staff cancel a client\u2019s order' },
];
const TRIGGER_SHORT: Record<string, string> = { Generated: 'Bill generated', Downloaded: 'PDF downloaded', PaymentChanged: 'Payment status changed', Reminder: 'Payment reminder', OrderPlaced: 'Order placed', OrderAccepted: 'Order accepted', OrderCancelled: 'Order cancelled' };

/** Standard formats for "Create email template": one for the client and one for you, for each kind of email. */
const STANDARD: Record<string, EmailTemplate> = {
  billClient: {
    id: '', name: 'Bill email to client', recipient: 'Client', triggers: ['Generated', 'Downloaded'], attachPdf: true, isActive: true,
    subject: 'Invoice {{InvoiceNumber}} from {{BusinessName}}',
    body: 'Dear {{ClientName}},\n\nThank you for your business. Please find your invoice attached.\n\n' +
          'Invoice No.: {{InvoiceNumber}}\nDate: {{InvoiceDate}}\nAmount: {{GrandTotal}}\n\n' +
          'For any questions, call us on {{BusinessPhone}}.\n\nRegards,\n{{BusinessName}}',
  },
  billSelf: {
    id: '', name: 'Bill email to me', recipient: 'Business', triggers: ['Generated', 'Downloaded'], attachPdf: true, isActive: true,
    subject: 'Invoice {{InvoiceNumber}} {{Event}} for {{ClientName}}',
    body: 'Invoice {{InvoiceNumber}} was {{Event}}.\n\nClient: {{ClientName}} ({{ClientCity}})\nDate: {{InvoiceDate}} {{Time}}\n' +
          'Amount: {{GrandTotal}}\n{{AmountInWords}}',
  },
  paymentClient: {
    id: '', name: 'Payment update to client', recipient: 'Client', triggers: ['PaymentChanged'], attachPdf: false, isActive: true,
    subject: 'Payment update for invoice {{InvoiceNumber}}: {{PaymentStatus}}',
    body: 'Dear {{ClientName}},\n\nThe payment status of your invoice {{InvoiceNumber}} is now {{PaymentStatus}}.\n\n' +
          'Invoice amount: {{GrandTotal}}\nAmount received: {{AmountPaid}}\nBalance due: {{BalanceDue}}\n\n' +
          'Thank you for your payment.\n\nRegards,\n{{BusinessName}}\n{{BusinessPhone}}',
  },
  paymentSelf: {
    id: '', name: 'Payment update to me', recipient: 'Business', triggers: ['PaymentChanged'], attachPdf: false, isActive: true,
    subject: 'Payment {{PaymentStatus}}: {{InvoiceNumber}} ({{ClientName}})',
    body: 'Invoice {{InvoiceNumber}} for {{ClientName}} ({{ClientCity}}) changed from {{PreviousStatus}} to {{PaymentStatus}}.\n\n' +
          'Invoice amount: {{GrandTotal}}\nAmount received so far: {{AmountPaid}}\nBalance due: {{BalanceDue}}\n\n' +
          'Last payment: {{PaymentAmount}} by {{PaymentMode}} on {{PaymentDate}}',
  },
};
const starter = (): EmailTemplate => structuredClone(STANDARD['billClient']);

/** Sample values, so View can show what an email will look like. */
const SAMPLE: Record<string, string> = {
  InvoiceNumber: 'No-0001', InvoiceDate: '03-10-2026', Time: '11:57 AM', ClientName: 'Sathyanatha Cloth Store', ClientCity: 'Koppa',
  BusinessName: 'Shahin Enterprises', BusinessPhone: '98458 45109', GrandTotal: 'Rs. 12,345.00',
  AmountInWords: 'Rupees Twelve Thousand Three Hundred Forty-Five Only', Event: 'generated',
  PaymentStatus: 'Part paid', PreviousStatus: 'Unpaid', AmountPaid: 'Rs. 5,000.00', BalanceDue: 'Rs. 7,345.00',
  PaymentAmount: 'Rs. 5,000.00', PaymentMode: 'UPI', PaymentDate: '03-10-2026', DaysOutstanding: '18',
  OrderPriority: 'URGENT', OrderSource: 'Phone call', OrderCancelReason: 'Out of stock', OrderNumber: 'ORD-0007', OrderDate: '06-10-2026 11:30 AM', OrderNote: 'Deliver Monday',
  OrderItems: '- Bed \u2014 Box \u00b7 Cotton \u00b7 6ft x 2 PCS\n- Pillow \u2014 Medium x 10 PCS',
};
const fill = (text: string) => text.replace(/\{\{\s*(\w+)\s*\}\}/g, (_, k: string) => SAMPLE[k] ?? '');

/** Email tab: a list of named email templates, each with View, Edit, Delete and an Active switch. */
@Component({
  selector: 'app-email-settings',
  imports: [FormsModule, ButtonModule, CheckboxModule, DialogModule, InputTextModule, InputNumberModule, SelectModule, TableModule, TagModule,
            TextareaModule, ToggleSwitchModule, TooltipModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Email templates</h1>
          <p>Emails the app sends by itself. Each template says who gets it and when. Switch one off to stop it without deleting it.</p>
        </div>
        <div class="actions">
          <button pButton type="button" label="Create email template" icon="pi pi-plus" (click)="open()"></button>
        </div>
      </div>

      @if (!smtpConfigured()) {
        <div class="panel notice">
          <strong>Email isn’t connected yet.</strong>
          Add your mail server under <code>Smtp</code> in <code>appsettings.Development.json</code>, then restart the API.
          Your templates are saved either way.
        </div>
      }

      <section class="panel rem">
        <h2>Automatic payment reminders</h2>
        <p class="muted small">
          Emails clients who still owe money, using the templates set to “Payment reminder”. It is <strong>off</strong> until you switch it on, and it only sends
          between 9 am and 7 pm. You can also send one any time from <strong>All bills</strong> (⋮ menu → Send payment reminder).
        </p>
        @if (rem(); as r) {
          <label class="row"><p-toggleswitch [(ngModel)]="r.enabled" name="remOn" /> <span>Send reminders automatically</span></label>
          <div class="rem-grid" [class.dim]="!r.enabled">
            <div class="field">
              <label for="rafter">First reminder after</label>
              <p-inputnumber inputId="rafter" [(ngModel)]="r.afterDays" [min]="1" [max]="365" suffix=" days" [showButtons]="true" [fluid]="true" />
              <span class="hint">days since the bill date</span>
            </div>
            <div class="field">
              <label for="rrep">Then every</label>
              <p-inputnumber inputId="rrep" [(ngModel)]="r.repeatEveryDays" [min]="1" [max]="90" suffix=" days" [showButtons]="true" [fluid]="true" />
            </div>
            <div class="field">
              <label for="rmax">At most</label>
              <p-inputnumber inputId="rmax" [(ngModel)]="r.maxReminders" [min]="1" [max]="10" suffix=" reminders" [showButtons]="true" [fluid]="true" />
              <span class="hint">per bill</span>
            </div>
          </div>
          <div><button pButton type="button" label="Save reminder settings" icon="pi pi-check" [outlined]="true" [loading]="savingRem()" (click)="saveReminders()"></button></div>
        }
      </section>

      <div class="panel">
        <p-table [value]="templates()" [loading]="loading()" dataKey="id" [rowHover]="true" responsiveLayout="scroll">
          <ng-template #header>
            <tr><th>Name</th><th>Sent to</th><th>Sent when</th><th>Subject</th><th>Active</th><th></th></tr>
          </ng-template>
          <ng-template #body let-t>
            <tr [class.inactive]="!t.isActive">
              <td class="tname">{{ t.name }}</td>
              <td><p-tag [severity]="t.recipient === 'Client' ? 'info' : 'secondary'" [value]="t.recipient === 'Client' ? 'Client' : 'Me (business)'" [rounded]="true" /></td>
              <td>
                <div class="chips">
                  @for (g of t.triggers; track g) { <span class="chip">{{ short(g) }}</span> }
                </div>
              </td>
              <td class="subj">{{ t.subject }}</td>
              <td>
                <p-toggleswitch [ngModel]="t.isActive" (ngModelChange)="setActive(t, $event)" [pTooltip]="t.isActive ? 'Active: click to switch off' : 'Inactive: click to switch on'" />
              </td>
              <td class="row-actions">
                <button pButton type="button" icon="pi pi-eye" [text]="true" pTooltip="View" (click)="view(t)" aria-label="View"></button>
                <button pButton type="button" icon="pi pi-pencil" [text]="true" pTooltip="Edit" (click)="open(t)" aria-label="Edit"></button>
                <button pButton type="button" icon="pi pi-send" [text]="true" pTooltip="Send me a test" [disabled]="!smtpConfigured()" (click)="test(t)" aria-label="Send test"></button>
                <button pButton type="button" icon="pi pi-trash" [text]="true" severity="danger" pTooltip="Delete" (click)="remove(t)" aria-label="Delete"></button>
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="6">
              <div class="empty">
                <h3>No email templates</h3>
                <p>Create one to email your client or yourself when a bill is generated, downloaded, or its payment status changes.</p>
                <button pButton type="button" label="Create email template" icon="pi pi-plus" (click)="open()"></button>
              </div>
            </td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <!-- View -->
    <p-dialog [header]="viewing()?.name ?? ''" [visible]="viewOpen()" (visibleChange)="viewOpen.set($event)"
              [modal]="true" [style]="{ width: '620px' }" [breakpoints]="{ '700px': '96vw' }" [draggable]="false">
      @if (viewing(); as v) {
        <div class="facts">
          <div><span class="muted small">Sent to</span><br /><strong>{{ v.recipient === 'Client' ? 'The client' : 'Me (business email)' }}</strong></div>
          <div><span class="muted small">Sent when</span><br /><strong>{{ triggerText(v) }}</strong></div>
          <div><span class="muted small">PDF attached</span><br /><strong>{{ v.attachPdf ? 'Yes' : 'No' }}</strong></div>
          <div><span class="muted small">Status</span><br /><strong>{{ v.isActive ? 'Active' : 'Inactive' }}</strong></div>
        </div>
        <p class="muted small" style="margin: 14px 0 6px">Preview with sample values</p>
        <div class="mail">
          <div class="mail-subject">{{ fill(v.subject) }}</div>
          <div class="mail-body">{{ fill(v.body) }}</div>
        </div>
      }
      <ng-template #footer>
        <button pButton type="button" label="Close" [text]="true" (click)="viewOpen.set(false)"></button>
        <button pButton type="button" label="Edit" icon="pi pi-pencil" (click)="editFromView()"></button>
      </ng-template>
    </p-dialog>

    <!-- Create / edit -->
    <p-dialog [header]="editing().id ? 'Edit email template' : 'Create email template'" [visible]="dialogOpen()"
              (visibleChange)="dialogOpen.set($event)" [modal]="true" [style]="{ width: '720px' }"
              [breakpoints]="{ '780px': '96vw' }" [draggable]="false">
      @let t = editing();
      <form (ngSubmit)="save()" id="emailForm" class="form">
        @if (!t.id) {
          <div class="field">
            <label for="estd">Start from a standard format</label>
            <p-select inputId="estd" name="standard" [options]="standards" optionLabel="label" optionValue="value"
                      [ngModel]="standard" (ngModelChange)="applyStandard($event)" appendTo="body" [fluid]="true" />
            <span class="hint">Ready-made wording. Pick the one for the client or the one for you, then change anything you like below.</span>
          </div>
        }
        <div class="two">
          <div class="field">
            <label for="ename">Template name</label>
            <input pInputText id="ename" name="name" [(ngModel)]="t.name" required placeholder="e.g. Bill email to client" />
            @if (errors()['name']) { <span class="error">{{ errors()['name'] }}</span> }
          </div>
          <div class="field">
            <label for="erec">Send to</label>
            <p-select inputId="erec" name="recipient" [options]="recipientOptions" optionLabel="label" optionValue="value"
                      [(ngModel)]="t.recipient" appendTo="body" [fluid]="true" />
            <span class="hint">Client emails need an email address saved on the client.</span>
          </div>
        </div>

        <div class="field">
          <label>Send when</label>
          <div class="checks">
            @for (g of triggers; track g.key) {
              <label class="check-row"><p-checkbox [binary]="true" [ngModel]="t.triggers.includes(g.key)" (ngModelChange)="toggleTrigger(t, g.key, $event)" [name]="'trg' + g.key" /> {{ g.label }}</label>
            }
          </div>
          @if (errors()['triggers']) { <span class="error">{{ errors()['triggers'] }}</span> }
        </div>

        <div class="field">
          <label for="esub">Subject</label>
          <input pInputText id="esub" name="subject" [(ngModel)]="t.subject" (focus)="active = 'subject'" />
          @if (errors()['subject']) { <span class="error">{{ errors()['subject'] }}</span> }
        </div>
        <div class="field">
          <label for="ebody">Message</label>
          <textarea pTextarea id="ebody" name="body" rows="10" [(ngModel)]="t.body" (focus)="active = 'body'"></textarea>
          @if (errors()['body']) { <span class="error">{{ errors()['body'] }}</span> }
        </div>

        <div class="field">
          <label>Placeholders <span class="muted small">(click to add to the box you last clicked in)</span></label>
          <div class="chips">
            @for (p of placeholders; track p.key) {
              <button type="button" class="chip pick" (click)="insert(p.key)" [title]="p.hint">{{ '{' + '{' + p.key + '}' + '}' }}</button>
            }
          </div>
        </div>

        <div class="toggles">
          <label class="check-row"><p-toggleswitch [(ngModel)]="t.attachPdf" name="attach" /> Attach the bill PDF</label>
          <label class="check-row"><p-toggleswitch [(ngModel)]="t.isActive" name="isActive" /> Active</label>
        </div>
      </form>
      <ng-template #footer>
        <button pButton type="button" label="Cancel" [text]="true" (click)="dialogOpen.set(false)"></button>
        <button pButton type="submit" form="emailForm" [label]="t.id ? 'Save template' : 'Create template'" icon="pi pi-check" [loading]="saving()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .rem { margin-bottom: 16px; display: flex; flex-direction: column; gap: 12px; }
    .rem p { margin: 0; }
    .rem-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 14px; }
    .rem-grid.dim { opacity: 0.55; }
    @media (max-width: 760px) { .rem-grid { grid-template-columns: 1fr; } }
    .notice { background: #fff8e6; border-color: #f1d28a; margin-bottom: 16px; }
    .tname { font-weight: 700; }
    .subj { max-width: 280px; color: var(--muted); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    tr.inactive td:not(:nth-child(5)):not(:last-child) { opacity: 0.55; }
    .row-actions { text-align: right; white-space: nowrap; }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .chip { background: var(--navy-soft); color: var(--navy); border-radius: 999px; padding: 2px 10px; font-size: 0.78rem; font-weight: 600; border: 0; }
    .chip.pick { cursor: pointer; font-family: monospace; font-weight: 500; background: #fff; border: 1px solid var(--line); color: var(--ink); }
    .chip.pick:hover { background: var(--navy-soft); }
    .form { display: flex; flex-direction: column; gap: 16px; }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
    .checks { display: flex; flex-direction: column; gap: 8px; }
    .toggles { display: flex; gap: 28px; flex-wrap: wrap; }
    .facts { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px 20px; }
    .mail { border: 1px solid var(--line); border-radius: 12px; overflow: hidden; }
    .mail-subject { padding: 10px 14px; font-weight: 700; background: #f6f9fd; border-bottom: 1px solid var(--line); }
    .mail-body { padding: 14px; white-space: pre-wrap; line-height: 1.55; }
    @media (max-width: 700px) { .two { grid-template-columns: 1fr; } }
  `,
})
export class EmailSettingsPage implements OnInit {
  private api = inject(EmailApi);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);

  templates = signal<EmailTemplate[]>([]);
  smtpConfigured = signal(true);
  loading = signal(true);

  dialogOpen = signal(false);
  editing = signal<EmailTemplate>(starter());
  saving = signal(false);
  errors = signal<Record<string, string>>({});
  active: TextKey = 'body';

  viewOpen = signal(false);
  viewing = signal<EmailTemplate | null>(null);

  triggers = TRIGGERS;
  recipientOptions = [{ label: 'The client', value: 'Client' }, { label: 'Me (my business email)', value: 'Business' }];
  fill = fill;
  standard = 'billClient';
  standards = [
    { label: 'Bill email to client', value: 'billClient' },
    { label: 'Bill email to me', value: 'billSelf' },
    { label: 'Payment update to client', value: 'paymentClient' },
    { label: 'Payment update to me', value: 'paymentSelf' },
  ];

  placeholders = [
    { key: 'InvoiceNumber', hint: 'e.g. No-0001' },
    { key: 'InvoiceDate', hint: 'dd-MM-yyyy' },
    { key: 'Time', hint: 'Time of sale' },
    { key: 'ClientName', hint: 'Client name' },
    { key: 'ClientCity', hint: 'City picked on the bill' },
    { key: 'BusinessName', hint: 'Your business name' },
    { key: 'BusinessPhone', hint: 'Your phone number' },
    { key: 'GrandTotal', hint: 'e.g. Rs. 81,774.00' },
    { key: 'AmountInWords', hint: 'Grand total in words' },
    { key: 'Event', hint: '"generated", "downloaded" or "payment updated"' },
    { key: 'PaymentStatus', hint: 'Paid, Part paid or Unpaid' },
    { key: 'PreviousStatus', hint: 'The payment status before' },
    { key: 'AmountPaid', hint: 'Total received so far' },
    { key: 'BalanceDue', hint: 'Amount still due' },
    { key: 'PaymentAmount', hint: 'The last payment amount' },
    { key: 'PaymentMode', hint: 'Cash, UPI, etc.' },
    { key: 'PaymentDate', hint: 'Date of the last payment' },
    { key: 'DaysOutstanding', hint: 'Days since the bill date (best for reminders)' },
    { key: 'OrderNumber', hint: 'Order emails: e.g. ORD-0007' },
    { key: 'OrderDate', hint: 'Order emails: when it was placed' },
    { key: 'OrderItems', hint: 'Order emails: the products and quantities, one per line (no prices)' },
    { key: 'OrderNote', hint: 'Order emails: the client\u2019s note' },
    { key: 'OrderPriority', hint: 'Order emails: Normal, High or URGENT' },
    { key: 'OrderSource', hint: 'Order emails: Phone call or Ordering website' },
    { key: 'OrderCancelReason', hint: 'Cancelled-order emails: the reason you gave' },
  ];

  rem = signal<ReminderSettings | null>(null);
  savingRem = signal(false);

  saveReminders() {
    this.savingRem.set(true);
    this.api.saveReminders(this.rem()!).subscribe({
      next: r => { this.rem.set(r); this.savingRem.set(false); this.notify.ok(r.enabled ? 'Automatic reminders are on.' : 'Automatic reminders are off.'); },
      error: e => { this.notify.error(e); this.savingRem.set(false); },
    });
  }

  ngOnInit() {
    this.api.reminders().subscribe({ next: r => this.rem.set(r), error: () => {} });
    this.load();
    this.api.status().subscribe({ next: s => this.smtpConfigured.set(s.smtpConfigured), error: () => {} });
  }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: list => { this.templates.set(list); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  short(g: string) { return TRIGGER_SHORT[g] ?? g; }
  triggerText(t: EmailTemplate) { return t.triggers.map(g => this.short(g)).join(', '); }

  // ---- view ----
  view(t: EmailTemplate) { this.viewing.set(t); this.viewOpen.set(true); }
  editFromView() { const t = this.viewing(); this.viewOpen.set(false); if (t) this.open(t); }

  // ---- create / edit ----
  open(t?: EmailTemplate) {
    this.errors.set({});
    this.active = 'body';
    this.standard = 'billClient';
    this.editing.set(t ? structuredClone(t) : starter());
    this.dialogOpen.set(true);
  }

  /** Fills the form from a standard format (only used while creating a new template). */
  applyStandard(key: string) {
    this.standard = key;
    const std = STANDARD[key];
    const t = this.editing();
    const wasStandardName = !t.name.trim() || Object.values(STANDARD).some(x => x.name === t.name);
    Object.assign(t, structuredClone({ ...std, id: t.id, name: wasStandardName ? std.name : t.name }));
    this.editing.set({ ...t });
  }

  toggleTrigger(t: EmailTemplate, key: string, on: boolean) {
    t.triggers = on ? [...new Set([...t.triggers, key])] : t.triggers.filter(x => x !== key);
  }

  insert(key: string) {
    const t = this.editing();
    t[this.active] += `{{${key}}}`;
  }

  save() {
    const t = this.editing();
    this.saving.set(true);
    this.errors.set({});
    const req = t.id ? this.api.update(t) : this.api.create(t);
    req.subscribe({
      next: () => {
        this.notify.ok(t.id ? 'Template saved' : 'Template created');
        this.dialogOpen.set(false);
        this.saving.set(false);
        this.load();
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  // ---- active / test / delete ----
  setActive(t: EmailTemplate, on: boolean) {
    this.api.setActive(t.id, on).subscribe({
      next: () => { t.isActive = on; this.templates.update(l => [...l]); this.notify.ok(on ? `${t.name} is active` : `${t.name} is switched off`); },
      error: e => { this.notify.error(e); this.load(); },
    });
  }

  test(t: EmailTemplate) {
    this.api.test(t.id).subscribe({ next: r => this.notify.ok(r.message), error: e => this.notify.error(e) });
  }

  remove(t: EmailTemplate) {
    this.confirm.confirm({
      header: 'Delete template?',
      message: `Delete “${t.name}”? Emails of this kind will stop being sent. To pause it instead, switch it off.`,
      acceptLabel: 'Delete template', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(t.id).subscribe({
        next: () => { this.notify.ok('Template deleted'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }
}
