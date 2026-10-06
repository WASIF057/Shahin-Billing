import { Injectable, inject } from '@angular/core';
import { MessageService } from 'primeng/api';
import { errorMessage } from './errors';

/** Small wrapper so components can show toasts in one line. */
@Injectable({ providedIn: 'root' })
export class Notify {
  private messages = inject(MessageService);
  ok(detail: string) { this.messages.add({ severity: 'success', summary: detail, life: 2500 }); }
  info(detail: string) { this.messages.add({ severity: 'info', summary: detail, life: 3500 }); }
  warn(detail: string) { this.messages.add({ severity: 'warn', summary: detail, life: 5000 }); }
  error(err: unknown) { this.messages.add({ severity: 'error', summary: errorMessage(err), life: 6000 }); }
}
