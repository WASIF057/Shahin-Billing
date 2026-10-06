import { Component, input, model, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

/**
 * Picks a PNG/JPG and returns it as a data URL (what the API stores).
 * `model()` creates a two-way bindable input: use it as [(value)]="...".
 */
@Component({
  selector: 'app-image-picker',
  imports: [ButtonModule],
  template: `
    <div class="picker">
      <div class="preview" [style.height.px]="height()">
        @if (value()) { <img [src]="value()" [alt]="label()" /> }
        @else { <span class="muted small">No {{ label().toLowerCase() }}</span> }
      </div>
      <div class="actions">
        <label class="p-button p-button-outlined p-button-sm upload">
          <i class="pi pi-upload"></i>&nbsp;Upload
          <input type="file" accept="image/png,image/jpeg" (change)="pick($event)" hidden />
        </label>
        @if (value()) {
          <button pButton type="button" label="Remove" [text]="true" size="small" severity="danger" (click)="value.set('')"></button>
        }
      </div>
    </div>
  `,
  styles: `
    .picker { display: flex; flex-direction: column; gap: 8px; }
    .preview { border: 1px dashed var(--line); border-radius: var(--radius-sm); display: flex;
               align-items: center; justify-content: center; background: #fafbfc; padding: 6px; }
    .preview img { max-height: 100%; max-width: 100%; object-fit: contain; }
    .upload { cursor: pointer; }
  `,
})
export class ImagePicker {
  value = model<string>('');
  label = input('Image');
  maxKb = input(500);
  height = input(90);
  tooLarge = output<string>();

  pick(e: Event) {
    const file = (e.target as HTMLInputElement).files?.[0];
    (e.target as HTMLInputElement).value = '';
    if (!file) return;
    if (file.size > this.maxKb() * 1024) {
      this.tooLarge.emit(`${this.label()} must be under ${this.maxKb()} KB. This file is ${Math.round(file.size / 1024)} KB.`);
      return;
    }
    const reader = new FileReader();
    reader.onload = () => this.value.set(reader.result as string);
    reader.readAsDataURL(file);
  }
}
