import { Component, inject, input } from '@angular/core';
import { AuthMood } from './auth-mood';

/**
 * The brand's sleeping scene as a real 3D object in CSS: a ticking-striped mattress with a pillow stack, a blanket and
 * rising "zzz". It slowly turns, and leans toward the mouse through --rx / --ry / --px / --py (set by the appTilt directive
 * on a parent). With [assemble]="true" the pieces drop in one after another (used by the intro). Colours follow the theme.
 * The component is 330 x 300 px; the page that uses it positions and scales it.
 */
@Component({
  selector: 'app-bed3d',
  template: `
    <div class="scene" [class.assemble]="assemble()" [class.privacy]="mood.mood() === 'privacy'" [class.joy]="mood.mood() === 'joy'" [class.oops]="mood.mood() === 'oops'">
      <div class="stage">
        <div class="bed">
          <div class="face top">
            <div class="blanket"></div>
            <div class="pillow p1"></div><div class="pillow p2"></div><div class="pillow p3"></div>
          </div>
          <div class="face front"><div class="drape"></div></div>
          <div class="face back"></div>
          <div class="face left"></div>
          <div class="face right"></div>
        </div>
        <div class="shadow"></div>
      </div>
      <span class="zz z1">z</span><span class="zz z2">z</span><span class="zz z3">Z</span>
      <span class="lockico"><i class="pi pi-lock"></i></span>
      <span class="tick"><i class="pi pi-check"></i></span>
      <div class="sparks" aria-hidden="true">@for (k of sparks; track $index) { <i [style.--x.px]="k.x" [style.--y.px]="k.y" [style.--c]="k.c" [style.--s.px]="k.s" [style.animation-delay.ms]="k.d"></i> }</div>
    </div>
  `,
  styles: `
    :host { display: block; position: relative; width: 330px; height: 300px; }
    .scene { position: absolute; inset: 0; }
    .stage {
      position: absolute; left: 50%; top: 55%; width: 0; height: 0; perspective: 1100px;
      transform: translate3d(calc(var(--px, 0) * 14px), calc(var(--py, 0) * 10px), 0);
      transition: transform 0.25s ease-out;
    }
    .bed { position: absolute; transform-style: preserve-3d; width: 0; height: 0; animation: turn 22s ease-in-out infinite; }
    @keyframes turn {
      0%, 100% { transform: rotateX(calc(-22deg - var(--rx, 0) * 1deg)) rotateY(calc(-38deg + var(--ry, 0) * 1deg)); }
      50% { transform: rotateX(calc(-22deg - var(--rx, 0) * 1deg)) rotateY(calc(38deg + var(--ry, 0) * 1deg)); }
    }
    .face { position: absolute; left: 50%; top: 50%; backface-visibility: hidden; box-sizing: border-box; border: 2px solid rgba(255, 255, 255, 0.55); }
    .top { width: 230px; height: 140px; margin: -70px 0 0 -115px; transform: rotateX(90deg) translateZ(32px); transform-style: preserve-3d;
           background: repeating-linear-gradient(90deg, #fbfcff 0 12px, rgba(var(--navy-rgb), 0.5) 12px 17px); border-radius: 10px; }
    .front { width: 230px; height: 64px; margin: -32px 0 0 -115px; transform: translateZ(70px); border-radius: 8px;
             background: repeating-linear-gradient(90deg, #eef2fa 0 12px, rgba(var(--navy-rgb), 0.55) 12px 17px); overflow: hidden; }
    .back { width: 230px; height: 64px; margin: -32px 0 0 -115px; transform: translateZ(-70px) rotateY(180deg); border-radius: 8px;
            background: repeating-linear-gradient(90deg, #dfe6f2 0 12px, rgba(var(--navy-rgb), 0.6) 12px 17px); }
    .left { width: 140px; height: 64px; margin: -32px 0 0 -70px; transform: rotateY(-90deg) translateZ(115px); border-radius: 8px;
            background: linear-gradient(90deg, rgba(var(--navy-rgb), 0.65), rgba(var(--navy-rgb), 0.9)); }
    .right { width: 140px; height: 64px; margin: -32px 0 0 -70px; transform: rotateY(90deg) translateZ(115px); border-radius: 8px;
             background: linear-gradient(90deg, rgba(var(--navy-rgb), 0.9), rgba(var(--navy-rgb), 0.65)); }
    .drape { position: absolute; right: 0; top: 0; bottom: 0; width: 58%; background: linear-gradient(180deg, var(--accent), var(--accent-deep)); }
    .blanket { position: absolute; left: 118px; top: 4px; width: 104px; height: 128px; border-radius: 10px; transform: translateZ(4px);
               background: linear-gradient(135deg, var(--accent), var(--accent-deep)); box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.35) inset; }
    .pillow { position: absolute; border-radius: 22px; background: radial-gradient(ellipse at 35% 30%, #fff, #f2dfd0 70%); box-shadow: 0 3px 6px rgba(0, 0, 0, 0.25); }
    .p1 { left: 12px; top: 14px; width: 70px; height: 112px; transform: translateZ(4px); }
    .p2 { left: 16px; top: 18px; width: 62px; height: 104px; transform: translateZ(10px); }
    .p3 { left: 22px; top: 25px; width: 50px; height: 90px; transform: translateZ(15px); background: radial-gradient(ellipse at 35% 30%, #fff, #f6e6da 75%); }
    .shadow { position: absolute; left: -150px; top: 82px; width: 300px; height: 70px; border-radius: 50%;
              background: radial-gradient(ellipse, rgba(0, 0, 0, 0.45), transparent 70%); filter: blur(6px); }

    .zz { position: absolute; font-family: Montserrat, Manrope, sans-serif; font-weight: 800; color: var(--accent); opacity: 0; text-shadow: 0 4px 14px rgba(0, 0, 0, 0.3);
          animation: rise 5.4s ease-in infinite; }
    .z1 { left: 46%; top: 20%; font-size: 1.1rem; }
    .z2 { left: 54%; top: 14%; font-size: 1.5rem; animation-delay: 1.7s; }
    .z3 { left: 63%; top: 6%; font-size: 2rem; animation-delay: 3.4s; }
    @keyframes rise { 0% { opacity: 0; transform: translate(0, 14px) rotate(-8deg) scale(0.7); } 20% { opacity: 1; } 100% { opacity: 0; transform: translate(26px, -52px) rotate(10deg) scale(1.2); } }

    /* ---- assembling (intro): every piece drops in from above, then the pillows and blanket pop on ---- */
    .assemble .face { animation: drop 0.9s cubic-bezier(0.2, 0.9, 0.3, 1.15) both; }
    .assemble .left { animation-delay: 0.1s; } .assemble .right { animation-delay: 0.25s; } .assemble .back { animation-delay: 0.4s; }
    .assemble .front { animation-delay: 0.55s; } .assemble .top { animation-delay: 0.75s; }
    @keyframes drop { from { opacity: 0; translate: 0 -240px; scale: 0.6; } to { opacity: 1; translate: 0 0; scale: 1; } }
    .assemble .blanket { animation: pop 0.55s cubic-bezier(0.2, 0.9, 0.3, 1.3) 1.35s both; }
    .assemble .p1 { animation: pop 0.55s cubic-bezier(0.2, 0.9, 0.3, 1.3) 1.5s both; }
    .assemble .p2 { animation: pop 0.55s cubic-bezier(0.2, 0.9, 0.3, 1.3) 1.62s both; }
    .assemble .p3 { animation: pop 0.55s cubic-bezier(0.2, 0.9, 0.3, 1.3) 1.74s both; }
    @keyframes pop { from { opacity: 0; scale: 0.2; } to { opacity: 1; scale: 1; } }
    .assemble .shadow { animation: fadeIn 1.4s ease 0.5s both; }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
    .assemble .z1 { animation-delay: 2.3s; } .assemble .z2 { animation-delay: 3.6s; } .assemble .z3 { animation-delay: 4.9s; }

    /* ---- reacting to the person ---- */
    /* password box in focus: the blanket is pulled over the whole mattress and a lock appears */
    .blanket, .drape, .pillow { transition: left 0.6s cubic-bezier(0.3, 0.8, 0.3, 1), width 0.6s cubic-bezier(0.3, 0.8, 0.3, 1), opacity 0.4s ease, scale 0.5s ease; }
    .privacy .blanket { left: 4px; width: 222px; top: 3px; height: 134px; }
    .privacy .drape { width: 100%; }
    .privacy .pillow { opacity: 0.0; scale: 0.7; }
    .privacy .zz { visibility: hidden; }
    .lockico { position: absolute; left: 50%; top: 14%; margin-left: -22px; width: 44px; height: 44px; border-radius: 50%; display: grid; place-items: center; font-size: 1.1rem;
               background: var(--accent); color: var(--accent-ink); box-shadow: 0 10px 24px rgba(0, 0, 0, 0.35); opacity: 0; scale: 0.3; transition: opacity 0.35s ease, scale 0.45s cubic-bezier(0.2, 0.9, 0.3, 1.4); }
    .privacy .lockico { opacity: 1; scale: 1; }
    /* it worked: the mattress hops, a tick pops up and sparks fly out */
    .joy { animation: hop 0.9s cubic-bezier(0.3, 0.7, 0.4, 1) both; }
    @keyframes hop { 0% { transform: translateY(0); } 25% { transform: translateY(-34px); } 45% { transform: translateY(0); } 62% { transform: translateY(-16px); } 80%, 100% { transform: translateY(0); } }
    .tick { position: absolute; left: 50%; top: 12%; margin-left: -26px; width: 52px; height: 52px; border-radius: 50%; display: grid; place-items: center; font-size: 1.4rem; color: #fff;
            background: #1f9d63; box-shadow: 0 12px 28px rgba(31, 157, 99, 0.5); opacity: 0; scale: 0.2; }
    .joy .tick { animation: tickIn 0.7s cubic-bezier(0.2, 0.9, 0.3, 1.5) 0.15s both; }
    @keyframes tickIn { from { opacity: 0; scale: 0.2; } to { opacity: 1; scale: 1; } }
    .sparks { position: absolute; left: 50%; top: 42%; width: 0; height: 0; }
    .sparks i { position: absolute; left: 0; top: 0; width: var(--s, 6px); height: var(--s, 6px); border-radius: 50%; background: var(--c, #fff); opacity: 0; }
    .joy .sparks i { animation: spark 0.95s cubic-bezier(0.1, 0.7, 0.3, 1) both; }
    @keyframes spark { 0% { opacity: 1; transform: translate(0, 0) scale(1); } 100% { opacity: 0; transform: translate(var(--x), var(--y)) scale(0.2); } }
    /* it failed: a quick sideways shake */
    .oops .stage { animation: shakeBed 0.5s ease both; }
    @keyframes shakeBed { 0%, 100% { margin-left: 0; } 15% { margin-left: -14px; } 30% { margin-left: 12px; } 45% { margin-left: -9px; } 60% { margin-left: 7px; } 80% { margin-left: -3px; } }
    .oops .blanket { opacity: 0.55; }

    @media (prefers-reduced-motion: reduce) {
      .joy, .joy .tick, .joy .sparks i, .oops .stage { animation: none; }
      .joy .tick { opacity: 1; scale: 1; }
      .bed, .zz, .assemble .face, .assemble .blanket, .assemble .pillow, .assemble .shadow { animation: none; }
      .bed { transform: rotateX(-22deg) rotateY(-30deg); } .zz { opacity: 0.8; } .stage { transition: none; }
    }
  `,
})
export class Bed3d {
  assemble = input(false);
  mood = inject(AuthMood);

  /** Sparks for the "it worked" moment: each flies out in its own direction. */
  sparks = Array.from({ length: 22 }, (_, i) => {
    const a = (i / 22) * Math.PI * 2 + (i % 2 ? 0.15 : 0), dist = 90 + (i % 5) * 22;
    return { x: Math.round(Math.cos(a) * dist), y: Math.round(Math.sin(a) * dist * 0.8), c: i % 3 === 0 ? '#ffffff' : i % 3 === 1 ? 'var(--accent)' : 'var(--navy-light)', s: 5 + (i % 4) * 2, d: (i % 6) * 25 };
  });
}
