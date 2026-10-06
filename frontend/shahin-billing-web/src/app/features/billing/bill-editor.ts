import { Component, computed, HostListener, inject, input, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { CheckboxModule } from 'primeng/checkbox';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { BusinessApi, ClientsApi, InvoicesApi, ItemsApi, OrdersApi, openBlob, saveBlob } from '../../core/api.service';
import {
  amountInWords, calculate, CalcLine, fromApiDate, isInterState, resolveRate, STATES, stateName, toIsoDate,
} from '../../core/gst';
import { Business, Client, emptyAddress, Invoice, InvoiceRequest, Item, Order, PartySnapshot, TransportDetails } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';

/** One row of the bill while editing. */
interface EditorLine {
  key: number;
  itemId: string | null;
  quantity: number;
  rate: number;
  discount: number;
  gstRate: number;
  hsnCode: string;
  unit: string;
  isSpecial: boolean;
}

interface ItemOption { id: string; label: string; size: string; hsnCode: string; item: Item; }

let lineKey = 1;

@Component({
  selector: 'app-bill-editor',
  imports: [FormsModule, RouterLink, ButtonModule, DatePickerModule, InputNumberModule, InputTextModule, SelectModule,
            CheckboxModule, TagModule, TextareaModule, TooltipModule, SelectButtonModule, InrPipe],
  templateUrl: './bill-editor.html',
  styleUrl: './bill-editor.scss',
})
export class BillEditor implements OnInit {
  /** Route param :id (thanks to withComponentInputBinding). Empty = new bill. */
  id = input<string>();
  /** ?orderId= : the bill is being made from a client's order (the client and products are filled in). */
  orderId = input<string>();

  private businessApi = inject(BusinessApi);
  private clientsApi = inject(ClientsApi);
  private ordersApi = inject(OrdersApi);
  private itemsApi = inject(ItemsApi);
  private invoicesApi = inject(InvoicesApi);
  private router = inject(Router);
  private notify = inject(Notify);

  states = STATES;
  loading = signal(true);
  saving = signal<'' | 'draft' | 'final' | 'pdf'>('');
  moreOpen = signal(false);
  dirty = false;

  business = signal<Business | null>(null);
  clients = signal<Client[]>([]);
  itemOptions = signal<ItemOption[]>([]);
  existing = signal<Invoice | null>(null);

  // ---- form model (mutated by ngModel; `tick` tells computed values to re-run) ----
  tick = signal(0);
  invoiceDate: Date = new Date();
  clientId: string | null = null;
  /** true = a plain Non-GST bill. Chosen when the bill is created and fixed after that (its number comes from that series). */
  nonGst = false;
  billTypes = [{ label: 'GST invoice', value: false }, { label: 'Non-GST bill', value: true }];
  /** Which of the client's cities this bill is for. */
  city = '';
  shipToSame = true;
  shipTo: PartySnapshot = { name: '', gstin: '', contactPerson: '', phone: '', address: emptyAddress() };
  placeOfSupply = '';
  poNumber = '';
  poDate: Date | null = null;
  transport: TransportDetails = { transporterName: '', vehicleNumber: '', ewayBillNumber: '', lrNumber: '', deliveryDate: null };
  deliveryDate: Date | null = null;
  notes = '';
  lines: EditorLine[] = [];

  // ---- derived values ----
  client = computed(() => { this.tick(); return this.clients().find(c => c.id === this.clientId) ?? null; });
  /** The selected client's cities, as dropdown options. */
  cityOptions = computed(() => (this.client()?.cities ?? []).map(x => ({ label: x, value: x })));
  billToCity = computed(() => { this.tick(); const c = this.client(); return c ? (this.city || c.cities?.[0] || c.billingAddress.city) : ''; });
  businessState = computed(() => {
    const b = this.business();
    return b ? b.address.stateCode || b.gstin.slice(0, 2) : '';
  });
  interState = computed(() => { this.tick(); return !this.nonGst && isInterState(this.businessState(), this.placeOfSupply); });
  calc = computed(() => {
    this.tick();
    return calculate(this.lines.map(l => ({ quantity: l.quantity, rate: l.rate, discount: l.discount, gstRate: this.nonGst ? 0 : l.gstRate })),
                     this.interState());
  });
  words = computed(() => amountInWords(this.calc().totals.grandTotal));
  /** Place of supply is shown as a city: where the goods go (same as the Bill To city when Ship To is the same). */
  placeOfSupplyCity = computed(() => {
    this.tick();
    return this.shipToSame ? this.billToCity() : (this.shipTo.address.city || this.billToCity());
  });
  /** Discount is no longer entered, but an older bill that has one keeps showing it. */
  hasDiscount = computed(() => { this.tick(); return this.lines.some(l => l.discount > 0); });
  previewing = signal(false);
  /** Which copies "Save & download PDF" prints. Starts from the Bill Format default. */
  copies = { Original: true, Duplicate: false, Triplicate: false };
  private selectedCopies() {
    const picked = (Object.keys(this.copies) as (keyof typeof this.copies)[]).filter(k => this.copies[k]);
    return picked.length ? picked : ['Original'];
  }
  numberPreview = computed(() => {
    const ex = this.existing();
    if (ex) return ex.invoiceNumber;
    return this.nextNumber() || '…';
  });
  /** The number a new bill will get; confirmed when the bill is saved. */
  nextNumber = signal('');
  stateName = stateName;

  ngOnInit() {
    forkJoin({
      business: this.businessApi.get(),
      clients: this.clientsApi.list(undefined, true),
      items: this.itemsApi.list(undefined, true),
    }).subscribe({
      next: ({ business, clients, items }) => {
        this.business.set(business);
        const defaults = business.template?.defaultCopies ?? ['Original'];
        this.copies = { Original: defaults.includes('Original'), Duplicate: defaults.includes('Duplicate'), Triplicate: defaults.includes('Triplicate') };
        this.clients.set(clients);
        this.itemOptions.set(items.map(i => ({
          id: i.id, label: i.sizeOrVariant ? `${i.name} — ${i.sizeOrVariant}` : i.name, size: i.sizeOrVariant, hsnCode: i.hsnCode, item: i,
        })));
        const id = this.id();
        if (!id) this.invoicesApi.nextNumber().subscribe(r => this.nextNumber.set(r.number));
        if (id) this.loadExisting(id);
        else if (this.orderId()) this.prefillFromOrder(this.orderId()!);
        else { this.addLine(); this.loading.set(false); }
      },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  /** The order this bill is being made from. It is marked Billed when the bill is saved. */
  orderRef = signal<Order | null>(null);

  private prefillFromOrder(orderId: string) {
    this.ordersApi.get(orderId).subscribe({
      next: o => {
        const client = this.clients().find(c => c.id === o.clientId);
        if (!client) {
          this.notify.warn('This order\u2019s client is hidden or has been removed, so the bill can\u2019t be filled in. Pick the client yourself.');
          this.addLine(); this.loading.set(false);
          return;
        }
        this.orderRef.set(o);
        this.clientId = o.clientId;
        this.clientChanged();                                   // city, ship-to, Non-GST default
        if (o.city && client.cities?.includes(o.city)) { this.city = o.city; this.cityChanged(); }
        this.lines = [];
        const missing: string[] = [];
        for (const ol of o.lines) {
          if (!this.itemOptions().some(x => x.id === ol.itemId)) { missing.push(ol.label); continue; }
          const line: EditorLine = { key: lineKey++, itemId: ol.itemId, quantity: ol.quantity, rate: 0, discount: 0, gstRate: 0, hsnCode: '', unit: '', isSpecial: false };
          this.lines.push(line);
          this.itemChanged(line);                               // this client's rate, GST and unit
        }
        if (!this.lines.length) this.addLine();
        if (missing.length) this.notify.warn(`No longer available, left out: ${missing.join(', ')}`);
        this.notify.info(`Filled in from order ${o.orderNumber}. Check the rates, then save.`);
        this.dirty = false;
        this.changed(false);
        this.loading.set(false);
      },
      error: e => { this.notify.error(e); this.addLine(); this.loading.set(false); },
    });
  }

  private loadExisting(id: string) {
    this.invoicesApi.get(id).subscribe({
      next: inv => {
        if (inv.status === 'Cancelled') {
          this.notify.warn('A cancelled bill can’t be edited.');
          this.router.navigate(['/bills', id]);
          return;
        }
        this.existing.set(inv);
        // A client hidden after billing must still be selectable on its own bill
        if (!this.clients().some(c => c.id === inv.clientId)) {
          this.clientsApi.get(inv.clientId).subscribe(c => this.clients.update(list => [...list, c]));
        }
        this.invoiceDate = fromApiDate(inv.invoiceDate) ?? new Date();
        this.clientId = inv.clientId;
        this.city = inv.city ?? '';
        this.nonGst = inv.isNonGst ?? false;
        this.shipToSame = inv.shipToSameAsBillTo;
        this.shipTo = structuredClone(inv.shipTo);
        this.placeOfSupply = inv.placeOfSupplyStateCode;
        this.poNumber = inv.poNumber;
        this.poDate = fromApiDate(inv.poDate);
        this.transport = structuredClone(inv.transport);
        this.deliveryDate = fromApiDate(inv.transport.deliveryDate);
        this.notes = inv.notes;
        this.lines = inv.lines.map(l => ({
          key: lineKey++, itemId: l.itemId, quantity: l.quantity, rate: l.rate, discount: l.discount,
          gstRate: l.gstRate, hsnCode: l.hsnCode, unit: l.unit, isSpecial: l.isSpecialRate,
        }));
        if (this.poNumber || this.transport.transporterName || this.transport.vehicleNumber || this.transport.ewayBillNumber || this.notes)
          this.moreOpen.set(true);
        this.changed(false);
        this.loading.set(false);
      },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  /** Call after any edit so totals recalculate. */
  changed(markDirty = true) {
    if (markDirty) this.dirty = true;
    this.tick.update(v => v + 1);
  }

  // ---------- client ----------
  clientChanged() {
    // Look the client up directly: the cached client() value is still the old one until tick changes
    const c = this.clients().find(x => x.id === this.clientId);
    if (!c) return;
    this.city = c.cities?.[0] ?? '';
    // A client marked "usually billed without GST" starts new bills as Non-GST (it can be changed on the bill)
    if (!this.existing() && this.nonGst !== !!c.billedWithoutGst) { this.nonGst = !!c.billedWithoutGst; this.refreshNextNumber(); }
    this.placeOfSupply = c.billingAddress.stateCode;
    this.shipToSame = true;
    this.resetShipTo(c);
    // Rule 4.1: rates depend on the client, so re-resolve every row
    let changedRates = 0;
    for (const l of this.lines) {
      const opt = this.itemOptions().find(o => o.id === l.itemId);
      if (!opt) continue;
      const r = resolveRate(opt.item, c.id);
      if (r.rate !== l.rate) changedRates++;
      l.rate = r.rate;
      l.isSpecial = r.isSpecial;
    }
    if (changedRates) this.notify.info(`Rates updated for ${c.name}.`);
    this.changed();
  }

  cityChanged() { this.changed(); }

  billTypeChanged() { this.refreshNextNumber(); this.changed(); }

  /** The number shown while creating comes from the series of the chosen bill type. */
  refreshNextNumber() {
    if (this.existing()) return;
    this.invoicesApi.nextNumber(this.nonGst).subscribe(r => this.nextNumber.set(r.number));
  }

  /** Ship To starts as the client's name with the Bill To city; the user can pick another of the client's cities. */
  private resetShipTo(c: Client) {
    this.shipTo = {
      name: c.name, gstin: c.gstin, contactPerson: '', phone: c.phone,
      address: { ...structuredClone(c.billingAddress), line1: '', line2: '', pincode: '', city: this.city || c.cities?.[0] || c.billingAddress.city },
    };
  }

  shipToSameChanged() {
    const c = this.client();
    if (!this.shipToSame && c) this.resetShipTo(c);
    this.changed();
  }

  // ---------- lines ----------
  addLine(focus = false) {
    this.lines.push({ key: lineKey++, itemId: null, quantity: 1, rate: 0, discount: 0, gstRate: 0, hsnCode: '', unit: '', isSpecial: false });
    this.changed(false);
    if (focus) {
      const index = this.lines.length - 1;
      setTimeout(() => (document.querySelector(`#line-item-${index} [role="combobox"]`) as HTMLElement | null)?.focus(), 50);
    }
  }

  removeLine(i: number) {
    this.lines.splice(i, 1);
    if (!this.lines.length) this.addLine();
    this.changed();
  }

  itemChanged(l: EditorLine) {
    const opt = this.itemOptions().find(o => o.id === l.itemId);
    if (!opt) return;
    const r = resolveRate(opt.item, this.clientId);
    l.rate = r.rate;
    l.isSpecial = r.isSpecial;
    l.gstRate = opt.item.gstRate;
    l.hsnCode = opt.item.hsnCode;
    l.unit = opt.item.unit;
    this.changed();
  }

  rateChanged(l: EditorLine) {
    const opt = this.itemOptions().find(o => o.id === l.itemId);
    l.isSpecial = !!opt && resolveRate(opt.item, this.clientId).isSpecial &&
                  resolveRate(opt.item, this.clientId).rate === l.rate;
    this.changed();
  }

  /** Enter in the last quantity box adds a new row. */
  qtyEnter(i: number, e: Event) {
    e.preventDefault();
    if (i === this.lines.length - 1) this.addLine(true);
  }

  lineCalc(i: number): CalcLine | undefined { return this.calc().lines[i]; }

  // ---------- save ----------
  private buildRequest(finalize: boolean): InvoiceRequest | null {
    if (!this.clientId) { this.notify.warn('Select a client first.'); return null; }
    const used = this.lines.filter(l => l.itemId);   // rows without an item are simply left out
    if (finalize && !used.length) { this.notify.warn('Add at least one item before finalizing.'); return null; }
    if (used.some(l => !(l.quantity > 0))) { this.notify.warn('Quantity must be more than 0 on every row.'); return null; }

    return {
      invoiceDate: toIsoDate(this.invoiceDate)!,
      clientId: this.clientId,
      city: this.city,
      nonGst: this.nonGst,
      orderId: this.orderRef()?.id ?? null,
      shipToSameAsBillTo: this.shipToSame,
      shipTo: this.shipToSame ? null : this.shipTo,
      placeOfSupplyStateCode: this.placeOfSupply,
      poNumber: this.poNumber,
      poDate: toIsoDate(this.poDate),
      transport: { ...this.transport, deliveryDate: toIsoDate(this.deliveryDate) },
      notes: this.notes,
      lines: used.map(l => ({ itemId: l.itemId!, quantity: l.quantity, rate: l.rate, discount: l.discount || 0 })),
      finalize,
    };
  }

  save(mode: 'draft' | 'final' | 'pdf') {
    const req = this.buildRequest(mode !== 'draft');
    if (!req) return;
    this.saving.set(mode);
    const ex = this.existing();
    const call: Observable<Invoice> = ex ? this.invoicesApi.update(ex.id, req) : this.invoicesApi.create(req);

    call.subscribe({
      next: inv => {
        this.dirty = false;
        if (mode === 'pdf') {
          this.invoicesApi.pdf(inv.id, this.selectedCopies(), true).subscribe({
            next: blob => {
              saveBlob(blob, `${inv.invoiceNumber.replace(/\//g, '-')}.pdf`);
              this.saving.set('');
              this.router.navigate(['/bills', inv.id]);
            },
            error: e => { this.notify.error(e); this.saving.set(''); },
          });
          this.notify.ok(`Bill ${inv.invoiceNumber} saved`);
          return;
        }
        this.saving.set('');
        if (mode === 'final') {
          this.notify.ok(`Bill ${inv.invoiceNumber} saved`);
          this.router.navigate(['/bills', inv.id]);
        } else {
          this.notify.ok(`Draft ${inv.invoiceNumber} saved`);
          this.existing.set(inv);
          if (!ex) this.router.navigate(['/bills', inv.id, 'edit'], { replaceUrl: true });
        }
      },
      error: e => { this.notify.error(e); this.saving.set(''); },
    });
  }

  /** Leaves without saving. The route guard asks first if there are unsaved changes. */
  cancel() {
    const ex = this.existing();
    this.router.navigate(ex ? ['/bills', ex.id] : ['/bills']);
  }

  /** Opens the bill as a PDF without saving it. */
  viewBill() {
    const req = this.buildRequest(false);
    if (!req) return;
    this.previewing.set(true);
    this.invoicesApi.previewPdf(req, this.existing()?.id).subscribe({
      next: blob => { this.previewing.set(false); openBlob(blob); },
      error: e => { this.notify.error(e); this.previewing.set(false); },
    });
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(e: BeforeUnloadEvent) {
    if (this.dirty) e.preventDefault();
  }

  /** Used by the route's canDeactivate guard. */
  canLeave(): boolean {
    return !this.dirty || confirm('You have unsaved changes on this bill. Leave without saving?');
  }
}
