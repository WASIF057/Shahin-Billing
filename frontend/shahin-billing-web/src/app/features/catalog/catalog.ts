import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { CatalogApi } from '../../core/api.service';
import { fieldErrors } from '../../core/errors';
import { ProductType } from '../../core/models';
import { Notify } from '../../core/notify.service';

const newType = (): ProductType => ({ id: '', name: '', usesVariants: false, variants: [], clothTypes: [], clothColours: [], sizes: [] });

/**
 * Item setup: define the types (Bed, Pillow, Cushion, Bolster...) once, with the item names, sizes and (for
 * types like Bed) variants that belong to each. The Items screen then offers these as dropdowns.
 */
@Component({
  selector: 'app-catalog',
  imports: [FormsModule, RouterLink, AutoCompleteModule, ButtonModule, DialogModule, InputTextModule, TableModule,
            ToggleSwitchModule, TooltipModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Item setup</h1>
          <p>Add each type of product once, with its sizes and, where it has them, variants. When you add an item, you just pick from these lists.</p>
        </div>
        <div class="actions">
          <a pButton routerLink="/items" label="Go to items" [outlined]="true" icon="pi pi-box"></a>
          <button pButton type="button" label="Add type" icon="pi pi-plus" (click)="open()"></button>
        </div>
      </div>

      <div class="panel">
        <p-table [value]="types()" [loading]="loading()" dataKey="id" [rowHover]="true">
          <ng-template #header>
            <tr><th>Type</th><th>Variants</th><th>Cloth</th><th>Sizes</th><th></th></tr>
          </ng-template>
          <ng-template #body let-t>
            <tr>
              <td class="type-name">{{ t.name }}</td>
              <td>
                @if (t.usesVariants) {
                  <div class="chips">@for (x of t.variants; track x) { <span class="chip">{{ x }}</span> } @empty { <span class="muted small">None yet</span> }</div>
                } @else { <span class="muted small">Not used</span> }
              </td>
              <td>
                @for (x of t.clothTypes; track x) {
                  <div class="clothrow"><span class="chip">{{ x }}</span> <span class="muted small">{{ coloursText(t, x) }}</span></div>
                } @empty { <span class="muted small">Not used</span> }
              </td>
              <td>
                <div class="chips">@for (x of t.sizes; track x) { <span class="chip">{{ x }}</span> } @empty { <span class="muted small">None yet</span> }</div>
              </td>
              <td class="row-actions">
                <button pButton type="button" icon="pi pi-pencil" [text]="true" pTooltip="Edit" (click)="open(t)" aria-label="Edit"></button>
                <button pButton type="button" icon="pi pi-trash" [text]="true" severity="danger" pTooltip="Delete" (click)="remove(t)" aria-label="Delete"></button>
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="5">
              <div class="empty">
                <h3>No types yet</h3>
                <p>Start with something like Bed or Pillow, then add its sizes (and variants, if it has any).</p>
                <button pButton type="button" label="Add your first type" icon="pi pi-plus" (click)="open()"></button>
              </div>
            </td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <p-dialog [header]="editing().id ? 'Edit type' : 'Add type'" [visible]="dialogOpen()" (visibleChange)="dialogOpen.set($event)"
              [modal]="true" [style]="{ width: '640px' }" [breakpoints]="{ '700px': '96vw' }" [draggable]="false">
      @let t = editing();
      <form (ngSubmit)="save()" (keydown.enter)="keepOpenOnEnter($event)" id="typeForm" class="form">
        <div class="field">
          <label for="tname">Type name</label>
          <input pInputText id="tname" name="name" [(ngModel)]="t.name" required placeholder="Bed, Pillow, Cushion, Bolster" />
          @if (errors()['name']) { <span class="error">{{ errors()['name'] }}</span> }
        </div>

        <label class="row"><p-toggleswitch [(ngModel)]="t.usesVariants" name="usesVariants" /> <span>This type has variants (like Sada, Quilt)</span></label>

        @if (t.usesVariants) {
          <div class="field">
            <label for="tvars">Variants</label>
            <p-autocomplete inputId="tvars" name="variants" [(ngModel)]="t.variants" [multiple]="true" [typeahead]="false"
                            [addOnBlur]="true" placeholder="Type a variant and press Enter" [fluid]="true" />
            <span class="hint">For example Sada, Quilt.</span>
          </div>
        }

        <div class="field">
          <label for="tcloth">Cloth types</label>
          <p-autocomplete inputId="tcloth" name="clothTypes" [(ngModel)]="t.clothTypes" [multiple]="true" [typeahead]="false"
                          [addOnBlur]="true" placeholder="Type a cloth type and press Enter" [fluid]="true" />
          <span class="hint">For example Cotton, Polyester. Leave empty if this type has no cloth choice.</span>
        </div>

        @if (t.clothTypes.length) {
          <div class="colours">
            <label>Colours of each cloth</label>
            @for (c of t.clothTypes; track c) {
              <div class="field">
                <label [for]="'col-' + c">{{ c }} colours</label>
                <p-autocomplete [inputId]="'col-' + c" [name]="'colours-' + c" [ngModel]="coloursOf(t, c)" (ngModelChange)="setColours(t, c, $event)"
                                [multiple]="true" [typeahead]="false" [addOnBlur]="true" [placeholder]="'Type a ' + c + ' colour and press Enter'" [fluid]="true" />
              </div>
            }
            <span class="hint">Each cloth has its own colours (Polyester can have colours that Cotton doesn’t). Leave a cloth empty if it has no colour choice.</span>
          </div>
        }

        <div class="field">
          <label for="tsizes">Sizes</label>
          <p-autocomplete inputId="tsizes" name="sizes" [(ngModel)]="t.sizes" [multiple]="true" [typeahead]="false"
                          [addOnBlur]="true" placeholder="Type a size and press Enter" [fluid]="true" />
          <span class="hint">For example 72x36x6 in, or Small, Medium, Large.</span>
        </div>
      </form>

      <ng-template #footer>
        <button pButton type="button" label="Cancel" [text]="true" (click)="dialogOpen.set(false)"></button>
        <button pButton type="submit" form="typeForm" [label]="t.id ? 'Save type' : 'Add type'" icon="pi pi-check" [loading]="saving()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .type-name { font-weight: 700; }
    .clothrow { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 4px; }
    .colours { display: flex; flex-direction: column; gap: 12px; padding: 12px 14px; background: var(--navy-soft); border-radius: 12px; }
    .colours > label { font-weight: 700; }
    .chips { display: flex; flex-wrap: wrap; gap: 6px; }
    .chip { background: var(--navy-soft); color: var(--navy); border-radius: 999px; padding: 2px 10px; font-size: 0.8rem; font-weight: 600; }
    .row-actions { text-align: right; white-space: nowrap; }
    .form { display: flex; flex-direction: column; gap: 16px; }
    .row { display: flex; align-items: center; gap: 10px; cursor: pointer; }
  `,
})
export class CatalogPage implements OnInit {
  private api = inject(CatalogApi);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);

  types = signal<ProductType[]>([]);
  loading = signal(true);
  dialogOpen = signal(false);
  editing = signal<ProductType>(newType());
  saving = signal(false);
  errors = signal<Record<string, string>>({});

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: list => { this.types.set(list); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  open(t?: ProductType) {
    this.errors.set({});
    const copy = t ? structuredClone(t) : newType();
    copy.clothTypes ??= [];
    copy.clothColours ??= [];
    this.editing.set(copy);
    this.dialogOpen.set(true);
  }

  /** Enter inside a chip box adds a chip. It must not also submit the form, which would save and close the dialog. */
  keepOpenOnEnter(e: Event) {
    if ((e.target as HTMLElement | null)?.closest('p-autocomplete')) e.preventDefault();
  }

  /** The colours box for one cloth. Every cloth gets its own list (made here if it has none yet), so each box has a stable list of its own.
   *  A cloth left with no colours is simply dropped when saved. */
  coloursOf(t: ProductType, cloth: string): string[] {
    let set = t.clothColours.find(c => c.cloth.toLowerCase() === cloth.toLowerCase());
    if (!set) { set = { cloth, colours: [] }; t.clothColours.push(set); }
    return set.colours;
  }

  setColours(t: ProductType, cloth: string, colours: string[]) {
    const set = t.clothColours.find(c => c.cloth.toLowerCase() === cloth.toLowerCase());
    if (set) set.colours = colours; else t.clothColours.push({ cloth, colours });
  }

  /** For the table: read only, never creates anything. */
  coloursText(t: ProductType, cloth: string) {
    const c = t.clothColours?.find(x => x.cloth.toLowerCase() === cloth.toLowerCase())?.colours ?? [];
    return c.length ? c.join(', ') : 'no colours';
  }

  save() {
    const t = this.editing();
    this.saving.set(true);
    this.errors.set({});
    const req = t.id ? this.api.update(t) : this.api.create(t);
    req.subscribe({
      next: () => {
        this.notify.ok(t.id ? 'Type updated' : 'Type added');
        this.dialogOpen.set(false);
        this.saving.set(false);
        this.load();
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  remove(t: ProductType) {
    this.confirm.confirm({
      header: `Delete ${t.name}?`,
      message: 'This removes the type and its lists. Items already created with it keep their details. A type that still has items can’t be deleted.',
      acceptLabel: 'Delete type', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(t.id).subscribe({
        next: () => { this.notify.ok('Type deleted'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }
}
