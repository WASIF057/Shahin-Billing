import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ClientsApi, MiscApi, saveBlob } from '../../core/api.service';
import { Client } from '../../core/models';
import { toIsoDate } from '../../core/gst';
import { Notify } from '../../core/notify.service';

@Component({
  selector: 'app-reports',
  imports: [FormsModule, DatePipe, ButtonModule, DatePickerModule, SelectModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Reports</h1>
          <p>Excel files for GST filing and your own records. Cancelled bills are left out of GST reports.</p>
        </div>
      </div>

      <div class="cards">
        <section class="panel">
          <h2>GSTR-1 for a month</h2>
          <p class="muted small">B2B invoice list and HSN summary in one Excel file. Hand it to your accountant, or use it to fill GSTR-1.</p>
          <div class="field">
            <label for="m">Month</label>
            <p-datepicker inputId="m" [(ngModel)]="month" view="month" dateFormat="MM yy" [fluid]="true" appendTo="body" />
          </div>
          <button pButton type="button" label="Download GSTR-1 Excel" icon="pi pi-file-excel" [loading]="busy() === 'gstr'"
                  (click)="gstr1()"></button>
        </section>

        <section class="panel">
          <h2>Sales register</h2>
          <p class="muted small">Every bill in a date range with tax, payments and status.</p>
          <div class="two">
            <div class="field">
              <label for="f">From</label>
              <p-datepicker inputId="f" [(ngModel)]="from" dateFormat="dd-mm-yy" [fluid]="true" appendTo="body" />
            </div>
            <div class="field">
              <label for="t">To</label>
              <p-datepicker inputId="t" [(ngModel)]="to" dateFormat="dd-mm-yy" [fluid]="true" appendTo="body" />
            </div>
          </div>
          <button pButton type="button" label="Download sales Excel" icon="pi pi-file-excel" [loading]="busy() === 'sales'"
                  (click)="sales()"></button>
        </section>

        <section class="panel">
          <h2>Client report</h2>
          <p class="muted small">All finalized bills for one client in a date range, with totals. If the client has several cities, pick one or see them all.</p>
          <div class="field">
            <label for="rc">Client</label>
            <p-select inputId="rc" [options]="clients()" optionLabel="name" optionValue="id" [(ngModel)]="clientId"
                      (onChange)="city = ''" [filter]="true" filterBy="name" placeholder="Select a client" [fluid]="true" appendTo="body" />
          </div>
          @if (cityOptions().length) {
            <div class="field">
              <label for="rcity">City</label>
              <p-select inputId="rcity" [options]="cityOptions()" optionLabel="label" optionValue="value" [(ngModel)]="city"
                        [fluid]="true" appendTo="body" />
            </div>
          }
          <div class="two">
            <div class="field">
              <label for="rcf">From</label>
              <p-datepicker inputId="rcf" [(ngModel)]="cFrom" dateFormat="dd-mm-yy" [fluid]="true" appendTo="body" />
            </div>
            <div class="field">
              <label for="rct">To</label>
              <p-datepicker inputId="rct" [(ngModel)]="cTo" dateFormat="dd-mm-yy" [fluid]="true" appendTo="body" />
            </div>
          </div>
          <div class="btns">
            <button pButton type="button" label="Excel report" icon="pi pi-file-excel" [loading]="busy() === 'client'"
                    [disabled]="!clientId" (click)="clientReport()"></button>
            <button pButton type="button" label="Statement (PDF)" icon="pi pi-file-pdf" [outlined]="true" [loading]="busy() === 'statement'"
                    [disabled]="!clientId" (click)="clientStatement()"></button>
          </div>
          <p class="muted small">The PDF statement lists the bills, what was received and the balance due. You can send it to the client.</p>
        </section>

        <section class="panel">
          <h2>Backup</h2>
          <p class="muted small">All your business details, items, clients and bills in one file. Keep a copy somewhere safe every month.</p>
          @if (backupInfo(); as b) {
            <p class="muted small">
              @if (b.enabled) {
                <i class="pi pi-check-circle" style="color: var(--good)"></i>
                A backup is also saved automatically every day on the server (the last {{ b.keep }} are kept).
                {{ b.lastBackupAt ? 'Last one: ' + (b.lastBackupAt | date: 'dd-MM-yyyy hh:mm a') + '.' : 'The first one is made shortly after the app starts.' }}
              } @else { Automatic backups are switched off on this server. }
            </p>
          }
          <button pButton type="button" label="Download backup" icon="pi pi-cloud-download" [outlined]="true"
                  [loading]="busy() === 'backup'" (click)="backup()"></button>
        </section>
      </div>
    </div>
  `,
  styles: `
    .cards { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; align-items: start; }
    .cards .panel + .panel { margin-top: 0; }
    .panel { display: flex; flex-direction: column; gap: 14px; }
    .panel > h2 { margin-bottom: 0; }
    .panel p { margin: 0; }
    .btns { display: flex; gap: 10px; flex-wrap: wrap; }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    @media (max-width: 1000px) { .cards { grid-template-columns: 1fr; } }
  `,
})
export class ReportsPage implements OnInit {
  private api = inject(MiscApi);
  private clientsApi = inject(ClientsApi);
  private notify = inject(Notify);
  busy = signal<'' | 'gstr' | 'sales' | 'client' | 'statement' | 'backup'>('');

  // client report
  clients = signal<Client[]>([]);
  clientId: string | null = null;
  city = '';
  cFrom = (() => { const n = new Date(); return new Date(n.getMonth() >= 3 ? n.getFullYear() : n.getFullYear() - 1, 3, 1); })();   // 1 April: start of this financial year
  cTo = new Date();

  backupInfo = signal<{ enabled: boolean; lastBackupAt: string | null; keep: number } | null>(null);

  ngOnInit() {
    this.clientsApi.list().subscribe(c => this.clients.set(c));
    this.api.backupStatus().subscribe({ next: s => this.backupInfo.set(s), error: () => {} });
  }

  /** City choices appear only for a client that has more than one city. */
  cityOptions() {
    const c = this.clients().find(x => x.id === this.clientId);
    return c && (c.cities?.length ?? 0) > 1
      ? [{ label: 'All cities', value: '' }, ...c.cities.map(x => ({ label: x, value: x }))]
      : [];
  }

  month = new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1);   // last month: what you usually file
  from = new Date(new Date().getFullYear(), new Date().getMonth(), 1);
  to = new Date();

  gstr1() {
    const m = `${this.month.getFullYear()}-${String(this.month.getMonth() + 1).padStart(2, '0')}`;
    this.run('gstr', this.api.gstr1(m), `GSTR1-${m}.xlsx`);
  }

  sales() {
    this.run('sales', this.api.sales(toIsoDate(this.from)!, toIsoDate(this.to)!), `Sales-${toIsoDate(this.from)}-to-${toIsoDate(this.to)}.xlsx`);
  }

  clientReport() {
    const c = this.clients().find(x => x.id === this.clientId);
    if (!c) return;
    const label = (c.name + (this.city ? '-' + this.city : '')).replace(/[^A-Za-z0-9]+/g, '-');
    this.run('client', this.api.clientReport(c.id, toIsoDate(this.cFrom)!, toIsoDate(this.cTo)!, this.city || undefined),
             `Client-${label}-${toIsoDate(this.cFrom)}-to-${toIsoDate(this.cTo)}.xlsx`);
  }

  clientStatement() {
    const c = this.clients().find(x => x.id === this.clientId);
    if (!c) return;
    const label = (c.name + (this.city ? '-' + this.city : '')).replace(/[^A-Za-z0-9]+/g, '-');
    this.run('statement', this.api.clientStatement(c.id, toIsoDate(this.cFrom)!, toIsoDate(this.cTo)!, this.city || undefined),
             `Statement-${label}-${toIsoDate(this.cFrom)}-to-${toIsoDate(this.cTo)}.pdf`);
  }

  backup() { this.run('backup', this.api.backup(), `shahin-billing-backup-${toIsoDate(new Date())}.json`); }

  private run(kind: 'gstr' | 'sales' | 'client' | 'statement' | 'backup', req: import('rxjs').Observable<Blob>, name: string) {
    this.busy.set(kind);
    req.subscribe({
      next: b => { saveBlob(b, name); this.busy.set(''); },
      error: e => { this.notify.error(e); this.busy.set(''); },
    });
  }
}
