import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService } from 'primeng/api';
import { CatalogApi, ClientsApi, ItemsApi, saveBlob } from '../../core/api.service';
import { RouterLink } from '@angular/router';
import { fieldErrors } from '../../core/errors';
import { GST_RATES, UNITS } from '../../core/gst';
import { Client, Item, ProductType } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';

interface ClientOption { id: string; name: string; disabled: boolean; }

const newItem = (): Item => ({
  id: '', typeId: '', typeName: '', variant: '', cloth: '', colour: '', size: '', name: '', sizeOrVariant: '', description: '', hsnCode: '', unit: 'PCS',
  gstRate: 18, defaultRate: 0, specialRates: [], isActive: true,
});

@Component({
  selector: 'app-items',
  imports: [FormsModule, RouterLink, ButtonModule, DialogModule, InputTextModule, InputNumberModule, SelectModule, MultiSelectModule,
            TableModule, TagModule, ToggleSwitchModule, IconFieldModule, InputIconModule, TooltipModule, InrPipe],
  templateUrl: './items.html',
  styleUrl: './items.scss',
})
export class ItemsPage implements OnInit {
  private api = inject(ItemsApi);
  private clientsApi = inject(ClientsApi);
  private catalogApi = inject(CatalogApi);
  private confirm = inject(ConfirmationService);
  private notify = inject(Notify);

  gstRates = GST_RATES.map(r => ({ label: `${r}%`, value: r }));
  units = UNITS;

  items = signal<Item[]>([]);
  clients = signal<Client[]>([]);
  types = signal<ProductType[]>([]);
  search = signal('');
  showInactive = signal(false);
  /** '' = all types, 'none' = items with no type, or a type id */
  typeFilter = signal('');
  exporting = signal(false);
  typeFilterOptions = computed(() => [
    { label: 'All types', value: '' },
    ...this.types().map(t => ({ label: t.name, value: t.id })),
    { label: 'No type', value: 'none' },
  ]);
  loading = signal(true);

  filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    return this.items().filter(i =>
      (this.showInactive() || i.isActive) &&
      (!this.typeFilter() || (this.typeFilter() === 'none' ? !i.typeId : i.typeId === this.typeFilter())) &&
      (!q || `${i.name} ${i.sizeOrVariant} ${i.typeName}`.toLowerCase().includes(q)));
  });

  // dialog state
  dialogOpen = signal(false);
  editing = signal<Item>(newItem());
  rowOptions = signal<(ClientOption[] | undefined)[]>([]);
  saving = signal(false);
  errors = signal<Record<string, string>>({});

  ngOnInit() {
    this.load();
    this.clientsApi.list(undefined, true).subscribe(c => this.clients.set(c));
    this.catalogApi.list().subscribe(t => this.types.set(t));
  }

  // ---- type-driven dropdowns (Item setup) ----
  typeOf(it: Item): ProductType | undefined { return this.types().find(t => t.id === it.typeId); }

  /** A dropdown list that still includes the item's saved value, even if the type's list changed since. */
  opts(list: string[] | undefined, current: string): string[] {
    const l = list ?? [];
    return current && !l.includes(current) ? [...l, current] : l;
  }

  typeChanged(it: Item) {
    const t = this.typeOf(it);
    it.typeName = t?.name ?? '';
    if (!t) return;
    it.name = t.name;   // typed items are called after their type
    if (!t.sizes.includes(it.size)) it.size = '';
    if (!t.usesVariants || !t.variants.includes(it.variant)) it.variant = '';
    if (!(t.clothTypes ?? []).includes(it.cloth)) it.cloth = '';
    this.clothChanged(it);
  }

  /** The colours of the cloth chosen on this item (each cloth has its own list in Item setup). */
  coloursFor(t: ProductType, cloth: string): string[] {
    return t.clothColours?.find(c => c.cloth.toLowerCase() === (cloth ?? '').toLowerCase())?.colours ?? [];
  }

  /** A new cloth means the colour has to be chosen again, from that cloth's colours. */
  clothChanged(it: Item) {
    const t = this.typeOf(it);
    if (!t || !it.cloth || !this.coloursFor(t, it.cloth).includes(it.colour)) it.colour = '';
  }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: list => { this.items.set(list); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  open(item?: Item) {
    this.errors.set({});
    this.editing.set(item ? structuredClone(item) : newItem());
    this.refreshRowOptions();
    this.dialogOpen.set(true);
  }

  addSpecialRate() {
    const it = this.editing();
    it.specialRates.push({ rate: it.defaultRate, clientIds: [] });
    this.refreshRowOptions();
  }

  removeSpecialRate(index: number) {
    this.editing().specialRates.splice(index, 1);
    this.refreshRowOptions();
  }

  /** A client already picked in one special-rate row is disabled in all the other rows (rule 4.1). */
  refreshRowOptions() {
    const rows = this.editing().specialRates;
    this.rowOptions.set(rows.map((row, i) => {
      const takenElsewhere = new Set(rows.filter((_, j) => j !== i).flatMap(r => r.clientIds));
      return this.clients().map(c => ({ id: c.id, name: c.name, disabled: takenElsewhere.has(c.id) }));
    }));
  }

  specialCount(i: Item) { return i.specialRates.reduce((n, s) => n + s.clientIds.length, 0); }

  save() {
    const it = this.editing();
    this.saving.set(true);
    this.errors.set({});
    const req = it.id ? this.api.update(it) : this.api.create(it);
    req.subscribe({
      next: () => {
        this.notify.ok(it.id ? 'Item updated' : 'Item added');
        this.dialogOpen.set(false);
        this.saving.set(false);
        this.load();
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  /** Downloads an Excel file of the items (one sheet per type) for the type chosen in the filter. */
  exportItems() {
    this.exporting.set(true);
    const f = this.typeFilter();
    this.api.export(f).subscribe({
      next: blob => {
        const label = f === '' ? 'All' : f === 'none' ? 'No-type' : (this.types().find(t => t.id === f)?.name ?? 'Type');
        saveBlob(blob, `Items-${label.replace(/[^A-Za-z0-9-]+/g, '-')}.xlsx`);
        this.exporting.set(false);
      },
      error: e => { this.notify.error(e); this.exporting.set(false); },
    });
  }

  remove(i: Item) {
    const label = i.sizeOrVariant ? `${i.name} (${i.sizeOrVariant})` : i.name;
    this.confirm.confirm({
      header: 'Delete item?',
      message: `Delete ${label}? This can't be undone. An item that is on a bill can't be deleted; hide it instead.`,
      acceptLabel: 'Delete item', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(i.id).subscribe({
        next: () => { this.notify.ok('Item deleted'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }

  toggleActive(i: Item) {
    this.api.setActive(i.id, !i.isActive).subscribe({
      next: () => { this.notify.ok(i.isActive ? 'Item hidden from billing' : 'Item active again'); this.load(); },
      error: e => this.notify.error(e),
    });
  }
}
