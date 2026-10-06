import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ActivityApi, TeamApi } from '../../core/api.service';
import { toIsoDate } from '../../core/gst';
import { ActivityEntry, TeamMember } from '../../core/models';
import { Notify } from '../../core/notify.service';

const TYPES = ['Order', 'Bill', 'Payment', 'Client', 'Item', 'Setup', 'Email', 'Team', 'Login', 'Backup', 'Import'];
const TYPE_SEVERITY: Record<string, 'info' | 'success' | 'warn' | 'secondary' | 'danger'> = {
  Order: 'info', Bill: 'info', Payment: 'success', Client: 'secondary', Item: 'secondary', Setup: 'warn', Email: 'warn',
  Team: 'danger', Login: 'secondary', Backup: 'secondary', Import: 'warn',
};

/** Activity: who did what and when. Owner only. Entries are kept for a year. */
@Component({
  selector: 'app-activity',
  imports: [DatePipe, FormsModule, ButtonModule, DatePickerModule, SelectModule, TableModule, TagModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Activity</h1>
          <p>A record of what happened and who did it: bills, payments, clients, items, settings, logins and more. Kept for a year.</p>
        </div>
      </div>

      <div class="panel">
        <div class="filters">
          <p-datepicker [(ngModel)]="from" (ngModelChange)="filterChanged()" placeholder="From" dateFormat="dd-mm-yy" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-datepicker [(ngModel)]="to" (ngModelChange)="filterChanged()" placeholder="To" dateFormat="dd-mm-yy" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-select [options]="people()" optionLabel="name" optionValue="id" [(ngModel)]="userId" (onChange)="filterChanged()"
                    placeholder="Everyone" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-select [options]="types" [(ngModel)]="type" (onChange)="filterChanged()" placeholder="Every kind" [showClear]="true" [fluid]="true" appendTo="body" />
          <button pButton type="button" label="Clear" icon="pi pi-filter-slash" [text]="true" (click)="clear()"></button>
        </div>

        <p-table [value]="rows()" [lazy]="true" (onLazyLoad)="onLazy($event)" [paginator]="true" [rows]="pageSize"
                 [totalRecords]="total()" [loading]="loading()" [rowsPerPageOptions]="[25, 50, 100]" dataKey="id" [rowHover]="true">
          <ng-template #header>
            <tr><th>When</th><th>Who</th><th>What happened</th><th>Kind</th></tr>
          </ng-template>
          <ng-template #body let-a>
            <tr>
              <td class="when">{{ a.at | date: 'dd-MM-yyyy hh:mm a' }}</td>
              <td class="who">{{ a.userName }}</td>
              <td>{{ a.summary }}</td>
              <td><p-tag [severity]="severity(a.entityType)" [value]="a.entityType" [rounded]="true" /></td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="4"><div class="empty"><h3>Nothing here</h3><p>No activity matches these filters.</p></div></td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
  styles: `
    .filters { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)) auto; gap: 10px; margin-bottom: 14px; align-items: center; }
    .when { white-space: nowrap; color: var(--muted); }
    .who { font-weight: 600; white-space: nowrap; }
    @media (max-width: 900px) { .filters { grid-template-columns: 1fr 1fr; } }
  `,
})
export class ActivityPage implements OnInit {
  private api = inject(ActivityApi);
  private team = inject(TeamApi);
  private notify = inject(Notify);

  rows = signal<ActivityEntry[]>([]);
  total = signal(0);
  loading = signal(true);
  people = signal<TeamMember[]>([]);
  types = TYPES;
  pageSize = 50;
  page = 1;

  from: Date | null = null;
  to: Date | null = null;
  userId: string | null = null;
  type: string | null = null;

  severity(t: string) { return TYPE_SEVERITY[t] ?? 'secondary'; }

  ngOnInit() { this.team.list().subscribe({ next: p => this.people.set(p), error: () => {} }); }

  filterChanged() { this.page = 1; this.load(); }
  clear() { this.from = null; this.to = null; this.userId = null; this.type = null; this.filterChanged(); }

  onLazy(e: TableLazyLoadEvent) {
    this.pageSize = e.rows ?? 50;
    this.page = Math.floor((e.first ?? 0) / this.pageSize) + 1;
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.list({ from: toIsoDate(this.from), to: toIsoDate(this.to), userId: this.userId, type: this.type, page: this.page, pageSize: this.pageSize }).subscribe({
      next: r => { this.rows.set(r.items); this.total.set(r.total); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }
}
