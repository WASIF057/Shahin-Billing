import { Component, computed, input } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';

/**
 * Grey placeholder rows for a table that is still loading. Put it in the table's emptymessage template while loading,
 * so the screen shows the shape of the list instead of a wrong "No items yet" message.
 */
@Component({
  selector: 'app-table-skeleton',
  imports: [SkeletonModule],
  template: `
    @for (r of rowList(); track r) {
      <tr class="sk-row" aria-hidden="true">
        @for (c of colList(); track c) {
          <td><p-skeleton height="1.05rem" [width]="width(r, c)" /></td>
        }
      </tr>
    }
    <tr class="sk-status"><td [attr.colspan]="cols()"><span class="sk-sr" role="status">Loading&hellip;</span></td></tr>
  `,
  styles: `
    :host { display: contents; }
    .sk-row td { padding: 15px 12px; }
    .sk-status td { padding: 0; border: 0; height: 0; }
    .sk-sr { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
  `,
})
export class TableSkeleton {
  rows = input(6);
  cols = input(5);
  rowList = computed(() => Array.from({ length: this.rows() }, (_, i) => i));
  colList = computed(() => Array.from({ length: this.cols() }, (_, i) => i));

  /** Different widths so it looks like text, not a grid. */
  width(r: number, c: number) { return `${[78, 55, 66, 42, 60, 50, 70][(r * 3 + c * 2) % 7]}%`; }
}
