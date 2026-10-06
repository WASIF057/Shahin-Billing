import { Component, computed, input } from '@angular/core';
import { TagModule } from 'primeng/tag';
import { InvoiceStatus, PaymentStatus } from '../core/models';

/** Colour-coded badge for bill status / payment status. */
@Component({
  selector: 'app-status-tag',
  imports: [TagModule],
  template: `<p-tag [value]="label()" [severity]="severity()" [rounded]="true" />`,
})
export class StatusTag {
  status = input.required<InvoiceStatus>();
  payment = input<PaymentStatus | null>(null);

  label = computed(() => {
    if (this.status() === 'Cancelled') return 'Cancelled';
    if (this.status() === 'Draft') return 'Draft';
    return { Unpaid: 'Unpaid', PartlyPaid: 'Part paid', Paid: 'Paid' }[this.payment() ?? 'Unpaid'];
  });

  severity = computed<'success' | 'warn' | 'danger' | 'secondary' | 'info'>(() => {
    if (this.status() === 'Cancelled') return 'danger';
    if (this.status() === 'Draft') return 'secondary';
    return this.payment() === 'Paid' ? 'success' : this.payment() === 'PartlyPaid' ? 'warn' : 'info';
  });
}
