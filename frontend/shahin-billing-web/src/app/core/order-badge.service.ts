import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';

/** How many orders are waiting. The menu shows it as a badge, and the Orders screen refreshes it. */
@Injectable({ providedIn: 'root' })
export class OrderBadge {
  private http = inject(HttpClient);
  readonly count = signal(0);

  refresh() {
    this.http.get<{ newCount: number }>(`${environment.apiUrl}/orders/summary`).subscribe({ next: r => this.count.set(r.newCount), error: () => {} });
  }
}
