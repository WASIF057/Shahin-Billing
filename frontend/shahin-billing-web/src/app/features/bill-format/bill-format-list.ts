import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { BillFormatsApi, MiscApi } from '../../core/api.service';
import { BillFormat } from '../../core/models';
import { Notify } from '../../core/notify.service';

/** Bill format tab: a list of named PDF looks. The Active one is used for every PDF. */
@Component({
  selector: 'app-bill-format-list',
  imports: [FormsModule, RouterLink, ButtonModule, DialogModule, TableModule, TagModule, ToggleSwitchModule, TooltipModule],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>Bill formats</h1>
          <p>Different looks for your PDF bills. The Active one is used for every bill, download and email.</p>
        </div>
        <div class="actions">
          <a pButton routerLink="/bill-format/new" label="Create bill format" icon="pi pi-plus"></a>
        </div>
      </div>

      <div class="panel">
        <p-table [value]="formats()" [loading]="loading()" dataKey="id" [rowHover]="true" responsiveLayout="scroll">
          <ng-template #header>
            <tr><th>Name</th><th>Layout</th><th>Colour</th><th>Text size</th><th>Copies</th><th>Active</th><th></th></tr>
          </ng-template>
          <ng-template #body let-f>
            <tr [class.inactive]="!f.isActive">
              <td class="fname">
                {{ f.name }}
                @if (f.isActive) { <p-tag severity="success" value="In use" [rounded]="true" class="ml" /> }
              </td>
              <td>{{ f.settings.layout }}</td>
              <td><span class="dot" [style.background]="f.settings.primaryColor"></span> <span class="muted small">{{ f.settings.primaryColor }}</span></td>
              <td>{{ f.settings.fontSize }}</td>
              <td class="muted small">{{ f.settings.defaultCopies.join(', ') }}</td>
              <td>
                <p-toggleswitch [ngModel]="f.isActive" (ngModelChange)="setActive(f, $event)"
                                [pTooltip]="f.isActive ? 'In use. Switch on another format to change.' : 'Switch on to use this format'" />
              </td>
              <td class="row-actions">
                <button pButton type="button" icon="pi pi-eye" [text]="true" pTooltip="View" (click)="view(f)" aria-label="View"></button>
                <button pButton type="button" icon="pi pi-pencil" [text]="true" pTooltip="Edit" [routerLink]="['/bill-format', f.id]" aria-label="Edit"></button>
                <button pButton type="button" icon="pi pi-trash" [text]="true" severity="danger" pTooltip="Delete" (click)="remove(f)" aria-label="Delete"></button>
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr><td colspan="7"><div class="empty"><h3>No bill formats</h3><p>Create one to get started.</p></div></td></tr>
          </ng-template>
        </p-table>
      </div>
    </div>

    <p-dialog [header]="viewing()?.name ?? ''" [visible]="viewOpen()" (visibleChange)="closeView($event)" [modal]="true"
              [style]="{ width: '860px', maxWidth: '96vw' }" [draggable]="false">
      @if (previewUrl()) {
        <iframe [src]="previewUrl()" title="Bill format preview"></iframe>
      } @else {
        <div class="empty"><i class="pi pi-spin pi-spinner"></i> Preparing preview…</div>
      }
      <ng-template #footer>
        <button pButton type="button" label="Close" [text]="true" (click)="closeView(false)"></button>
        <button pButton type="button" label="Edit" icon="pi pi-pencil" (click)="editFromView()"></button>
      </ng-template>
    </p-dialog>
  `,
  styles: `
    .fname { font-weight: 700; }
    .ml { margin-left: 8px; }
    tr.inactive td:not(:nth-child(6)):not(:last-child) { opacity: 0.6; }
    .row-actions { text-align: right; white-space: nowrap; }
    .dot { display: inline-block; width: 14px; height: 14px; border-radius: 50%; vertical-align: -2px; margin-right: 4px; box-shadow: 0 0 0 1px var(--line); }
    iframe { width: 100%; height: 70vh; border: 1px solid var(--line); border-radius: 10px; background: #fff; }
  `,
})
export class BillFormatList implements OnInit, OnDestroy {
  private api = inject(BillFormatsApi);
  private misc = inject(MiscApi);
  private notify = inject(Notify);
  private confirm = inject(ConfirmationService);
  private sanitizer = inject(DomSanitizer);
  private router = inject(Router);

  formats = signal<BillFormat[]>([]);
  loading = signal(true);
  viewOpen = signal(false);
  viewing = signal<BillFormat | null>(null);
  previewUrl = signal<SafeResourceUrl | null>(null);
  private objectUrl: string | null = null;

  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: list => { this.formats.set(list); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  setActive(f: BillFormat, on: boolean) {
    this.api.setActive(f.id, on).subscribe({
      next: () => { if (on) this.notify.ok(`${f.name} is now in use for all bills`); this.load(); },
      error: e => { this.notify.error(e); this.load(); },
    });
  }

  view(f: BillFormat) {
    this.viewing.set(f);
    this.previewUrl.set(null);
    this.viewOpen.set(true);
    // The preview uses your real business details with sample items
    this.misc.previewPdf(f.settings).subscribe({
      next: blob => {
        this.revoke();
        this.objectUrl = URL.createObjectURL(blob);
        this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl + '#toolbar=0&view=FitH'));
      },
      error: e => this.notify.error(e),
    });
  }

  closeView(open: boolean) { this.viewOpen.set(open); if (!open) this.revoke(); }
  editFromView() { const f = this.viewing(); this.closeView(false); if (f) this.router.navigate(['/bill-format', f.id]); }

  remove(f: BillFormat) {
    this.confirm.confirm({
      header: 'Delete bill format?',
      message: `Delete “${f.name}”? Bills you already made are not changed. A format that is in use can't be deleted.`,
      acceptLabel: 'Delete format', rejectLabel: 'Keep',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.remove(f.id).subscribe({
        next: () => { this.notify.ok('Format deleted'); this.load(); },
        error: e => this.notify.error(e),
      }),
    });
  }

  private revoke() { if (this.objectUrl) { URL.revokeObjectURL(this.objectUrl); this.objectUrl = null; } }
  ngOnDestroy() { this.revoke(); }
}
