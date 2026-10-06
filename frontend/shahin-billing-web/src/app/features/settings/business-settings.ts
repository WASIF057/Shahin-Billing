import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { PasswordModule } from 'primeng/password';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageModule } from 'primeng/message';
import { BusinessApi, MiscApi } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { fieldErrors } from '../../core/errors';
import { formatInvoiceNumber, isValidGstin, STATES, stateFromGstin } from '../../core/gst';
import { Business } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { ImagePicker } from '../../shared/image-picker';

@Component({
  selector: 'app-business-settings',
  imports: [FormsModule, ButtonModule, InputTextModule, InputNumberModule, SelectModule, TextareaModule,
            PasswordModule, MessageModule, ToggleSwitchModule, ImagePicker],
  templateUrl: './business-settings.html',
})
export class BusinessSettings implements OnInit {
  private api = inject(BusinessApi);
  private misc = inject(MiscApi);
  private auth = inject(AuthService);
  notify = inject(Notify);
  welcome = inject(ActivatedRoute).snapshot.queryParamMap.has('welcome');

  states = STATES;
  biz = signal<Business | null>(null);
  saving = signal(false);
  errors = signal<Record<string, string>>({});
  /** bumps on every edit so computed() values re-run (the object itself is mutated by ngModel) */
  tick = signal(0);

  nonGstExample = computed(() => {
    this.tick();
    const b = this.biz();
    return b ? formatInvoiceNumber(b.invoiceNumbering, 1, true) : '';
  });

  numberExample = computed(() => {
    this.tick();
    const b = this.biz();
    return b ? formatInvoiceNumber(b.invoiceNumbering, 1) : '';
  });

  pw = { current: '', next: '' };

  ngOnInit() {
    this.api.get().subscribe({ next: b => this.biz.set(b), error: e => this.notify.error(e) });
  }

  gstinChanged(b: Business) {
    b.gstin = (b.gstin || '').toUpperCase().trim();
    const code = stateFromGstin(b.gstin);
    if (isValidGstin(b.gstin) && code) b.address.stateCode = code;
    if (b.gstin.length === 15 && !b.pan) b.pan = b.gstin.slice(2, 12);
    this.tick.update(v => v + 1);
  }

  gstinInvalid(b: Business) { return !!b.gstin && b.gstin.length >= 15 && !isValidGstin(b.gstin); }

  save() {
    const b = this.biz();
    if (!b) return;
    this.saving.set(true);
    this.errors.set({});
    if (b.bank.ifsc) b.bank.ifsc = b.bank.ifsc.toUpperCase().trim();
    this.api.update(b).subscribe({
      next: saved => {
        this.biz.set(saved);
        this.auth.setBusinessName(saved.name);
        this.notify.ok('Business details saved');
        this.saving.set(false);
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  changePassword() {
    this.auth.changePassword(this.pw.current, this.pw.next).subscribe({
      next: () => { this.pw = { current: '', next: '' }; this.notify.ok('Password changed. Please log in again.'); this.auth.logout(); },
      error: e => this.notify.error(e),
    });
  }

  seed() {
    this.misc.seed().subscribe({
      next: r => { this.notify.info(r.message); this.ngOnInit(); },
      error: e => this.notify.error(e),
    });
  }
}
