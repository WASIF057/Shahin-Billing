import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { Order } from './models';
import { OrderBadge } from './order-badge.service';

const SOUND_KEY = 'orders-sound';
const SEEN_KEY = 'orders-seen';

/**
 * Watches for new orders while the owner or staff have the app open: a popup card, a ring, and (if allowed)
 * a desktop notification when the tab is in the background. It asks the server every 15 seconds.
 * The ring is made by the browser itself (no sound file). Browsers only allow sound after you have clicked
 * on the page once, which happens naturally after logging in.
 */
@Injectable({ providedIn: 'root' })
export class OrderAlerts {
  private http = inject(HttpClient);
  private badge = inject(OrderBadge);

  /** Orders to show as popup cards right now. */
  readonly popups = signal<Order[]>([]);
  /** Shown once after logging in when orders were already waiting (no ring: they are not new). */
  readonly waiting = signal(0);
  readonly soundOn = signal(this.read(SOUND_KEY) !== 'off');
  /** true when the browser is still refusing sound because nothing was clicked yet. */
  readonly soundBlocked = signal(false);
  readonly desktop = signal<NotificationPermission | 'unsupported'>(typeof Notification === 'undefined' ? 'unsupported' : Notification.permission);

  private timer: ReturnType<typeof setInterval> | null = null;
  private ctx: AudioContext | null = null;
  private seen = new Set<string>(this.loadSeen());
  private first = true;
  private unlock = () => { void this.audio()?.resume(); };

  start() {
    if (this.timer) return;
    this.first = true;
    // Any click or key press lets the browser play sound
    document.addEventListener('pointerdown', this.unlock);
    document.addEventListener('keydown', this.unlock);
    this.poll();
    this.timer = setInterval(() => this.poll(), 15_000);
  }

  stop() {
    if (this.timer) { clearInterval(this.timer); this.timer = null; }
    document.removeEventListener('pointerdown', this.unlock);
    document.removeEventListener('keydown', this.unlock);
    this.popups.set([]);
    this.waiting.set(0);
  }

  private poll() {
    this.http.get<{ newCount: number; fresh: Order[] }>(`${environment.apiUrl}/orders/watch`).subscribe({
      next: r => {
        this.badge.count.set(r.newCount);
        const fresh = r.fresh.filter(o => !this.seen.has(o.id));
        if (fresh.length) this.arrived(fresh);
        else if (this.first && r.newCount > 0) this.waiting.set(r.newCount);
        this.first = false;
      },
      error: () => {},   // a missed check is fine: the next one is 15 seconds away
    });
  }

  private arrived(fresh: Order[]) {
    fresh.forEach(o => this.seen.add(o.id));
    this.saveSeen();
    this.waiting.set(0);
    this.popups.update(p => [...p, ...fresh]);
    if (this.soundOn()) this.ring();
    this.desktopNotice(fresh);
    // a popup that nobody touched goes away by itself after a minute (the Orders screen and the badge still show it)
    setTimeout(() => fresh.forEach(o => this.dismiss(o.id)), 60_000);
  }

  dismiss(id: string) { this.popups.update(p => p.filter(o => o.id !== id)); }
  dismissAll() { this.popups.set([]); this.waiting.set(0); }

  // ---------------------------------------------------------------- sound
  toggleSound() {
    const on = !this.soundOn();
    this.soundOn.set(on);
    try { localStorage.setItem(SOUND_KEY, on ? 'on' : 'off'); } catch { /* private window: just not remembered */ }
    if (on) this.ring();   // the click that turned it on also lets the browser play it
  }

  testSound() { this.ring(true); }

  private audio(): AudioContext | null {
    if (this.ctx) return this.ctx;
    const Ctor = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!Ctor) return null;
    this.ctx = new Ctor();
    return this.ctx;
  }

  /** Three double-rings, like a shop bell. */
  private ring(force = false) {
    if (!force && !this.soundOn()) return;
    const ctx = this.audio();
    if (!ctx) return;
    const play = () => {
      this.soundBlocked.set(false);
      const t0 = ctx.currentTime + 0.05;
      for (let i = 0; i < 3; i++) {
        this.tone(ctx, 880, t0 + i * 0.95, 0.4);
        this.tone(ctx, 1175, t0 + i * 0.95 + 0.2, 0.5);
      }
    };
    if (ctx.state === 'running') { play(); return; }
    ctx.resume().then(() => (ctx.state === 'running' ? play() : this.soundBlocked.set(true))).catch(() => this.soundBlocked.set(true));
  }

  private tone(ctx: AudioContext, freq: number, start: number, length: number) {
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.type = 'sine';
    osc.frequency.value = freq;
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(0.35, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + length);
    osc.connect(gain).connect(ctx.destination);
    osc.start(start);
    osc.stop(start + length + 0.05);
  }

  // ---------------------------------------------------------------- desktop notifications (optional)
  enableDesktop() {
    if (typeof Notification === 'undefined') return;
    void Notification.requestPermission().then(p => this.desktop.set(p));
  }

  private desktopNotice(orders: Order[]) {
    if (typeof Notification === 'undefined' || Notification.permission !== 'granted' || !document.hidden) return;
    for (const o of orders) {
      const n = new Notification(`New order ${o.orderNumber}`, {
        body: `${o.clientName}${o.city ? ' (' + o.city + ')' : ''}: ${o.lines.length} product${o.lines.length === 1 ? '' : 's'}`,
        tag: o.id,
      });
      n.onclick = () => { window.focus(); window.location.assign('/orders'); n.close(); };
    }
  }

  // ---------------------------------------------------------------- remembered in this browser
  private read(key: string): string | null { try { return localStorage.getItem(key); } catch { return null; } }
  private loadSeen(): string[] { try { return JSON.parse(localStorage.getItem(SEEN_KEY) ?? '[]'); } catch { return []; } }
  private saveSeen() {
    try { localStorage.setItem(SEEN_KEY, JSON.stringify([...this.seen].slice(-100))); } catch { /* not remembered */ }
  }
}
