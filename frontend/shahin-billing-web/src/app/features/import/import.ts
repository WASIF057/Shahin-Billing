import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ImportApi, saveBlob } from '../../core/api.service';
import { ImportResult } from '../../core/models';
import { Notify } from '../../core/notify.service';

type Kind = 'clients' | 'items';

/** Import: bring clients or items in from an Excel file. Check the file first, then import the good rows. */
@Component({
  selector: 'app-import',
  imports: [ButtonModule, TableModule, TagModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Import from Excel</h1>
          <p>Add many clients or items at once. Download the template, fill it in, then check the file before importing.</p>
        </div>
      </div>

      <div class="cards">
        @for (k of kinds; track k.key) {
          <section class="panel">
            <h2>{{ k.title }}</h2>
            <p class="muted small">{{ k.help }}</p>

            <div class="steps">
              <div>
                <span class="n">1</span>
                <button pButton type="button" label="Download template" icon="pi pi-download" [outlined]="true" size="small"
                        (click)="template(k.key)"></button>
              </div>
              <div>
                <span class="n">2</span>
                <input #f type="file" accept=".xlsx" hidden (change)="picked(k.key, f.files)" />
                <button pButton type="button" [label]="file(k.key)?.name ?? 'Choose your Excel file'" icon="pi pi-file-excel"
                        [outlined]="true" size="small" (click)="f.click()"></button>
              </div>
              <div>
                <span class="n">3</span>
                <button pButton type="button" label="Check the file" icon="pi pi-search" size="small"
                        [disabled]="!file(k.key)" [loading]="busy() === k.key + ':check'" (click)="run(k.key, true)"></button>
              </div>
            </div>

            @if (result(k.key); as r) {
              <div class="summary">
                @if (r.imported) {
                  <p-tag severity="success" [value]="r.ok + ' added'" [rounded]="true" />
                } @else {
                  <p-tag severity="success" [value]="r.ok + ' will be added'" [rounded]="true" />
                }
                @if (r.skipped) { <p-tag severity="secondary" [value]="r.skipped + ' already exist'" [rounded]="true" /> }
                @if (r.errors) { <p-tag severity="danger" [value]="r.errors + ' with problems'" [rounded]="true" /> }
              </div>

              @if (r.rows.length) {
                <p-table [value]="r.rows" [scrollable]="true" scrollHeight="320px" styleClass="rows" size="small">
                  <ng-template #header><tr><th style="width: 60px">Row</th><th style="width: 90px">Result</th><th>Details</th></tr></ng-template>
                  <ng-template #body let-x>
                    <tr>
                      <td>{{ x.row }}</td>
                      <td><p-tag [severity]="x.status === 'Ok' ? 'success' : x.status === 'Skipped' ? 'secondary' : 'danger'" [value]="x.status === 'Ok' ? 'OK' : x.status" [rounded]="true" /></td>
                      <td>
                        <div class="sum">{{ x.summary }}</div>
                        <div class="msg" [class.bad]="x.status === 'Error'">{{ x.message }}</div>
                      </td>
                    </tr>
                  </ng-template>
                </p-table>
              }

              @if (!r.imported && r.ok > 0) {
                <button pButton type="button" [label]="'Import ' + r.ok + (r.ok === 1 ? ' row' : ' rows')" icon="pi pi-check"
                        [loading]="busy() === k.key + ':go'" (click)="run(k.key, false)"></button>
                @if (r.errors) { <p class="muted small">Rows with problems are left out. Fix them in the file and import it again: rows already added are skipped, so nothing is added twice.</p> }
              }
              @if (!r.imported && r.ok === 0) { <p class="muted small">Nothing to import yet. Fix the file and check it again.</p> }
            }
          </section>
        }
      </div>
    </div>
  `,
  styles: `
    .cards { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; align-items: start; }
    .cards .panel + .panel { margin-top: 0; }
    .panel { display: flex; flex-direction: column; gap: 14px; }
    .panel > h2 { margin: 0; }
    .panel p { margin: 0; }
    .steps { display: flex; flex-direction: column; gap: 10px; }
    .steps > div { display: flex; align-items: center; gap: 10px; }
    .n { width: 24px; height: 24px; border-radius: 50%; background: var(--navy-soft); color: var(--navy); font-weight: 800; font-size: 0.8rem; display: grid; place-items: center; flex-shrink: 0; }
    .summary { display: flex; gap: 8px; flex-wrap: wrap; }
    .sum { font-weight: 600; }
    .msg { font-size: 0.82rem; color: var(--muted); }
    .msg.bad { color: var(--bad); }
    @media (max-width: 1000px) { .cards { grid-template-columns: 1fr; } }
  `,
})
export class ImportPage {
  private api = inject(ImportApi);
  private notify = inject(Notify);

  kinds: { key: Kind; title: string; help: string }[] = [
    { key: 'clients', title: 'Clients', help: 'Name, GSTIN, state, mobile, email, cities. A client whose name or GSTIN already exists is skipped.' },
    { key: 'items', title: 'Items', help: 'Type, variant, cloth, colour, size, unit, GST % and rate. The type (and its variants, cloth and sizes) must already be in Item setup.' },
  ];

  private files = signal<Record<Kind, File | null>>({ clients: null, items: null });
  private results = signal<Record<Kind, ImportResult | null>>({ clients: null, items: null });
  busy = signal('');

  file(k: Kind) { return this.files()[k]; }
  result(k: Kind) { return this.results()[k]; }

  picked(k: Kind, list: FileList | null) {
    this.files.update(f => ({ ...f, [k]: list?.item(0) ?? null }));
    this.results.update(r => ({ ...r, [k]: null }));     // a new file needs checking again
  }

  template(k: Kind) {
    this.api.template(k).subscribe({ next: b => saveBlob(b, `import-${k}-template.xlsx`), error: e => this.notify.error(e) });
  }

  run(k: Kind, dryRun: boolean) {
    const f = this.file(k);
    if (!f) return;
    this.busy.set(`${k}:${dryRun ? 'check' : 'go'}`);
    this.api.run(k, f, dryRun).subscribe({
      next: r => {
        this.results.update(all => ({ ...all, [k]: r }));
        this.busy.set('');
        if (!dryRun) this.notify.ok(`${r.ok} ${k} added`);
      },
      error: e => { this.notify.error(e); this.busy.set(''); },
    });
  }
}
