import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { MessageModule } from 'primeng/message';
import { InvoicesApi, openBlob, saveBlob } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { toIsoDate } from '../../core/gst';
import { Invoice } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';
import { StatusTag } from '../../shared/status-tag';

@Component({
  selector: 'app-bill-view',
  imports: [DatePipe, FormsModule, RouterLink, TooltipModule, ButtonModule, CheckboxModule, DatePickerModule, DialogModule, InputNumberModule,
            InputTextModule, SelectModule, TextareaModule, MessageModule, InrPipe, StatusTag],
  templateUrl: './bill-view.html',
  styleUrl: './bill-view.scss',
})
export class BillView implements OnInit {
  id = input.required<string>();
  private api = inject(InvoicesApi);
  private router = inject(Router);
  private confirm = inject(ConfirmationService);
  private notify = inject(Notify);
  auth = inject(AuthService);

  inv = signal<Invoice | null>(null);
  busy = signal(false);
  cancelled = computed(() => this.inv()?.status === 'Cancelled');

  copies = { Original: true, Duplicate: false, Triplicate: false };

  payOpen = signal(false);
  modes = ['Bank Transfer', 'UPI', 'Cash', 'Cheque'];
  pay = { date: new Date(), amount: 0, mode: 'Bank Transfer', reference: '', note: '' };

  cancelOpen = signal(false);
  cancelReason = '';

  ngOnInit() { this.load(); }

  load() {
    this.api.get(this.id()).subscribe({ next: i => this.inv.set(i), error: e => this.notify.error(e) });
  }

  private selectedCopies() {
    return (Object.keys(this.copies) as (keyof typeof this.copies)[]).filter(k => this.copies[k]);
  }

  download() {
    const i = this.inv()!;
    this.api.pdf(i.id, this.selectedCopies(), true).subscribe({
      next: b => saveBlob(b, `${i.invoiceNumber.replace(/\//g, '-')}.pdf`), error: e => this.notify.error(e),
    });
  }

  print() {
    this.api.pdf(this.inv()!.id, this.selectedCopies()).subscribe({ next: b => openBlob(b), error: e => this.notify.error(e) });
  }

  finalize() {
    this.api.finalize(this.inv()!.id).subscribe({
      next: i => { this.inv.set(i); this.notify.ok('Bill finalized'); }, error: e => this.notify.error(e),
    });
  }

  duplicate() {
    this.api.duplicate(this.inv()!.id).subscribe({
      next: i => { this.notify.ok(`Draft ${i.invoiceNumber} created`); this.router.navigate(['/bills', i.id, 'edit']); },
      error: e => this.notify.error(e),
    });
  }

  openPayment() {
    const i = this.inv()!;
    this.pay = { date: new Date(), amount: Math.max(i.balanceDue, 0), mode: 'Bank Transfer', reference: '', note: '' };
    this.payOpen.set(true);
  }

  savePayment() {
    this.busy.set(true);
    this.api.addPayment(this.inv()!.id, { ...this.pay, date: toIsoDate(this.pay.date)! }).subscribe({
      next: i => { this.inv.set(i); this.payOpen.set(false); this.busy.set(false); this.notify.ok('Payment recorded'); },
      error: e => { this.notify.error(e); this.busy.set(false); },
    });
  }

  receipt(paymentId: string) {
    const i = this.inv()!;
    const n = i.payments.findIndex(p => p.id === paymentId) + 1;
    this.api.receipt(i.id, paymentId).subscribe({
      next: b => saveBlob(b, `Receipt-${i.invoiceNumber.replace(/\//g, '-')}-R${n}.pdf`),
      error: e => this.notify.error(e),
    });
  }

  removePayment(paymentId: string) {
    this.confirm.confirm({
      header: 'Remove payment?',
      message: 'This payment will be removed and the balance updated.',
      acceptLabel: 'Remove payment', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.removePayment(this.inv()!.id, paymentId).subscribe({
        next: i => { this.inv.set(i); this.notify.ok('Payment removed'); }, error: e => this.notify.error(e),
      }),
    });
  }

  confirmCancel() {
    this.busy.set(true);
    this.api.cancel(this.inv()!.id, this.cancelReason).subscribe({
      next: i => { this.inv.set(i); this.cancelOpen.set(false); this.busy.set(false); this.notify.ok('Bill cancelled'); },
      error: e => { this.notify.error(e); this.busy.set(false); },
    });
  }
}
