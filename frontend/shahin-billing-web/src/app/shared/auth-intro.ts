import { Component, HostListener, OnDestroy, OnInit, output, signal } from '@angular/core';
import { AuthScene } from './auth-scene';
import { Bed3d } from './bed3d';
import { Tilt } from './motion';

/**
 * The opening of the login page. The screen starts empty, the 3D mattress assembles itself, the name and the key
 * features appear, and the whole thing irises out to show the login page. About 6 seconds; Skip, Esc or a click ends it.
 */
@Component({
  selector: 'app-auth-intro',
  imports: [AuthScene, Bed3d, Tilt],
  template: `
    <div class="intro" [class.leaving]="leaving()" [appTilt]="9" role="dialog" aria-label="Welcome to Shahin Billing" (click)="finish()">
      <app-auth-scene [bed]="false" />
      <div class="beam" aria-hidden="true"></div>
      <div class="burst" aria-hidden="true">@for (k of sparks; track $index) { <i [style.--x.px]="k.x" [style.--y.px]="k.y" [style.--c]="k.c" [style.--s.px]="k.s"></i> }</div>

      <div class="center">
        <app-bed3d class="hero" [assemble]="true" />
        <span class="wave" aria-hidden="true"></span>

        <div class="word" [class.on]="titleOn()" aria-label="Shahin Enterprises">
          <div class="row big">@for (c of w1; track $index) { <span class="ch" [style.--i]="$index">{{ c }}</span> }</div>
          <div class="row small">@for (c of w2; track $index) { <span class="ch" [style.--i]="$index + 6">{{ c }}</span> }</div>
        </div>
        <p class="tag" [class.on]="titleOn()">Smart GST billing for your business</p>

        <div class="chips" [class.on]="featuresOn()">
          <span class="chip" style="--i: 0"><i class="pi pi-calculator"></i> Accurate GST</span>
          <span class="chip" style="--i: 1"><i class="pi pi-file-pdf"></i> PDF invoices</span>
          <span class="chip" style="--i: 2"><i class="pi pi-wallet"></i> Payment tracking</span>
          <span class="chip" style="--i: 3"><i class="pi pi-shield"></i> Secure and private</span>
        </div>
      </div>

      <button type="button" class="skip" (click)="finish(); $event.stopPropagation()">Skip intro <i class="pi pi-angle-double-right"></i></button>
      <div class="bar" aria-hidden="true"><span></span></div>
    </div>
  `,
  styles: `
    :host { position: fixed; inset: 0; z-index: 3000; display: block; }
    .intro {
      position: absolute; inset: 0; overflow: hidden; color: #fff; cursor: pointer; clip-path: circle(150% at 50% 50%);
      background: linear-gradient(160deg, var(--side-a) 0%, var(--side-b) 100%);
      animation: introIn 0.6s ease both;
    }
    @keyframes introIn { from { opacity: 0; } to { opacity: 1; } }
    .intro.leaving { animation: iris 0.85s cubic-bezier(0.7, 0, 0.3, 1) forwards; pointer-events: none; }
    @keyframes iris { from { clip-path: circle(150% at 50% 50%); } to { clip-path: circle(0% at 50% 50%); } }

    /* a light from above, like a stage spotlight, that fades up as the mattress arrives */
    .beam { position: absolute; left: 50%; top: 0; width: 640px; height: 74%; margin-left: -320px; pointer-events: none; opacity: 0;
            background: linear-gradient(180deg, rgba(255, 255, 255, 0.22), rgba(255, 255, 255, 0.04) 70%, transparent);
            clip-path: polygon(41% 0, 59% 0, 100% 100%, 0 100%); filter: blur(6px); animation: beam 1.6s ease 0.2s forwards, beamPulse 5s ease-in-out 1.8s infinite; }
    @keyframes beam { to { opacity: 1; } }
    @keyframes beamPulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.6; } }
    /* sparks thrown out when the mattress lands */
    .burst { position: absolute; left: 50%; top: calc(50% - 70px); width: 0; height: 0; pointer-events: none; z-index: 1; }
    .burst i { position: absolute; left: 0; top: 0; width: var(--s, 6px); height: var(--s, 6px); border-radius: 50%; background: var(--c, #fff); opacity: 0; animation: burst 1.1s cubic-bezier(0.1, 0.7, 0.3, 1) 1.45s both; }
    @keyframes burst { 0% { opacity: 1; transform: translate(0, 0) scale(1); } 100% { opacity: 0; transform: translate(var(--x), var(--y)) scale(0.2); } }

    .center { animation: dolly 7s ease-in-out both; }
    @keyframes dolly { from { transform: scale(0.93); } to { transform: scale(1.05); } }
    .center { position: absolute; inset: 0; z-index: 1; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 4px; padding: 0 16px; }
    .hero { transform: scale(var(--hs, 1.15)); margin-bottom: 0; }
    @media (max-height: 760px) { .hero { --hs: 0.85; margin: -28px 0; } }
    @media (prefers-reduced-motion: reduce) { .center, .beam { animation: none; } .beam { opacity: 1; } }
    @media (max-height: 600px) { .hero { --hs: 0.6; margin: -60px 0 -40px; } }
    /* a ring of light that spreads out when the mattress lands */
    .wave { position: absolute; left: 50%; top: calc(50% - 80px); width: 20px; height: 20px; margin: -10px 0 0 -10px; border-radius: 50%;
            border: 3px solid rgba(var(--accent-rgb), 0.9); opacity: 0; animation: wave 1.2s ease-out 1.15s forwards; pointer-events: none; }
    @keyframes wave { 0% { opacity: 0.9; transform: scale(1); } 100% { opacity: 0; transform: scale(60); } }

    .word { perspective: 700px; text-align: center; margin-top: 6px; }
    .row { display: flex; justify-content: center; }
    .ch { display: inline-block; opacity: 0; font-family: Montserrat, Manrope, sans-serif; font-weight: 800; transform-origin: 50% 100%; }
    .big { font-size: clamp(2.2rem, 6vw, 4.4rem); letter-spacing: 0.14em; line-height: 1.05; padding-left: 0.14em; }
    .small { font-size: clamp(0.8rem, 1.7vw, 1.15rem); letter-spacing: 0.7em; padding-left: 0.7em; color: var(--accent); font-weight: 500; margin-top: 4px; }
    .on .ch { animation: letter 0.75s cubic-bezier(0.2, 0.8, 0.2, 1) both, glow 2.6s ease-in-out infinite; animation-delay: calc(var(--i) * 70ms), calc(var(--i) * 130ms + 1.3s); }
    @keyframes glow { 0%, 100% { text-shadow: 0 0 0 transparent; } 50% { text-shadow: 0 0 26px rgba(var(--accent-rgb), 0.85), 0 0 4px rgba(255, 255, 255, 0.6); } }
    @keyframes letter { from { opacity: 0; transform: translateY(46px) rotateX(-85deg) scale(0.8); } to { opacity: 1; transform: none; } }

    .tag { margin: 12px 0 0; font-size: clamp(0.95rem, 1.6vw, 1.2rem); color: #c9d6ec; opacity: 0; letter-spacing: 0.02em; }
    .tag.on { animation: up 0.8s ease 0.9s both; }
    @keyframes up { from { opacity: 0; transform: translateY(14px); } to { opacity: 1; transform: none; } }

    .chips { display: flex; flex-wrap: wrap; justify-content: center; gap: 10px; margin-top: 22px; max-width: 680px; }
    .chip { display: inline-flex; align-items: center; gap: 8px; padding: 9px 16px; border-radius: 999px; font-weight: 700; font-size: 0.9rem; opacity: 0;
            background: rgba(255, 255, 255, 0.1); border: 1px solid rgba(255, 255, 255, 0.22); backdrop-filter: blur(6px); }
    .chip i { color: var(--accent); }
    .chips.on .chip { animation: chip 0.6s cubic-bezier(0.2, 0.9, 0.3, 1.3) both; animation-delay: calc(var(--i) * 140ms); }
    @keyframes chip { from { opacity: 0; transform: translateY(24px) scale(0.8); } to { opacity: 1; transform: none; } }

    .skip { position: absolute; right: 22px; top: 20px; z-index: 2; border: 1px solid rgba(255, 255, 255, 0.3); background: rgba(255, 255, 255, 0.1); color: #fff;
            border-radius: 999px; padding: 8px 16px; font: inherit; font-weight: 700; font-size: 0.85rem; cursor: pointer; backdrop-filter: blur(6px); }
    .skip:hover { background: rgba(255, 255, 255, 0.2); }
    .bar { position: absolute; left: 0; right: 0; bottom: 0; height: 4px; background: rgba(255, 255, 255, 0.1); z-index: 2; }
    .bar span { display: block; height: 100%; width: 100%; background: linear-gradient(90deg, var(--navy-light), var(--accent)); transform-origin: left; animation: fill 6s linear both; }
    @keyframes fill { from { transform: scaleX(0); } to { transform: scaleX(1); } }
  `,
})
export class AuthIntro implements OnInit, OnDestroy {
  done = output<void>();

  /** Sparks for the landing moment. */
  sparks = Array.from({ length: 26 }, (_, i) => {
    const a = (i / 26) * Math.PI * 2 + (i % 2 ? 0.12 : 0), dist = 130 + (i % 6) * 34;
    return { x: Math.round(Math.cos(a) * dist), y: Math.round(Math.sin(a) * dist * 0.75), c: i % 3 === 0 ? '#ffffff' : i % 3 === 1 ? 'var(--accent)' : 'var(--navy-light)', s: 4 + (i % 4) * 2 };
  });

  w1 = 'SHAHIN'.split('');
  w2 = 'ENTERPRISES'.split('');
  titleOn = signal(false);
  featuresOn = signal(false);
  leaving = signal(false);
  private timers: ReturnType<typeof setTimeout>[] = [];

  ngOnInit() {
    // The timeline: mattress assembles (0 - 2 s), name and tagline (2.3 s), feature chips (3.8 s), then the iris out (6 s)
    this.timers.push(
      setTimeout(() => this.titleOn.set(true), 2300),
      setTimeout(() => this.featuresOn.set(true), 3800),
      setTimeout(() => this.finish(), 6000),
    );
  }

  @HostListener('document:keydown.escape') onEscape() { this.finish(); }

  finish() {
    if (this.leaving()) return;
    this.leaving.set(true);
    this.timers.push(setTimeout(() => this.done.emit(), 850));
  }

  ngOnDestroy() { this.timers.forEach(clearTimeout); }
}
