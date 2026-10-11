import { Component, input, model, signal } from '@angular/core';

/**
 * A list of short text values (variants, sizes, cloths, colours) typed in with Enter.
 * Each chip can be moved left/right with its arrows or dragged to a new place, so the order can be changed
 * without deleting and re-adding. Use as `[(items)]="list"`; the order of the list is the order shown elsewhere.
 */
@Component({
  selector: 'app-chip-list',
  template: `
    <div class="box">
      @for (x of items(); track x; let i = $index) {
        <span class="chip" draggable="true" [class.over]="overIndex() === i"
              (dragstart)="dragFrom = i" (dragend)="dragFrom = -1; overIndex.set(-1)"
              (dragover)="$event.preventDefault(); overIndex.set(i)" (drop)="dropOn(i, $event)">
          <button type="button" class="mv" [disabled]="i === 0" (click)="move(i, -1)" [attr.aria-label]="'Move ' + x + ' earlier'">‹</button>
          <span class="txt">{{ x }}</span>
          <button type="button" class="mv" [disabled]="i === items().length - 1" (click)="move(i, 1)" [attr.aria-label]="'Move ' + x + ' later'">›</button>
          <button type="button" class="rm" (click)="remove(i)" [attr.aria-label]="'Remove ' + x">×</button>
        </span>
      }
      <input #box class="entry" type="text" [id]="inputId()" [placeholder]="placeholder()" autocomplete="off"
             (keydown.enter)="add(box)" (blur)="add(box)" (keydown.backspace)="backspace(box)" />
    </div>
  `,
  styles: `
    .box { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; min-height: 48px; padding: 6px 8px;
           border: 1px solid var(--input-border); border-radius: 10px; background: var(--input-bg); }
    .box:focus-within { border-color: var(--navy, #2f4a7a); box-shadow: 0 0 0 3px rgba(47, 74, 122, 0.15); }
    .chip { display: inline-flex; align-items: center; gap: 2px; background: var(--navy-soft, #eaeef7); border-radius: 8px;
            padding: 3px 4px; cursor: grab; font-weight: 500; }
    .chip.over { outline: 2px dashed var(--navy, #2f4a7a); }
    .txt { padding: 0 6px; }
    button { border: 0; background: transparent; cursor: pointer; font-size: 1.1rem; line-height: 1; padding: 2px 6px; border-radius: 6px; color: inherit; }
    button:hover:not(:disabled) { background: rgba(0, 0, 0, 0.08); }
    button:disabled { opacity: 0.25; cursor: default; }
    .rm { font-size: 1.2rem; }
    .entry { flex: 1; min-width: 140px; border: 0; outline: 0; background: transparent; color: inherit; font: inherit; padding: 6px 4px; }
  `,
})
export class ChipList {
  items = model<string[]>([]);
  placeholder = input('Type and press Enter');
  inputId = input('');

  protected dragFrom = -1;
  protected overIndex = signal(-1);

  add(box: HTMLInputElement) {
    const v = box.value.trim();
    box.value = '';
    if (!v) return;
    const list = this.items() ?? [];
    if (!list.some(x => x.toLowerCase() === v.toLowerCase())) this.items.set([...list, v]);
  }

  /** Backspace in an empty box removes the last chip, like the old chip box did. */
  backspace(box: HTMLInputElement) {
    if (box.value === '' && this.items().length) this.remove(this.items().length - 1);
  }

  remove(i: number) { this.items.set(this.items().filter((_, j) => j !== i)); }

  move(i: number, by: number) { this.reorder(i, i + by); }

  dropOn(to: number, e: Event) {
    e.preventDefault();
    if (this.dragFrom >= 0) this.reorder(this.dragFrom, to);
    this.dragFrom = -1;
    this.overIndex.set(-1);
  }

  private reorder(from: number, to: number) {
    const list = [...this.items()];
    if (to < 0 || to >= list.length || from === to) return;
    const [x] = list.splice(from, 1);
    list.splice(to, 0, x);
    this.items.set(list);
  }
}
