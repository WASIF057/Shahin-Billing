import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { CheckboxModule } from 'primeng/checkbox';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService } from 'primeng/api';
import { AuthService } from '../../core/auth.service';
import { ClientsApi } from '../../core/api.service';
import { fieldErrors } from '../../core/errors';
import { isValidGstin, STATES, stateFromGstin, stateName } from '../../core/gst';
import { Client, ClientSpecialRate, emptyAddress, PortalAccess } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';

const newClient = (): Client => ({
  id: '', name: '', gstin: '', pan: '', contactPerson: '', phone: '', email: '',
  billingAddress: emptyAddress(), shippingSameAsBilling: true, shippingAddress: emptyAddress(), cities: [], notes: '', billedWithoutGst: false, isActive: true,
});

@Component({
  selector: 'app-clients',
  imports: [FormsModule, ButtonModule, DialogModule, InputTextModule, SelectModule, TableModule, TagModule, TextareaModule,
            CheckboxModule, AutoCompleteModule, ToggleSwitchModule, IconFieldModule, InputIconModule, TooltipModule, InrPipe],
  templateUrl: './clients.html',
  styles: `
    .toolbar { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; margin-bottom: 12px; }
    .name { font-weight: 600; }
    .portal-row { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 8px 0; border-bottom: 1px solid var(--line); }
    .portal-actions { display: flex; gap: 8px; flex-wrap: wrap; margin-top: 14px; }
    tr.inactive td { opacity: 0.6; }
    .row-actions { text-align: right; white-space: nowrap; }
    .section-title { margin: 20px 0 10px; padding-top: 14px; border-top: 1px solid var(--line); }
    .rates { width: 100%; border-collapse: collapse; }
    .rates td, .rates th { padding: 6px 4px; border-bottom: 1px solid var(--line); text-align: left; }
    .rates th { font-size: 0.8rem; color: var(--muted); }
  `,
})
export class ClientsPage implements OnInit {
  private api = inject(ClientsApi);
  private notify = inject(Notify);
  auth = inject(AuthService);
  private confirm = inject(ConfirmationService);
  states = STATES;
  stateName = stateName;

  clients = signal<Client[]>([]);
  search = signal('');
  showInactive = signal(false);
  loading = signal(true);
  filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.clients().filter(c =>
      (this.showInactive() || c.isActive) &&
      (!q || `${c.name} ${c.gstin} ${c.contactPerson} ${c.phone} ${(c.cities ?? []).join(' ')}`.toLowerCase().includes(q)));
  });

  dialogOpen = signal(false);
  editing = signal<Client>(newClient());
  specialRates = signal<ClientSpecialRate[]>([]);
  saving = signal(false);
  errors = signal<Record<string, string>>({});

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: c => { this.clients.set(c); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  open(c?: Client) {
    this.errors.set({});
    this.specialRates.set([]);
    const copy = c ? structuredClone(c) : newClient();
    // Older clients only had one city on the address: show it as their city
    if (!copy.cities?.length) copy.cities = copy.billingAddress.city ? [copy.billingAddress.city] : [];
    this.editing.set(copy);
    if (c) this.api.specialRates(c.id).subscribe(r => this.specialRates.set(r));
    this.dialogOpen.set(true);
  }

  gstinChanged(c: Client) {
    c.gstin = (c.gstin || '').toUpperCase().trim();
    if (isValidGstin(c.gstin)) {
      c.billingAddress.stateCode = stateFromGstin(c.gstin);
      if (!c.pan) c.pan = c.gstin.slice(2, 12);
    }
  }

  gstinInvalid(c: Client) { return !!c.gstin && c.gstin.length >= 15 && !isValidGstin(c.gstin); }

  save() {
    const c = this.editing();
    this.saving.set(true);
    this.errors.set({});
    (c.id ? this.api.update(c) : this.api.create(c)).subscribe({
      next: () => {
        this.notify.ok(c.id ? 'Client updated' : 'Client added');
        this.dialogOpen.set(false);
        this.saving.set(false);
        this.load();
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  remove(c: Client) {
    this.confirm.confirm({
      header: 'Delete client?',
      message: `Delete ${c.name}? This can't be undone, and any special rates for them are removed. A client with bills can't be deleted; hide them instead.`,
      acceptLabel: 'Delete client', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(c.id).subscribe({
        next: () => { this.notify.ok('Client deleted'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }

  // ---- ordering login for a client (owner only) ----
  portalOpen = signal(false);
  portalClient = signal<Client | null>(null);
  portal = signal<PortalAccess | null>(null);
  portalBusy = signal(false);

  openPortal(c: Client) {
    this.portalClient.set(c);
    this.portal.set(null);
    this.portalOpen.set(true);
    this.api.portal(c.id).subscribe({ next: p => this.portal.set(p), error: e => this.notify.error(e) });
  }

  createPortal() {
    const c = this.portalClient()!;
    this.portalBusy.set(true);
    this.api.createPortal(c.id).subscribe({
      next: p => {
        this.portal.set(p);
        this.portalBusy.set(false);
        if (p.warning) this.notify.warn(p.warning); else this.notify.ok(`Ordering login created. An invitation was emailed to ${p.email}.`);
      },
      error: e => { this.notify.error(e); this.portalBusy.set(false); },
    });
  }

  invitePortal() {
    this.portalBusy.set(true);
    this.api.invitePortal(this.portalClient()!.id).subscribe({
      next: () => { this.portalBusy.set(false); this.notify.ok('Invitation emailed again.'); },
      error: e => { this.notify.error(e); this.portalBusy.set(false); },
    });
  }

  togglePortal() {
    const c = this.portalClient()!;
    const on = !this.portal()!.isActive;
    this.api.setPortalActive(c.id, on).subscribe({
      next: () => { this.portal.update(p => p && { ...p, isActive: on }); this.notify.ok(on ? 'Ordering login switched on.' : 'Ordering login switched off. Any open session ends now.'); },
      error: e => this.notify.error(e),
    });
  }

  removePortal() {
    const c = this.portalClient()!;
    this.confirm.confirm({
      header: 'Remove the ordering login?',
      message: `${c.name} will no longer be able to log in. Their past orders stay in your Orders list. You can create a login again later.`,
      acceptLabel: 'Remove login', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.removePortal(c.id).subscribe({
        next: () => { this.portal.set({ exists: false, email: null, isActive: false, createdAt: null, warning: null }); this.notify.ok('Ordering login removed.'); },
        error: e => this.notify.error(e),
      }),
    });
  }

  /** Enter inside a chip box adds a chip. It must not also submit the form, which would save and close the dialog. */
  keepOpenOnEnter(e: Event) {
    if ((e.target as HTMLElement | null)?.closest('p-autocomplete')) e.preventDefault();
  }

  toggleActive(c: Client) {
    this.api.setActive(c.id, !c.isActive).subscribe({
      next: () => { this.notify.ok(c.isActive ? 'Client hidden from billing' : 'Client active again'); this.load(); },
      error: e => this.notify.error(e),
    });
  }
}
