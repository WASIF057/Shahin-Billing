import { Component, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { PortalApi } from '../../core/api.service';
import { PortalOrder } from '../../core/models';
import { Notify } from '../../core/notify.service';

/** My orders: a client's own orders and where they stand. No prices. */
@Component({
  selector: 'app-my-orders',
  imports: [DatePipe, RouterLink, ButtonModule, TableModule, TagModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>My orders</h1>
          <p>The orders you have placed and where they stand.</p>
        </div>
        <div class="actions"><a pButton routerLink="/portal" label="Order products" icon="pi pi-shopping-cart"></a></div>
      </div>

      <div class="panel">
        <p-table [value]="orders()" [loading]="loading()" dataKey="id" [rowHover]="true">
          <ng-template #header>
            <tr><th style="width: 3rem"></th><th>Order</th><th>Placed</th><th>Location</th><th>Products</th><th>Status</th></tr>
          </ng-template>
          <ng-template #body let-o let-expanded="expanded">
            <tr>
              <td><button pButton type="button" [text]="true" size="small" [icon]="expanded ? 'pi pi-chevron-down' : 'pi pi-chevron-right'" [pRowToggler]="o" aria-label="Show products"></button></td>
              <td class="no">{{ o.orderNumber }} @if (o.priority !== 'Normal') { <p-tag [severity]="o.priority === 'Urgent' ? 'danger' : 'warn'" [value]="o.priority" [rounded]="true" /> }</td>
              <td class="muted">{{ o.placedAt | date: 'dd-MM-yyyy hh:mm a' }}</td>
              <td>{{ o.city || '—' }}</td>
              <td>{{ o.lines.length }} {{ o.lines.length === 1 ? 'product' : 'products' }}</td>
              <td><p-tag [severity]="severity(o.status)" [value]="label(o.status)" [rounded]="true" /></td>
            </tr>
          </ng-template>
          <ng-template #expandedrow let-o>
            <tr>
              <td colspan="6">
                <div class="detail">
                  <ul>
                    @for (l of o.lines; track l.label) { <li><span>{{ l.label }}</span><strong>{{ l.quantity }} {{ l.unit }}</strong></li> }
                  </ul>
                  @if (o.note) { <p class="small"><strong>Your note:</strong> {{ o.note }}</p> }
                  @if (o.status === 'Cancelled' && o.cancelReason) { <p class="small muted">{{ o.cancelReason }}</p> }
                  @if (o.status === 'New') {
                    <button pButton type="button" label="Cancel this order" severity="danger" [outlined]="true" size="small" (click)="cancel(o)"></button>
                  }
                </div>
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="6">
              <div class="empty"><h3>No orders yet</h3><p>When you place an order, it will show up here.</p>
                <a pButton routerLink="/portal" label="Order products" icon="pi pi-shopping-cart"></a></div>
            </td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
  styles: `
    .no { font-weight: 800; }
    .detail { padding: 6px 4px 10px; display: flex; flex-direction: column; gap: 8px; }
    .detail ul { list-style: none; margin: 0; padding: 0; max-width: 520px; }
    .detail li { display: flex; justify-content: space-between; gap: 16px; padding: 4px 0; border-bottom: 1px solid var(--line); }
    .detail p { margin: 0; }
    .detail button { align-self: flex-start; }
  `,
})
export class MyOrdersPage implements OnInit {
  private api = inject(PortalApi);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);

  orders = signal<PortalOrder[]>([]);
  loading = signal(true);

  /** Plain words for a client. */
  label(s: string) { return ({ New: 'Received', Accepted: 'Accepted', Billed: 'Billed', Cancelled: 'Cancelled' } as Record<string, string>)[s] ?? s; }
  severity(s: string): 'info' | 'success' | 'warn' | 'danger' | 'secondary' {
    return ({ New: 'info', Accepted: 'warn', Billed: 'success', Cancelled: 'danger' } as Record<string, 'info' | 'success' | 'warn' | 'danger'>)[s] ?? 'secondary';
  }

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.orders().subscribe({
      next: o => { this.orders.set(o); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  cancel(o: PortalOrder) {
    this.confirm.confirm({
      header: 'Cancel this order?',
      message: `Cancel order ${o.orderNumber}? You can only do this until we pick it up.`,
      acceptLabel: 'Cancel order', rejectLabel: 'Keep it',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.cancel(o.id).subscribe({
        next: () => { this.notify.ok('Order cancelled'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }
}
