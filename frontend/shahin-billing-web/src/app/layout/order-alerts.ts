import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { OrderAlerts } from '../core/order-alerts.service';

/** The popup cards for new orders, in the top-right corner of every owner and staff screen. */
@Component({
  selector: 'app-order-alerts',
  imports: [ButtonModule],
  template: `
    <div class="alerts" aria-live="assertive">
      @if (alerts.waiting() > 0) {
        <div class="card calm">
          <i class="pi pi-inbox icon"></i>
          <div class="body">
            <strong>{{ alerts.waiting() }} {{ alerts.waiting() === 1 ? 'order is' : 'orders are' }} waiting</strong>
            <div class="actions">
              <button pButton type="button" label="View orders" size="small" (click)="open()"></button>
              <button pButton type="button" label="Later" size="small" [text]="true" (click)="alerts.dismissAll()"></button>
            </div>
          </div>
        </div>
      }

      @for (o of alerts.popups(); track o.id) {
        <div class="card">
          <i class="pi pi-bell icon ring"></i>
          <div class="body">
            <strong>New order {{ o.orderNumber }}</strong>@if (o.priority !== 'Normal') { <span class="prio" [class.urgent]="o.priority === 'Urgent'">{{ o.priority }}</span> }
            <div class="who">{{ o.clientName }}{{ o.city ? ' · ' + o.city : '' }}</div>
            <ul>
              @for (l of o.lines.slice(0, 3); track l.itemId) { <li>{{ l.label }} <b>× {{ l.quantity }}</b></li> }
            </ul>
            @if (o.lines.length > 3) { <div class="more">+ {{ o.lines.length - 3 }} more</div> }
            <div class="actions">
              <button pButton type="button" label="View orders" size="small" (click)="open(o.id)"></button>
              <button pButton type="button" label="Dismiss" size="small" [text]="true" (click)="alerts.dismiss(o.id)"></button>
            </div>
          </div>
        </div>
      }

      @if ((alerts.popups().length || alerts.waiting()) && alerts.soundOn() && alerts.soundBlocked()) {
        <div class="hint"><i class="pi pi-volume-off"></i> Your browser is holding back the ring. Click anywhere on the page once to allow the sound.</div>
      }
    </div>
  `,
  styles: `
    .alerts { position: fixed; top: 14px; right: 14px; z-index: 3000; display: flex; flex-direction: column; gap: 10px; width: min(380px, calc(100vw - 28px)); pointer-events: none; }
    .alerts > * { pointer-events: auto; }
    .card { display: flex; gap: 12px; background: #fff; border: 1px solid var(--line); border-left: 6px solid var(--accent); border-radius: 14px;
            padding: 14px 16px; box-shadow: 0 14px 36px rgba(15, 25, 50, 0.28); animation: slide 0.3s ease both; }
    .card.calm { border-left-color: var(--navy); }
    .icon { font-size: 1.5rem; color: var(--accent); margin-top: 2px; }
    .calm .icon { color: var(--navy); }
    .ring { animation: ring 1.1s ease-in-out 3; transform-origin: 50% 0; }
    .body { flex: 1; min-width: 0; }
    .prio { margin-left: 8px; font-size: 0.72rem; font-weight: 800; text-transform: uppercase; padding: 2px 8px; border-radius: 999px; background: #fdecc8; color: var(--warn); }
    .prio.urgent { background: #fbdcd9; color: var(--bad); }
    .who { font-weight: 700; margin-top: 2px; }
    ul { margin: 6px 0 0; padding-left: 18px; font-size: 0.88rem; }
    .more { font-size: 0.82rem; color: var(--muted); margin-top: 2px; }
    .actions { display: flex; gap: 6px; margin-top: 10px; }
    .hint { background: #fff8e6; border: 1px solid #f1d28a; border-radius: 10px; padding: 8px 12px; font-size: 0.82rem; }
    @keyframes slide { from { opacity: 0; transform: translateX(24px); } to { opacity: 1; transform: none; } }
    @keyframes ring { 0%, 100% { transform: rotate(0); } 15% { transform: rotate(18deg); } 30% { transform: rotate(-16deg); } 45% { transform: rotate(12deg); } 60% { transform: rotate(-8deg); } 75% { transform: rotate(4deg); } }
    @media (prefers-reduced-motion: reduce) { .card, .ring { animation: none; } }
  `,
})
export class OrderAlertsComponent {
  alerts = inject(OrderAlerts);
  private router = inject(Router);

  open(id?: string) {
    if (id) this.alerts.dismiss(id); else this.alerts.dismissAll();
    this.router.navigate(['/orders']);
  }
}
