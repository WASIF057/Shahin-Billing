import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';

/**
 * Knows whether the server can be reached. The HTTP interceptor calls markDown() when a request fails because the server did not
 * answer (no connection, 502, 503 or 504) and markUp() when any answer arrives. While it is down this checks /api/health
 * every few seconds (a little longer each time) and puts everything back by itself once the server answers.
 */
@Injectable({ providedIn: 'root' })
export class ServerStatus {
  readonly down = signal(false);
  /** The device itself has no internet connection (as opposed to the server being down). */
  readonly offline = signal(false);
  /** Seconds until the next automatic try. */
  readonly nextTry = signal(0);
  readonly checking = signal(false);

  private timer: ReturnType<typeof setInterval> | null = null;
  private wait = 5;

  constructor() {
    // coming back online is a good moment to look again straight away
    if (typeof window !== 'undefined') window.addEventListener('online', () => { if (this.down()) void this.check(); });
  }

  markDown() {
    if (this.down()) return;
    this.offline.set(typeof navigator !== 'undefined' && navigator.onLine === false);
    this.wait = 5;
    this.down.set(true);
    this.schedule();
  }

  markUp() {
    if (!this.down()) return;
    this.stop();
    this.down.set(false);
    this.checking.set(false);
  }

  /** The "Try again now" button. */
  retryNow() { if (this.down() && !this.checking()) void this.check(); }

  private schedule() {
    this.stop();
    this.nextTry.set(this.wait);
    this.timer = setInterval(() => {
      this.nextTry.update(v => v - 1);
      if (this.nextTry() <= 0) void this.check();
    }, 1000);
  }

  private stop() { if (this.timer) { clearInterval(this.timer); this.timer = null; } }

  private async check() {
    this.stop();
    this.checking.set(true);
    this.offline.set(typeof navigator !== 'undefined' && navigator.onLine === false);
    let ok = false;
    try {
      // plain fetch, not HttpClient, so this check never triggers the interceptor again
      const r = await fetch(`${environment.apiUrl}/health`, { cache: 'no-store' });
      ok = r.ok;
    } catch { ok = false; }
    this.checking.set(false);
    if (ok) { this.markUp(); return; }
    this.wait = Math.min(Math.round(this.wait * 1.5), 30);   // 5, 8, 12, 18, 27, 30 seconds
    this.schedule();
  }
}
