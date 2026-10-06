import { Component, inject, input, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { ColorPickerModule } from 'primeng/colorpicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CheckboxModule } from 'primeng/checkbox';
import { BillFormatsApi, MiscApi } from '../../core/api.service';
import { fieldErrors } from '../../core/errors';
import { TemplateSettings } from '../../core/models';
import { Notify } from '../../core/notify.service';

type ToggleKey = keyof Pick<TemplateSettings, 'showLogo' | 'showHsnColumn' | 'showDiscountColumn' | 'showGstSummary' |
  'showBankDetails' | 'showUpi' | 'showTerms' | 'showDeclaration' | 'showSignature' | 'showTransportDetails' | 'showPoDetails'>;

@Component({
  selector: 'app-bill-format',
  imports: [FormsModule, RouterLink, ButtonModule, ColorPickerModule, InputTextModule, SelectButtonModule, ToggleSwitchModule, CheckboxModule],
  templateUrl: './bill-format.html',
  styleUrl: './bill-format.scss',
})
export class BillFormat implements OnInit, OnDestroy {
  /** Route param :id (empty = creating a new format). */
  id = input<string>();

  private api = inject(MiscApi);
  private formatsApi = inject(BillFormatsApi);
  private router = inject(Router);
  private notify = inject(Notify);
  private sanitizer = inject(DomSanitizer);

  t = signal<TemplateSettings | null>(null);
  name = '';
  isActive = false;
  errors = signal<Record<string, string>>({});
  previewUrl = signal<SafeResourceUrl | null>(null);
  previewLoading = signal(false);
  saving = signal(false);
  private objectUrl: string | null = null;
  private changes = new Subject<void>();

  layouts = [{ label: 'Classic', value: 'Classic' }, { label: 'Modern', value: 'Modern' }];
  sizes = [{ label: 'Small', value: 'Small' }, { label: 'Normal', value: 'Normal' }, { label: 'Large', value: 'Large' }];
  swatches = ['#1F4E79', '#23466B', '#0F5257', '#2E5E3A', '#5B2A86', '#8A1C1C', '#333333'];
  toggles: { key: ToggleKey; label: string }[] = [
    { key: 'showLogo', label: 'Logo' },
    { key: 'showGstSummary', label: 'GST summary by rate' },
    { key: 'showBankDetails', label: 'Bank details' },
    { key: 'showUpi', label: 'UPI ID' },
    { key: 'showTransportDetails', label: 'Transport details' },
    { key: 'showPoDetails', label: 'PO date' },
    { key: 'showTerms', label: 'Terms and conditions' },
    { key: 'showDeclaration', label: 'Declaration' },
    { key: 'showSignature', label: 'Signature image' },
  ];
  copyNames = ['Original', 'Duplicate', 'Triplicate'];

  constructor() {
    // Live preview: wait 500 ms after the last change, then ask the API for a fresh sample PDF
    this.changes.pipe(
      debounceTime(500),
      switchMap(() => { this.previewLoading.set(true); return this.api.previewPdf(this.t()!); }),
      takeUntilDestroyed(),
    ).subscribe({
      next: blob => {
        if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
        this.objectUrl = URL.createObjectURL(blob);
        this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.objectUrl + '#toolbar=0&view=FitH'));
        this.previewLoading.set(false);
      },
      error: e => { this.notify.error(e); this.previewLoading.set(false); },
    });
  }

  ngOnInit() {
    const id = this.id();
    if (id) {
      this.formatsApi.get(id).subscribe({
        next: f => { this.name = f.name; this.isActive = f.isActive; this.t.set(f.settings); this.changed(); },
        error: e => { this.notify.error(e); this.router.navigate(['/bill-format']); },
      });
    } else {
      // New format: start from the look in use now, then change what you like
      this.api.template().subscribe({
        next: t => { this.t.set(t); this.changed(); },
        error: e => this.notify.error(e),
      });
    }
  }

  changed() { this.changes.next(); }

  hasCopy(t: TemplateSettings, c: string) { return t.defaultCopies.includes(c); }
  toggleCopy(t: TemplateSettings, c: string, on: boolean) {
    t.defaultCopies = on ? [...new Set([...t.defaultCopies, c])] : t.defaultCopies.filter(x => x !== c);
    if (!t.defaultCopies.length) t.defaultCopies = ['Original'];
  }

  setColor(t: TemplateSettings, c: string) { t.primaryColor = c; this.changed(); }

  save() {
    const id = this.id();
    this.saving.set(true);
    this.errors.set({});
    const body = { id: id ?? '', name: this.name, settings: this.t()!, isActive: this.isActive };
    const req = id ? this.formatsApi.update(body) : this.formatsApi.create(body);
    req.subscribe({
      next: () => {
        this.saving.set(false);
        this.notify.ok(id && this.isActive ? 'Bill format saved. New PDFs use it right away.' : 'Bill format saved.');
        this.router.navigate(['/bill-format']);
      },
      error: e => { this.errors.set(fieldErrors(e)); this.notify.error(e); this.saving.set(false); },
    });
  }

  ngOnDestroy() { if (this.objectUrl) URL.revokeObjectURL(this.objectUrl); }
}
