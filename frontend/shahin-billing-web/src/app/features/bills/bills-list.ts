import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Subject, concat, debounceTime, of, switchMap, toArray } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { MenuModule } from 'primeng/menu';
import { TooltipModule } from 'primeng/tooltip';
import { TagModule } from 'primeng/tag';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { ClientsApi, InvoicesApi, saveBlob } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { toIsoDate } from '../../core/gst';
import { Client, InvoiceListItem } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';
import { StatusTag } from '../../shared/status-tag';

@Component({
  selector: 'app-bills-list',
  imports: [FormsModule, DatePipe, RouterLink, ButtonModule, DatePickerModule, InputTextModule, SelectModule, TableModule,
            IconFieldModule, InputIconModule, MenuModule, TooltipModule, TagModule, DialogModule, InputNumberModule, SelectButtonModule, InrPipe, StatusTag],
  templateUrl: './bills-list.html',
  styles: `
    .filters { display: grid; grid-template-columns: 2fr 1.5fr 1fr 1fr 1fr 1fr; gap: 10px; margin-bottom: 14px; align-items: end; }
    .filters input { width: 100%; }
    .no { font-weight: 700; color: var(--navy); text-decoration: none; }
    .row-actions { text-align: right; white-space: nowrap; }
    tr { cursor: default; }
    .pay-link { background: none; border: 0; padding: 0; cursor: pointer; border-radius: 999px; }
    .pay-link:hover { filter: brightness(0.95); box-shadow: 0 0 0 3px rgba(42, 91, 143, 0.15); }
    :host ::ng-deep .ng-tag { margin-left: 8px; font-size: 0.68rem; }
    .status-cell { display: flex; align-items: center; gap: 2px; }
    .pay-body { display: flex; flex-direction: column; gap: 14px; }
    @media (max-width: 1000px) { .filters { grid-template-columns: 1fr 1fr; } }
  `,
})
export class BillsList implements OnInit {
  private api = inject(InvoicesApi);
  private clientsApi = inject(ClientsApi);
  private router = inject(Router);
  private notify = inject(Notify);
  private auth = inject(AuthService);
  private confirm = inject(ConfirmationService);

  bills = signal<InvoiceListItem[]>([]);
  total = signal(0);
  loading = signal(true);
  clients = signal<Client[]>([]);
  pageSize = 20;
  page = 1;

  search = '';
  clientId: string | null = null;
  from: Date | null = null;
  to: Date | null = null;
  status: string | null = null;
  paymentStatus: string | null = null;

  statusOptions = [{ label: 'Final', value: 'Final' }, { label: 'Draft', value: 'Draft' }, { label: 'Cancelled', value: 'Cancelled' }];
  paymentOptions = [{ label: 'Unpaid', value: 'Unpaid' }, { label: 'Part paid', value: 'PartlyPaid' }, { label: 'Paid', value: 'Paid' }];

  private searchInput = new Subject<string>();
  menuItems: MenuItem[] = [];

  constructor() {
    // Wait until typing pauses before searching
    this.searchInput.pipe(debounceTime(300), takeUntilDestroyed()).subscribe(() => { this.page = 1; this.load(); });
  }

  ngOnInit() {
    this.clientsApi.list().subscribe(c => this.clients.set(c));
  }

  onSearch(v: string) { this.search = v; this.searchInput.next(v); }
  filterChanged() { this.page = 1; this.load(); }

  clear() {
    this.search = ''; this.clientId = null; this.from = null; this.to = null; this.status = null; this.paymentStatus = null;
    this.filterChanged();
  }

  onLazy(e: TableLazyLoadEvent) {
    this.pageSize = e.rows ?? 20;
    this.page = Math.floor((e.first ?? 0) / this.pageSize) + 1;
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.list({
      search: this.search, clientId: this.clientId ?? undefined, from: toIsoDate(this.from), to: toIsoDate(this.to),
      status: this.status, paymentStatus: this.paymentStatus, page: this.page, pageSize: this.pageSize,
    }).subscribe({
      next: r => { this.bills.set(r.items); this.total.set(r.total); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  openMenu(menu: { toggle: (e: Event) => void }, e: Event, b: InvoiceListItem) {
    this.menuItems = [
      { label: 'Duplicate', icon: 'pi pi-copy', command: () => this.duplicate(b) },
      ...(b.status === 'Final' ? [{ label: 'Change payment status', icon: 'pi pi-wallet', command: () => this.openStatus(b) }] : []),
      ...(b.status === 'Final' && b.balanceDue > 0 ? [{ label: 'Send payment reminder', icon: 'pi pi-bell', command: () => this.remind(b) }] : []),
      { separator: true },
      { label: 'Download original', icon: 'pi pi-download', command: () => this.pdf(b, ['Original']) },
      { label: 'Download duplicate', icon: 'pi pi-download', command: () => this.pdf(b, ['Duplicate']) },
      { label: 'Download triplicate', icon: 'pi pi-download', command: () => this.pdf(b, ['Triplicate']) },
      { label: 'Download all 3 copies', icon: 'pi pi-clone', command: () => this.pdf(b, ['Original', 'Duplicate', 'Triplicate']) },
    ];
    menu.toggle(e);
  }

  // ---------- payment status ----------
  // A bill's payment status comes from the payments recorded on it, so changing it records
  // (or removes) payments rather than editing a flag.
  statusOpen = signal(false);
  statusBill = signal<InvoiceListItem | null>(null);
  statusSaving = signal(false);
  statusChoice: 'Paid' | 'Part' | 'Unpaid' = 'Paid';
  payAmount = 0;
  payMode = 'Cash';
  payDate: Date = new Date();
  /** Marking a bill unpaid removes payments, which only the owner can do. */
  get statusChoices() {
    const all = [{ label: 'Paid', value: 'Paid' }, { label: 'Part paid', value: 'Part' }, { label: 'Unpaid', value: 'Unpaid' }];
    return this.auth.isOwner() ? all : all.filter(c => c.value !== 'Unpaid');
  }
  payModes = ['Cash', 'Bank Transfer', 'UPI', 'Cheque'].map(x => ({ label: x, value: x }));

  openStatus(b: InvoiceListItem) {
    this.statusBill.set(b);
    this.statusChoice = b.paymentStatus === 'Paid' && this.auth.isOwner() ? 'Unpaid' : 'Paid';
    this.payAmount = b.balanceDue;
    this.payMode = 'Cash';
    this.payDate = new Date();
    this.statusOpen.set(true);
  }

  saveStatus() {
    const b = this.statusBill()!;
    const done = () => { this.statusSaving.set(false); this.statusOpen.set(false); this.load(); };
    const fail = (e: unknown) => { this.statusSaving.set(false); this.notify.error(e); };

    if (this.statusChoice === 'Unpaid') {
      this.statusSaving.set(true);
      this.api.get(b.id).pipe(
        switchMap(inv => inv.payments.length
          ? concat(...inv.payments.map(p => this.api.removePayment(b.id, p.id))).pipe(toArray())
          : of([])),
      ).subscribe({ next: () => { this.notify.ok(`${b.invoiceNumber} marked unpaid`); done(); }, error: fail });
      return;
    }

    const amount = this.statusChoice === 'Paid' ? b.balanceDue : this.payAmount;
    if (!(amount > 0)) { this.notify.warn(this.statusChoice === 'Paid' ? 'This bill is already fully paid.' : 'Enter the amount received.'); return; }
    if (amount > b.balanceDue) { this.notify.warn('That is more than the balance due.'); return; }

    this.statusSaving.set(true);
    this.api.addPayment(b.id, {
      date: toIsoDate(this.payDate)!, amount, mode: this.payMode, reference: '',
      note: this.statusChoice === 'Paid' ? 'Marked as paid' : '',
    }).subscribe({
      next: () => { this.notify.ok(this.statusChoice === 'Paid' ? `${b.invoiceNumber} marked paid` : 'Payment recorded'); done(); },
      error: fail,
    });
  }

  /** Emails the client a reminder for this bill (asks first, so a stray click doesn't email a customer). */
  remind(b: InvoiceListItem) {
    this.confirm.confirm({
      header: 'Send a payment reminder?',
      message: `Email a reminder for bill ${b.invoiceNumber} (${b.clientName}, balance due ${b.balanceDue.toLocaleString('en-IN', { minimumFractionDigits: 2 })})?`,
      acceptLabel: 'Send reminder', rejectLabel: 'Cancel',
      accept: () => this.api.remind(b.id).subscribe({
        next: r => { this.notify.ok(r.message); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }

  duplicate(b: InvoiceListItem) {
    this.api.duplicate(b.id).subscribe({
      next: inv => { this.notify.ok(`Draft ${inv.invoiceNumber} created from ${b.invoiceNumber}`); this.router.navigate(['/bills', inv.id, 'edit']); },
      error: e => this.notify.error(e),
    });
  }

  pdf(b: InvoiceListItem, copies?: string[]) {
    this.api.pdf(b.id, copies, true).subscribe({
      next: blob => saveBlob(blob, `${b.invoiceNumber.replace(/\//g, '-')}.pdf`),
      error: e => this.notify.error(e),
    });
  }
}
