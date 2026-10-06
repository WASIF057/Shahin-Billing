import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { PortalApi } from '../../core/api.service';
import { OrderPriority, PortalCatalogItem, PortalProfile } from '../../core/models';
import { Notify } from '../../core/notify.service';

/** Order products: the ordering website for a client. Product names and quantities only: no prices anywhere. */
@Component({
  selector: 'app-portal-order',
  imports: [FormsModule, ButtonModule, IconFieldModule, InputIconModule, InputNumberModule, InputTextModule, SelectModule, TextareaModule],
  template: `
    <div class="page wide">
      <div class="page-head">
        <div>
          <h1>Order products</h1>
          <p>Choose what you need and how many. We’ll confirm your order by email.</p>
        </div>
      </div>

      @if (loading()) { <div class="panel empty">Loading products…</div> }
      @else if (!catalog().length) {
        <div class="panel empty"><h3>No products to order yet</h3><p>Please check again soon, or contact us.</p></div>
      } @else {
        <div class="layout">
          <div class="products">
            <div class="panel top">
              <p-iconfield>
                <p-inputicon styleClass="pi pi-search" />
                <input pInputText type="text" placeholder="Search products" [ngModel]="search()" (ngModelChange)="search.set($event)" />
              </p-iconfield>
            </div>

            @for (g of groups(); track g.type) {
              <section class="panel group">
                <h2>{{ g.type }}</h2>
                @for (p of g.items; track p.itemId) {
                  <div class="row" [class.on]="(qty()[p.itemId] ?? 0) > 0">
                    <div class="name">
                      <strong>{{ p.name }}</strong>
                      @if (detail(p)) { <span class="muted small">{{ detail(p) }}</span> }
                    </div>
                    <div class="qty">
                      <p-inputnumber [ngModel]="qty()[p.itemId] ?? null" (ngModelChange)="setQty(p.itemId, $event)" [min]="0" [max]="10000"
                                     [maxFractionDigits]="2" placeholder="0" [suffix]="' ' + p.unit" [showButtons]="true" buttonLayout="horizontal"
                                     incrementButtonIcon="pi pi-plus" decrementButtonIcon="pi pi-minus" inputStyleClass="qty-input" [step]="1" />
                    </div>
                  </div>
                }
              </section>
            } @empty {
              <div class="panel empty">No product matches “{{ search() }}”.</div>
            }
          </div>

          <aside class="cart" id="cart">
            <div class="panel">
              <h2>Your order</h2>
              @if (chosen().length) {
                <ul>
                  @for (c of chosen(); track c.itemId) {
                    <li>
                      <span>{{ c.label }}</span>
                      <span class="q">{{ c.qty }} {{ c.unit }}</span>
                      <button type="button" class="x" (click)="setQty(c.itemId, null)" aria-label="Remove">×</button>
                    </li>
                  }
                </ul>
              } @else { <p class="muted small">Nothing chosen yet. Set a quantity next to the products you need.</p> }

              @if (profile()?.cities?.length && profile()!.cities.length > 1) {
                <div class="field">
                  <label for="ocity">Which of your locations is this for?</label>
                  <p-select inputId="ocity" [options]="profile()!.cities" [(ngModel)]="city" [fluid]="true" appendTo="body" />
                </div>
              }

              <div class="field">
                <label for="oprio">How soon do you need it?</label>
                <p-select inputId="oprio" [options]="priorities" optionLabel="label" optionValue="value" [(ngModel)]="priority" [fluid]="true" appendTo="body" />
              </div>

              <div class="field">
                <label for="onote">Note (optional)</label>
                <textarea pTextarea id="onote" rows="3" maxlength="500" [(ngModel)]="note" placeholder="Anything we should know, for example delivery day"></textarea>
              </div>

              <button pButton type="button" icon="pi pi-send" [label]="'Place order' + (chosen().length ? ' (' + chosen().length + ')' : '')" [fluid]="true"
                      [disabled]="!chosen().length" [loading]="placing()" (click)="confirmPlace()"></button>
            </div>
          </aside>
        </div>

        <div class="basket">
          <span><strong>{{ chosen().length }}</strong> {{ chosen().length === 1 ? 'product' : 'products' }} chosen</span>
          <button pButton type="button" label="Review & order" icon="pi pi-arrow-down" [disabled]="!chosen().length" (click)="toCart()"></button>
        </div>
      }
    </div>
  `,
  styles: `
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 340px; gap: 20px; align-items: start; }
    .products { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
    .top { padding: 12px 16px; }
    .top input { width: 100%; }
    .group { padding: 16px 18px; }
    .group h2 { margin-bottom: 8px; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 14px; padding: 10px 8px; border-top: 1px solid var(--line); border-radius: 8px; }
    .row.on { background: var(--navy-soft); }
    .name { display: flex; flex-direction: column; min-width: 0; }
    .qty { width: 190px; flex-shrink: 0; }
    :host ::ng-deep .qty-input { text-align: center; width: 100%; }
    .cart { position: sticky; top: 16px; }
    .cart .panel { display: flex; flex-direction: column; gap: 14px; }
    .cart h2 { margin: 0; }
    .cart ul { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 6px; }
    .cart li { display: flex; align-items: center; gap: 8px; font-size: 0.9rem; }
    .cart li span:first-child { flex: 1; min-width: 0; }
    .q { font-weight: 700; white-space: nowrap; }
    .x { border: 0; background: none; font-size: 1.2rem; line-height: 1; color: var(--muted); cursor: pointer; }
    .x:hover { color: var(--bad); }
    .basket { display: none; }
    @media (max-width: 1000px) { .layout { grid-template-columns: 1fr; } .cart { position: static; } .qty { width: 170px; } }
    /* Phones: the quantity sits under the product name, and a bar stays at the bottom so the order is always one tap away */
    @media (max-width: 640px) {
      .row { flex-direction: column; align-items: stretch; gap: 8px; }
      .qty { width: 100%; }
      .basket { display: flex; position: fixed; left: 0; right: 0; bottom: 0; z-index: 20; align-items: center; justify-content: space-between; gap: 12px;
                padding: 10px 14px calc(10px + env(safe-area-inset-bottom)); background: #fff; border-top: 1px solid var(--line); box-shadow: 0 -8px 24px rgba(15, 25, 50, 0.12); }
      :host { display: block; padding-bottom: 70px; }
    }
  `,
})
export class PortalOrderPage implements OnInit {
  private api = inject(PortalApi);
  private router = inject(Router);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);

  catalog = signal<PortalCatalogItem[]>([]);
  profile = signal<PortalProfile | null>(null);
  loading = signal(true);
  placing = signal(false);
  search = signal('');
  qty = signal<Record<string, number | undefined>>({});
  city = '';
  note = '';
  priority: OrderPriority = 'Normal';
  priorities = [{ label: 'Normal', value: 'Normal' }, { label: 'Soon (high priority)', value: 'High' }, { label: 'Urgent', value: 'Urgent' }];

  groups = computed(() => {
    const q = this.search().trim().toLowerCase();
    const map = new Map<string, PortalCatalogItem[]>();
    for (const p of this.catalog()) {
      if (q && !`${p.label} ${p.type}`.toLowerCase().includes(q)) continue;
      const key = p.type || 'Other products';
      (map.get(key) ?? map.set(key, []).get(key)!).push(p);
    }
    return [...map].map(([type, items]) => ({ type, items }));
  });

  chosen = computed(() => this.catalog()
    .filter(p => (this.qty()[p.itemId] ?? 0) > 0)
    .map(p => ({ itemId: p.itemId, label: p.label, unit: p.unit, qty: this.qty()[p.itemId] ?? 0 })));

  /** What tells two products of the same type apart: variant, cloth, colour and size. */
  detail(p: PortalCatalogItem) { return [p.variant, p.cloth, p.colour, p.size].filter(Boolean).join(' · '); }

  ngOnInit() {
    this.api.profile().subscribe({ next: p => { this.profile.set(p); this.city = p.cities[0] ?? ''; }, error: e => this.notify.error(e) });
    this.api.catalog().subscribe({
      next: c => { this.catalog.set(c); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  toCart() { document.getElementById('cart')?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }

  setQty(id: string, value: number | null) {
    this.qty.update(q => {
      const next = { ...q };
      if (value && value > 0) next[id] = value; else delete next[id];
      return next;
    });
  }

  confirmPlace() {
    const n = this.chosen().length;
    this.confirm.confirm({
      header: 'Place this order?',
      message: `Send an order for ${n} product${n === 1 ? '' : 's'}? We’ll email you a copy.`,
      acceptLabel: 'Place order', rejectLabel: 'Not yet',
      accept: () => this.place(),
    });
  }

  private place() {
    this.placing.set(true);
    this.api.place({
      lines: this.chosen().map(c => ({ itemId: c.itemId, quantity: c.qty })),
      city: this.city, note: this.note, priority: this.priority,
    }).subscribe({
      next: o => {
        this.placing.set(false);
        this.notify.ok(`Order ${o.orderNumber} placed. We’ve emailed you a copy.`);
        this.router.navigate(['/portal/orders']);
      },
      error: e => { this.notify.error(e); this.placing.set(false); },
    });
  }
}
