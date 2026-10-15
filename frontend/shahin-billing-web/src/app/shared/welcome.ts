import { Component, inject, Injectable, signal } from '@angular/core';
import { AuthService } from '../core/auth.service';
import { canAnimate } from './motion';

/**
 * The animation between a successful login and the first page: 3D doors swing shut, a ring of invoices turns
 * under "Welcome back", then the doors swing open on the dashboard. The page change itself happens behind the doors.
 */
@Injectable({ providedIn: 'root' })
export class Welcome {
  readonly active = signal(false);
  readonly leaving = signal(false);

  private timers: ReturnType<typeof setTimeout>[] = [];
  private go: (() => void) | null = null;

  /** Plays the animation and runs `go` (the navigation) while the doors are shut. People who asked for less motion go straight there. */
  play(go: () => void) {
    if (!canAnimate()) { go(); return; }
    this.clear();
    this.go = go;
    this.leaving.set(false);
    this.active.set(true);
    this.timers.push(
      setTimeout(() => this.runGo(), 1500),      // doors are shut and the ring is spinning: change the page behind them
      setTimeout(() => this.leave(), 2500),
    );
  }

  /** A click skips ahead: the page changes now and the doors open. */
  skip() { if (this.active()) { this.runGo(); this.leave(); } }

  private leave() {
    if (this.leaving()) return;
    this.leaving.set(true);
    this.timers.push(setTimeout(() => { this.active.set(false); this.leaving.set(false); }, 1000));
  }

  private runGo() { const g = this.go; this.go = null; g?.(); }
  private clear() { this.timers.forEach(clearTimeout); this.timers = []; }
}

@Component({
  selector: 'app-welcome',
  template: `
    @if (w.active()) {
      <div class="wl" [class.leaving]="w.leaving()" role="status" aria-live="polite" (click)="w.skip()">
        <div class="door l"></div>
        <div class="door r"></div>
        <div class="content">
          <div class="ring3d" aria-hidden="true">
            @for (c of cards; track $index) {
              <div class="card" [style.--i]="$index">
                <div class="ct"><b>TAX INVOICE</b><span>{{ c.no }}</span></div>
                <div class="cl"><i></i><i></i></div>
                <div class="cl s"><i></i><i></i></div>
                <div class="cm"><span>Total</span><b>{{ c.amt }}</b></div>
              </div>
            }
          </div>
          <h2 class="hi"><span>Welcome back,</span> <strong>{{ first() }}</strong></h2>
          <p class="sub">Getting your dashboard ready&hellip;</p>
          <div class="load" aria-hidden="true"><span></span></div>
        </div>
      </div>
    }
  `,
  styles: `
    .wl { position: fixed; inset: 0; z-index: 4000; perspective: 1700px; overflow: hidden; cursor: pointer; }

    /* ---- the two doors ---- */
    .door { position: absolute; top: 0; bottom: 0; width: 50%; background-size: 200% 100%; backface-visibility: hidden;
            background-image: radial-gradient(520px 420px at 30% 20%, rgba(var(--navy-rgb), 0.7), transparent 70%), linear-gradient(160deg, var(--side-a), var(--side-b)); }
    .door.l { left: 0; transform-origin: left center; background-position: left top; animation: shutL 0.6s cubic-bezier(0.5, 0, 0.2, 1) both; }
    .door.r { right: 0; transform-origin: right center; background-position: right top; animation: shutR 0.6s cubic-bezier(0.5, 0, 0.2, 1) both; }
    .door::after { content: ''; position: absolute; top: 0; bottom: 0; width: 2px; background: linear-gradient(180deg, transparent, var(--accent), transparent); opacity: 0.85; box-shadow: 0 0 18px var(--accent); }
    .door.l::after { right: 0; } .door.r::after { left: 0; }
    @keyframes shutL { from { transform: rotateY(98deg); } to { transform: rotateY(0); } }
    @keyframes shutR { from { transform: rotateY(-98deg); } to { transform: rotateY(0); } }
    .leaving .door.l { animation: openL 1s cubic-bezier(0.7, 0, 0.25, 1) both; }
    .leaving .door.r { animation: openR 1s cubic-bezier(0.7, 0, 0.25, 1) both; }
    @keyframes openL { from { transform: rotateY(0); } to { transform: rotateY(98deg); } }
    @keyframes openR { from { transform: rotateY(0); } to { transform: rotateY(-98deg); } }

    /* ---- what is on the doors ---- */
    .content { position: absolute; inset: 0; z-index: 2; display: flex; flex-direction: column; align-items: center; justify-content: center; text-align: center; color: #fff;
               animation: contentIn 0.6s ease 0.45s both; }
    .leaving .content { animation: contentOut 0.45s ease both; }
    @keyframes contentIn { from { opacity: 0; transform: translateY(18px); } to { opacity: 1; transform: none; } }
    @keyframes contentOut { to { opacity: 0; transform: scale(1.12); } }

    /* a ring of invoices turning around the middle */
    .ring3d { position: relative; width: 190px; height: 128px; margin-bottom: 92px; transform-style: preserve-3d; animation: spinRing 7s linear infinite; }
    @keyframes spinRing { from { transform: rotateX(-16deg) rotateY(0deg); } to { transform: rotateX(-16deg) rotateY(360deg); } }
    .card { position: absolute; inset: 0; border-radius: 12px; padding: 12px 14px; background: rgba(255, 255, 255, 0.96); color: #1b2438; text-align: left;
            transform: rotateY(calc(var(--i) * 60deg)) translateZ(215px); backface-visibility: hidden; box-shadow: 0 18px 36px rgba(0, 0, 0, 0.4); display: flex; flex-direction: column; gap: 8px; }
    .ct { display: flex; justify-content: space-between; font-size: 0.62rem; letter-spacing: 0.08em; } .ct span { color: #6a7587; font-weight: 700; }
    .cl { display: flex; justify-content: space-between; gap: 18px; } .cl i { display: block; height: 7px; border-radius: 4px; background: #e3e8f1; flex: 1; } .cl i + i { flex: 0 0 22%; background: #cfd8e8; }
    .cl.s i:first-child { flex: 0 0 55%; }
    .cm { margin-top: auto; display: flex; justify-content: space-between; align-items: baseline; border-top: 2px dashed #d5dcea; padding-top: 6px; font-size: 0.68rem; } .cm b { font-size: 0.95rem; color: var(--navy-deep); }

    .hi { margin: 0; font-family: Montserrat, Manrope, sans-serif; font-weight: 800; font-size: clamp(1.7rem, 4.6vw, 3.1rem); line-height: 1.1; letter-spacing: -0.01em; }
    .hi span { display: block; font-weight: 500; font-size: 0.5em; letter-spacing: 0.16em; text-transform: uppercase; color: #c9d6ec; margin-bottom: 6px; }
    .hi strong { color: var(--accent); text-shadow: 0 0 30px rgba(var(--accent-rgb), 0.55); }
    .sub { margin: 14px 0 0; color: #c9d6ec; font-size: 1rem; }
    .load { width: min(280px, 70vw); height: 4px; border-radius: 4px; background: rgba(255, 255, 255, 0.16); margin-top: 20px; overflow: hidden; }
    .load span { display: block; height: 100%; background: linear-gradient(90deg, var(--navy-light), var(--accent)); transform-origin: left; animation: fill 2s ease-in-out 0.5s both; }
    @keyframes fill { from { transform: scaleX(0); } to { transform: scaleX(1); } }

    @media (max-width: 640px) { .ring3d { width: 150px; height: 102px; margin-bottom: 72px; } .card { transform: rotateY(calc(var(--i) * 60deg)) translateZ(165px); padding: 9px 11px; } }
    @media (max-height: 600px) { .ring3d { display: none; } }
    @media (prefers-reduced-motion: reduce) { .door, .content, .ring3d, .load span { animation: none !important; } }
  `,
})
export class WelcomeOverlay {
  w = inject(Welcome);
  private auth = inject(AuthService);

  first() { return (this.auth.user()?.name ?? '').trim().split(/\s+/)[0] || 'there'; }

  /** Decorative numbers on the spinning invoices. */
  cards = [
    { no: 'INV-0041', amt: '₹ 18,450.00' }, { no: 'INV-0042', amt: '₹ 24,780.00' }, { no: 'INV-0043', amt: '₹ 9,320.00' },
    { no: 'INV-0044', amt: '₹ 31,075.00' }, { no: 'INV-0045', amt: '₹ 12,640.00' }, { no: 'INV-0046', amt: '₹ 27,910.00' },
  ];
}
