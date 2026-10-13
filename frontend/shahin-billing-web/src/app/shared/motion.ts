import { Directive, ElementRef, inject, input, NgZone, OnDestroy, OnInit, effect } from '@angular/core';
import { InrPipe } from './inr.pipe';

/** Animations only run for people with a mouse and without "reduce motion" switched on in their device settings. */
export function canAnimate(): boolean {
  if (typeof window === 'undefined' || !window.matchMedia) return false;
  return !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}
function hasHover(): boolean {
  return typeof window !== 'undefined' && !!window.matchMedia?.('(hover: hover)').matches;
}

/**
 * 3D tilt that follows the mouse. It does not move anything itself: it writes CSS variables on the element
 * (--rx and --ry in degrees, --px and --py from -1 to 1) and the styles decide what turns or shifts.
 * Use as `[appTilt]="8"` (the number is the largest angle in degrees).
 */
@Directive({ selector: '[appTilt]' })
export class Tilt implements OnInit, OnDestroy {
  max = input<number | string>(8, { alias: 'appTilt' });
  private el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private zone = inject(NgZone);
  private frame = 0;
  private off: (() => void)[] = [];

  ngOnInit() {
    if (!canAnimate() || !hasHover()) return;
    // Runs outside Angular so mouse movement never triggers change detection
    this.zone.runOutsideAngular(() => {
      const move = (e: PointerEvent) => {
        if (this.frame) return;
        this.frame = requestAnimationFrame(() => {
          this.frame = 0;
          const r = this.el.getBoundingClientRect();
          const x = Math.max(-1, Math.min(1, ((e.clientX - r.left) / r.width) * 2 - 1));
          const y = Math.max(-1, Math.min(1, ((e.clientY - r.top) / r.height) * 2 - 1));
          const m = Number(this.max()) || 8;
          this.set(-y * m, x * m, x, y);
        });
      };
      const leave = () => this.set(0, 0, 0, 0);
      this.el.addEventListener('pointermove', move);
      this.el.addEventListener('pointerleave', leave);
      this.off.push(() => this.el.removeEventListener('pointermove', move), () => this.el.removeEventListener('pointerleave', leave));
    });
  }

  private set(rx: number, ry: number, px: number, py: number) {
    const s = this.el.style;
    s.setProperty('--rx', rx.toFixed(2));
    s.setProperty('--ry', ry.toFixed(2));
    s.setProperty('--px', px.toFixed(3));
    s.setProperty('--py', py.toFixed(3));
  }

  ngOnDestroy() { this.off.forEach(f => f()); if (this.frame) cancelAnimationFrame(this.frame); }
}

/** Counts a rupee amount up to its value (and from the old value to the new one when it changes). Use as `<span [appCountUp]="total"></span>`. */
@Directive({ selector: '[appCountUp]' })
export class CountUp implements OnDestroy {
  value = input<number | null | undefined>(0, { alias: 'appCountUp' });
  decimals = input(0);
  private el = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private pipe = new InrPipe();
  private shown = 0;
  private frame = 0;

  constructor() {
    effect(() => this.run(this.value() ?? 0, this.decimals()));
  }

  private run(target: number, decimals: number) {
    cancelAnimationFrame(this.frame);
    const text = (n: number) => (this.el.textContent = this.pipe.transform(n, true, decimals));
    if (!canAnimate() || this.shown === target) { this.shown = target; text(target); return; }
    const from = this.shown, start = performance.now(), ms = 800;
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / ms);
      const eased = 1 - Math.pow(1 - t, 3);   // fast at first, slow at the end
      this.shown = from + (target - from) * eased;
      text(this.shown);
      if (t < 1) this.frame = requestAnimationFrame(step); else { this.shown = target; text(target); }
    };
    this.frame = requestAnimationFrame(step);
  }

  ngOnDestroy() { cancelAnimationFrame(this.frame); }
}
