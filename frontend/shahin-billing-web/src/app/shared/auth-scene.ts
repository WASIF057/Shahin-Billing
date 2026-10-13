import { Component, input } from '@angular/core';
import { Bed3d } from './bed3d';

/**
 * The animated backdrop of the login pages: a slowly shifting aurora, twinkling dots, and a 3D mattress with a pillow,
 * a blanket and rising "zzz" (the brand's sleeping scene), all in CSS 3D. Colours come from the colour theme.
 * It leans toward the mouse through --rx / --ry / --px / --py, which the appTilt directive on the page sets.
 */
@Component({
  selector: 'app-auth-scene',
  imports: [Bed3d],
  template: `
    <div class="aurora"><span class="b1"></span><span class="b2"></span><span class="b3"></span></div>
    <div class="dots">@for (d of dots; track $index) { <i [style.left.%]="d.x" [style.top.%]="d.y" [style.animation-delay.s]="d.delay" [style.width.px]="d.size" [style.height.px]="d.size"></i> }</div>

    @if (bed()) { <app-bed3d class="slot" [class.low]="low()" /> }
  `,
  styles: `
    :host { position: absolute !important; inset: 0; z-index: 0 !important; display: block; overflow: hidden; pointer-events: none; }

    /* ---- aurora ---- */
    .aurora span { position: absolute; border-radius: 50%; filter: blur(60px); opacity: 0.55; will-change: transform; }
    .b1 { width: 380px; height: 380px; left: -90px; top: 8%; background: rgba(var(--navy-rgb), 0.75); animation: drift1 16s ease-in-out infinite; }
    .b2 { width: 320px; height: 320px; right: -60px; top: 30%; background: rgba(var(--accent-rgb), 0.45); animation: drift2 20s ease-in-out infinite; }
    .b3 { width: 300px; height: 300px; left: 30%; bottom: -120px; background: rgba(var(--navy-rgb), 0.55); animation: drift3 18s ease-in-out infinite; }
    @keyframes drift1 { 0%, 100% { transform: translate(0, 0) scale(1); } 50% { transform: translate(90px, 60px) scale(1.15); } }
    @keyframes drift2 { 0%, 100% { transform: translate(0, 0) scale(1); } 50% { transform: translate(-80px, 70px) scale(0.9); } }
    @keyframes drift3 { 0%, 100% { transform: translate(0, 0) scale(1); } 50% { transform: translate(60px, -70px) scale(1.12); } }

    /* ---- twinkling dots ---- */
    .dots i { position: absolute; border-radius: 50%; background: #fff; opacity: 0; animation: twinkle 5s ease-in-out infinite; }
    @keyframes twinkle { 0%, 100% { opacity: 0; transform: scale(0.6); } 50% { opacity: 0.8; transform: scale(1.2); } }

    /* ---- where the mattress sits ---- */
    .slot { position: absolute; right: 2%; top: 37%; transform: scale(var(--s, 0.85)); transform-origin: top right; }
    .slot.low { top: 58%; }
    @media (max-width: 1400px) { .slot { --s: 0.7; } }
    @media (max-width: 1180px), (max-height: 700px) { .slot { display: none; } }
    @media (max-width: 860px) { .dots, .slot { display: none; } }
    @media (prefers-reduced-motion: reduce) { .b1, .b2, .b3, .dots i { animation: none; } .dots i { opacity: 0.5; } }
  `,
})
export class AuthScene {
  /** Pages whose text is centred (Forgot password) put the mattress lower so it does not cover the words. */
  low = input(false);
  /** false = backdrop only (the intro draws its own, larger mattress). */
  bed = input(true);

  /** Fixed positions so the dots do not jump around between renders. */
  dots = [
    { x: 8, y: 22, delay: 0, size: 3 }, { x: 18, y: 64, delay: 1.2, size: 2 }, { x: 31, y: 12, delay: 2.4, size: 3 }, { x: 44, y: 78, delay: 0.6, size: 2 },
    { x: 55, y: 30, delay: 3.1, size: 3 }, { x: 63, y: 58, delay: 1.8, size: 2 }, { x: 72, y: 16, delay: 4, size: 3 }, { x: 80, y: 72, delay: 0.3, size: 2 },
    { x: 88, y: 40, delay: 2.7, size: 3 }, { x: 94, y: 84, delay: 3.6, size: 2 }, { x: 24, y: 88, delay: 2, size: 2 }, { x: 49, y: 6, delay: 1.5, size: 2 },
  ];
}
