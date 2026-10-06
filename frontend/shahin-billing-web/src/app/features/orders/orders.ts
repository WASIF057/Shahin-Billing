import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ClientsApi, ItemsApi, OrdersApi } from '../../core/api.service';
import { OrderBadge } from '../../core/order-badge.service';
import { toIsoDate } from '../../core/gst';
import { Client, Item, Order, OrderPriority, OrderStatus } from '../../core/models';
import { Notify } from '../../core/notify.service';

/** Orders: the bucket of orders clients placed on the ordering website, for the owner and staff. */
@Component({
  selector: 'app-orders',
  imports: [DatePipe, FormsModule, RouterLink, ButtonModule, CheckboxModule, DatePickerModule, DialogModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, SelectModule, TableModule, TagModule, TooltipModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Orders</h1>
          <p>What your clients ordered on the ordering website. Accept an order, or make the bill straight from it.</p>
        </div>
        <div class="actions">
          <button pButton type="button" label="Refresh" icon="pi pi-refresh" [outlined]="true" (click)="filterChanged()"></button>
          <button pButton type="button" label="Take an order" icon="pi pi-phone" (click)="openTake()"></button>
        </div>
      </div>

      <div class="panel">
        <div class="filters">
          <p-select [options]="statusOptions" optionLabel="label" optionValue="value" [(ngModel)]="status" (onChange)="filterChanged()"
                    placeholder="Any status" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-select [options]="clients()" optionLabel="name" optionValue="id" [(ngModel)]="clientId" (onChange)="filterChanged()"
                    [filter]="true" filterBy="name" placeholder="All clients" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-datepicker [(ngModel)]="from" (ngModelChange)="filterChanged()" placeholder="From" dateFormat="dd-mm-yy" [showClear]="true" [fluid]="true" appendTo="body" />
          <p-datepicker [(ngModel)]="to" (ngModelChange)="filterChanged()" placeholder="To" dateFormat="dd-mm-yy" [showClear]="true" [fluid]="true" appendTo="body" />
        </div>

        <p-table [value]="rows()" [lazy]="true" (onLazyLoad)="onLazy($event)" [paginator]="true" [rows]="pageSize" [totalRecords]="total()"
                 [loading]="loading()" [rowsPerPageOptions]="[25, 50, 100]" dataKey="id" [rowHover]="true">
          <ng-template #header>
            <tr><th>Order</th><th>Placed</th><th>Client</th><th>Products</th><th>Status</th><th></th></tr>
          </ng-template>
          <ng-template #body let-o>
            <tr [class.fresh]="o.status === 'New'">
              <td class="no">
                {{ o.orderNumber }}
                @if (o.source === 'Phone') { <i class="pi pi-phone muted" [pTooltip]="'Taken by phone' + (o.takenBy ? ' by ' + o.takenBy : '')"></i> }
                @if (o.priority && o.priority !== 'Normal') { <div><p-tag [severity]="o.priority === 'Urgent' ? 'danger' : 'warn'" [value]="o.priority" [rounded]="true" /></div> }
              </td>
              <td class="muted">{{ o.createdAt | date: 'dd-MM-yyyy hh:mm a' }}</td>
              <td>
                <div class="nm">{{ o.clientName }}</div>
                @if (o.city) { <div class="muted small">{{ o.city }}</div> }
              </td>
              <td class="items">
                @for (l of o.lines.slice(0, 2); track l.itemId) { <div>{{ l.label }} <strong>× {{ l.quantity }}</strong></div> }
                @if (o.lines.length > 2) { <div class="muted small">+ {{ o.lines.length - 2 }} more</div> }
              </td>
              <td>
                <p-tag [severity]="severity(o.status)" [value]="o.status" [rounded]="true" />
                @if (o.status === 'Billed' && o.invoiceId) { <div class="small"><a [routerLink]="['/bills', o.invoiceId]">{{ o.invoiceNumber }}</a></div> }
              </td>
              <td class="row-actions">
                <button pButton type="button" icon="pi pi-eye" [text]="true" pTooltip="View" (click)="view(o)" aria-label="View"></button>
                @if (o.status === 'New') {
                  <button pButton type="button" icon="pi pi-check" [text]="true" pTooltip="Accept" (click)="accept(o)" aria-label="Accept"></button>
                }
                @if (o.status === 'New' || o.status === 'Accepted') {
                  <button pButton type="button" icon="pi pi-file-edit" [text]="true" pTooltip="Make the bill" (click)="makeBill(o)" aria-label="Make the bill"></button>
                  <button pButton type="button" icon="pi pi-times" [text]="true" severity="danger" pTooltip="Cancel" (click)="openCancel(o)" aria-label="Cancel"></button>
                }
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="6"><div class="empty"><h3>No orders</h3><p>Orders your clients place on the ordering website will appear here.</p></div></td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <p-dialog [header]="viewing()?.orderNumber ?? ''" [visible]="viewOpen()" (visibleChange)="viewOpen.set($event)" [modal]="true"
              [style]="{ width: '560px' }" [breakpoints]="{ '620px': '96vw' }" [draggable]="false">
      @if (viewing(); as o) {
        <div class="head">
          <div><span class="muted small">Client</span><br /><strong>{{ o.clientName }}</strong>{{ o.city ? ' · ' + o.city : '' }}</div>
          <div><span class="muted small">Placed</span><br /><strong>{{ o.createdAt | date: 'dd-MM-yyyy hh:mm a' }}</strong></div>
          <div><span class="muted small">Status</span><br /><p-tag [severity]="severity(o.status)" [value]="o.status" [rounded]="true" />
            @if (o.priority && o.priority !== 'Normal') { <p-tag [severity]="o.priority === 'Urgent' ? 'danger' : 'warn'" [value]="o.priority" [rounded]="true" /> }</div>
        </div>
        @if (o.source === 'Phone') { <p class="muted small">Taken by phone{{ o.takenBy ? ' by ' + o.takenBy : '' }}.</p> }
        <ul class="lines">
          @for (l of o.lines; track l.itemId) { <li><span>{{ l.label }}</span><strong>{{ l.quantity }} {{ l.unit }}</strong></li> }
        </ul>
        @if (o.note) { <p class="note"><strong>Client’s note:</strong> {{ o.note }}</p> }
        @if (o.status === 'Cancelled' && o.cancelReason) { <p class="muted small">{{ o.cancelReason }}{{ o.handledBy ? ' (' + o.handledBy + ')' : '' }}</p> }
        @if (o.status === 'Billed') { <p class="small">Billed as <a [routerLink]="['/bills', o.invoiceId]" (click)="viewOpen.set(false)">{{ o.invoiceNumber }}</a></p> }
      }
      <ng-template #footer>
        <button pButton type="button" label="Close" [text]="true" (click)="viewOpen.set(false)"></button>
        @if (viewing(); as o) {
          @if (o.status === 'New') { <button pButton type="button" label="Accept" icon="pi pi-check" [outlined]="true" (click)="accept(o)"></button> }
          @if (o.status === 'New' || o.status === 'Accepted') { <button pButton type="button" label="Make the bill" icon="pi pi-file-edit" (click)="makeBill(o)"></button> }
        }
      </ng-template>
    </p-dialog>

    <p-dialog header="Take an order" [visible]="takeOpen()" (visibleChange)="takeOpen.set($event)" [modal]="true"
              [style]="{ width: '700px' }" [breakpoints]="{ '760px': '96vw' }" [draggable]="false">
      <form id="takeForm" class="take" (ngSubmit)="saveTake()" (keydown.enter)="$event.preventDefault()">
        <div class="field">
          <label for="tclient">Client</label>
          <p-select inputId="tclient" name="client" [options]="clients()" optionLabel="name" optionValue="id" [(ngModel)]="t.clientId" (onChange)="clientPicked()"
                    [filter]="true" filterBy="name" placeholder="Who is ordering?" [fluid]="true" appendTo="body" />
        </div>
        @if (takeCities().length > 1) {
          <div class="field">
            <label for="tcity">City</label>
            <p-select inputId="tcity" name="city" [options]="takeCities()" [(ngModel)]="t.city" [fluid]="true" appendTo="body" />
          </div>
        }

        <div class="field">
          <label for="tsearch">Products</label>
          <p-iconfield>
            <p-inputicon styleClass="pi pi-search" />
            <input pInputText id="tsearch" name="search" type="text" placeholder="Search products" [ngModel]="search()" (ngModelChange)="search.set($event)" />
          </p-iconfield>
        </div>
        <div class="plist">
          @for (g of groups(); track g.type) {
            <div class="pgroup">
              <h3>{{ g.type }}</h3>
              @for (p of g.items; track p.id) {
                <div class="prow" [class.on]="(qty()[p.id] ?? 0) > 0">
                  <div class="pname">
                    <strong>{{ p.name }}</strong>
                    @if (detail(p)) { <span class="muted small">{{ detail(p) }}</span> }
                  </div>
                  <div class="pqty">
                    <p-inputnumber [name]="'q-' + p.id" [ngModel]="qty()[p.id] ?? null" (ngModelChange)="setQty(p.id, $event)" [min]="0" [max]="10000"
                                   [maxFractionDigits]="2" placeholder="0" [suffix]="' ' + p.unit" [showButtons]="true" buttonLayout="horizontal"
                                   incrementButtonIcon="pi pi-plus" decrementButtonIcon="pi pi-minus" inputStyleClass="qtybox" [step]="1" />
                  </div>
                </div>
              }
            </div>
          } @empty { <p class="muted small">{{ items().length ? 'No product matches your search.' : 'No products yet.' }}</p> }
        </div>
        <p class="small chosen"><strong>{{ chosen().length }}</strong> {{ chosen().length === 1 ? 'product' : 'products' }} chosen</p>

        <div class="two">
          <div class="field">
            <label for="tprio">Priority</label>
            <p-select inputId="tprio" name="prio" [options]="priorities" optionLabel="label" optionValue="value" [(ngModel)]="t.priority" [fluid]="true" appendTo="body" />
          </div>
          <div class="field">
            <label for="tnote">Note (optional)</label>
            <input pInputText id="tnote" name="note" maxlength="500" [(ngModel)]="t.note" placeholder="e.g. Deliver Monday" />
          </div>
        </div>

        <div class="mailopts">
          <div class="check-row">
            <p-checkbox inputId="temc" name="emailClient" [binary]="true" [(ngModel)]="t.emailClient" [disabled]="!takeClientEmail()" />
            <label for="temc">Email a copy to the client {{ takeClientEmail() ? '(' + takeClientEmail() + ')' : '(this client has no email)' }}</label>
          </div>
          <div class="check-row">
            <p-checkbox inputId="temm" name="emailMe" [binary]="true" [(ngModel)]="t.emailMe" />
            <label for="temm">Email a copy to me</label>
          </div>
        </div>
      </form>
      <ng-template #footer>
        <button pButton type="button" label="Cancel" [text]="true" (click)="takeOpen.set(false)"></button>
        <button pButton type="submit" form="takeForm" label="Save order" icon="pi pi-check" [loading]="taking()"></button>
      </ng-template>
    </p-dialog>

    <p-dialog header="Cancel this order?" [visible]="cancelOpen()" (visibleChange)="cancelOpen.set($event)" [modal]="true"
              [style]="{ width: '440px' }" [breakpoints]="{ '500px': '96vw' }" [draggable]="false">
      <div class="field">
        <label for="creason">Reason (the client will see it)</label>
        <input pInputText id="creason" [(ngModel)]="reason" maxlength="200" placeholder="e.g. Out of stock" />
        <span class="hint">The client is emailed that the order was cancelled, with this reason.</span>
      </div>
      <ng-template #footer>
        <button pButton type="button" label="Keep the order" [text]="true" (click)="cancelOpen.set(false)"></button>
        <button pButton type="button" label="Cancel order" severity="danger" icon="pi pi-times" (click)="cancel()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .take { display: flex; flex-direction: column; gap: 14px; }
    .two { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .plist { max-height: 340px; overflow-y: auto; border: 1px solid var(--line); border-radius: 12px; padding: 4px 12px 8px; }
    .pgroup h3 { margin: 12px 0 4px; color: var(--navy); font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.06em; }
    .prow { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 8px 8px; border-top: 1px solid var(--line); border-radius: 8px; }
    .prow.on { background: var(--navy-soft); }
    .pname { display: flex; flex-direction: column; min-width: 0; }
    .pqty { width: 180px; flex-shrink: 0; }
    :host ::ng-deep .qtybox { text-align: center; width: 100%; }
    .chosen { margin: 0; }
    .mailopts { display: flex; flex-direction: column; gap: 8px; background: var(--navy-soft); padding: 10px 12px; border-radius: 10px; }
    .filters { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; margin-bottom: 14px; }
    .no { font-weight: 800; }
    .nm { font-weight: 600; }
    .items { max-width: 340px; }
    tr.fresh td { background: #fffbea; }
    .row-actions { text-align: right; white-space: nowrap; }
    .head { display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; margin-bottom: 14px; }
    .lines { list-style: none; margin: 0; padding: 0; }
    .lines li { display: flex; justify-content: space-between; gap: 16px; padding: 7px 0; border-bottom: 1px solid var(--line); }
    .note { background: var(--navy-soft); padding: 10px 12px; border-radius: 10px; margin: 14px 0 0; }
    @media (max-width: 900px) { .filters { grid-template-columns: 1fr 1fr; } .head { grid-template-columns: 1fr; } }
    @media (max-width: 560px) { .two { grid-template-columns: 1fr; } .prow { flex-direction: column; align-items: stretch; gap: 6px; } .pqty { width: 100%; } }
  `,
})
export class OrdersPage implements OnInit {
  private api = inject(OrdersApi);
  private clientsApi = inject(ClientsApi);
  private itemsApi = inject(ItemsApi);
  private router = inject(Router);
  private notify = inject(Notify);
  private badge = inject(OrderBadge);

  rows = signal<Order[]>([]);
  total = signal(0);
  loading = signal(true);
  clients = signal<Client[]>([]);
  pageSize = 25;
  page = 1;

  status: OrderStatus | null = 'New';   // the bucket opens on what is waiting
  clientId: string | null = null;
  from: Date | null = null;
  to: Date | null = null;
  statusOptions = (['New', 'Accepted', 'Billed', 'Cancelled'] as OrderStatus[]).map(s => ({ label: s, value: s }));

  viewing = signal<Order | null>(null);
  viewOpen = signal(false);
  cancelOpen = signal(false);
  cancelling: Order | null = null;
  reason = '';

  severity(s: string): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    return ({ New: 'info', Accepted: 'warn', Billed: 'success', Cancelled: 'danger' } as Record<string, 'info' | 'success' | 'warn' | 'danger'>)[s] ?? 'secondary';
  }

  // ---- taking an order over the phone
  priorities: { label: string; value: OrderPriority }[] = [{ label: 'Normal', value: 'Normal' }, { label: 'High', value: 'High' }, { label: 'Urgent', value: 'Urgent' }];
  takeOpen = signal(false);
  taking = signal(false);
  items = signal<Item[]>([]);
  t = this.blankTake();
  qty = signal<Record<string, number | undefined>>({});
  search = signal('');

  private blankTake() {
    return { clientId: '', city: '', note: '', priority: 'Normal' as OrderPriority, emailClient: false, emailMe: false };
  }

  /** Products grouped by type, filtered by the search box: the same look as the client's ordering page. */
  groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const map = new Map<string, Item[]>();
    for (const i of this.items()) {
      if (q && !`${i.name} ${i.sizeOrVariant} ${i.typeName}`.toLowerCase().includes(q)) continue;
      const key = i.typeName || 'Other products';
      (map.get(key) ?? map.set(key, []).get(key)!).push(i);
    }
    return [...map].map(([type, items]) => ({ type, items }));
  });
  chosen = computed(() => this.items().filter(i => (this.qty()[i.id] ?? 0) > 0).map(i => ({ itemId: i.id, quantity: this.qty()[i.id] ?? 0 })));
  detail(i: Item) { return [i.variant, i.cloth, i.colour, i.size].filter(Boolean).join(' \u00b7 '); }
  setQty(id: string, value: number | null) {
    this.qty.update(q => {
      const next = { ...q };
      if (value && value > 0) next[id] = value; else delete next[id];
      return next;
    });
  }
  private takeClient() { return this.clients().find(c => c.id === this.t.clientId); }
  takeCities() { return this.takeClient()?.cities ?? []; }
  takeClientEmail() { return (this.takeClient()?.email ?? '').trim(); }

  openTake() {
    this.t = this.blankTake();
    this.t.emailMe = true;
    this.qty.set({}); this.search.set('');
    if (!this.items().length) this.itemsApi.list(undefined, true).subscribe({ next: i => this.items.set(i), error: e => this.notify.error(e) });
    this.takeOpen.set(true);
  }

  /** Choosing the client sets their first city and ticks "email the client" when they have an email. */
  clientPicked() {
    this.t.city = this.takeCities()[0] ?? '';
    this.t.emailClient = !!this.takeClientEmail();
  }

  saveTake() {
    if (!this.t.clientId) { this.notify.error('Choose the client who is ordering.'); return; }
    if (!this.chosen().length) { this.notify.error('Add at least one product.'); return; }
    this.taking.set(true);
    const emailing = (this.t.emailClient && !!this.takeClientEmail()) || this.t.emailMe;
    this.api.take({ ...this.t, lines: this.chosen(), emailClient: this.t.emailClient && !!this.takeClientEmail() }).subscribe({
      next: o => {
        this.taking.set(false); this.takeOpen.set(false);
        this.notify.ok(`Order ${o.orderNumber} saved${emailing ? '. The email is on its way.' : ''}`);
        this.status = null; this.filterChanged();
      },
      error: e => { this.notify.error(e); this.taking.set(false); },
    });
  }

  ngOnInit() { this.clientsApi.list().subscribe({ next: c => this.clients.set(c), error: () => {} }); }

  filterChanged() { this.page = 1; this.load(); }

  onLazy(e: TableLazyLoadEvent) {
    this.pageSize = e.rows ?? 25;
    this.page = Math.floor((e.first ?? 0) / this.pageSize) + 1;
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.list({ status: this.status, clientId: this.clientId, from: toIsoDate(this.from), to: toIsoDate(this.to), page: this.page, pageSize: this.pageSize }).subscribe({
      next: r => { this.rows.set(r.items); this.total.set(r.total); this.loading.set(false); this.badge.refresh(); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  view(o: Order) { this.viewing.set(o); this.viewOpen.set(true); }

  accept(o: Order) {
    this.api.setStatus(o.id, 'Accepted').subscribe({
      next: u => { this.notify.ok(`Order ${u.orderNumber} accepted. The client is being emailed.`); this.viewOpen.set(false); this.load(); },
      error: e => this.notify.error(e),
    });
  }

  makeBill(o: Order) {
    this.viewOpen.set(false);
    this.router.navigate(['/bills/new'], { queryParams: { orderId: o.id } });
  }

  openCancel(o: Order) { this.cancelling = o; this.reason = ''; this.cancelOpen.set(true); }

  cancel() {
    const o = this.cancelling;
    if (!o) return;
    this.api.setStatus(o.id, 'Cancelled', this.reason).subscribe({
      next: u => { this.notify.ok(`Order ${u.orderNumber} cancelled. The client is being emailed.`); this.cancelOpen.set(false); this.load(); },
      error: e => this.notify.error(e),
    });
  }
}
