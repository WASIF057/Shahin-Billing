import { inject, Injectable, signal } from '@angular/core';
import { AuthService } from './auth.service';
import { BusinessApi } from './api.service';

export interface ThemeChoice { id: string; name: string; primary: string; accent: string; }

/** Ready-made colour themes. "Ocean" is the original look. */
export const THEMES: ThemeChoice[] = [
  { id: 'ocean',   name: 'Ocean',   primary: '#2a5b8f', accent: '#e8a33d' },
  { id: 'indigo',  name: 'Indigo',  primary: '#3f4fc2', accent: '#f2a93b' },
  { id: 'violet',  name: 'Violet',  primary: '#6d48c4', accent: '#f0a347' },
  { id: 'rose',    name: 'Rose',    primary: '#c0334d', accent: '#f0b34c' },
  { id: 'orange',  name: 'Sunset',  primary: '#d1590e', accent: '#2f7dc0' },
  { id: 'emerald', name: 'Emerald', primary: '#0f7a5e', accent: '#eeb02e' },
  { id: 'teal',    name: 'Teal',    primary: '#0c8696', accent: '#f09a3e' },
  { id: 'slate',   name: 'Slate',   primary: '#46566d', accent: '#e8a33d' },
];

const KEY = 'shahin.theme';
const MODE_KEY = 'shahin.mode';
export type Mode = 'light' | 'dark' | 'auto';

type Rgb = [number, number, number];
const toRgb = (hex: string): Rgb => {
  const h = hex.replace('#', '');
  const n = parseInt(h.length === 3 ? h.split('').map(c => c + c).join('') : h, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
};
const toHex = (c: Rgb) => '#' + c.map(v => Math.round(Math.max(0, Math.min(255, v))).toString(16).padStart(2, '0')).join('');
/** Mixes `hex` with `other` ; amount = how much of `other` (0 to 1). */
const mix = (hex: string, other: string, amount: number) => {
  const a = toRgb(hex), b = toRgb(other);
  return toHex([0, 1, 2].map(i => a[i] + (b[i] - a[i]) * amount) as Rgb);
};
const isHex = (v: unknown): v is string => typeof v === 'string' && /^#[0-9a-fA-F]{6}$/.test(v);

/**
 * The colour theme. Every colour in the app comes from CSS variables (see styles.scss); this service
 * works out the whole set from one main colour and one accent colour and writes them on <html>.
 * The choice is remembered in this browser.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private auth = inject(AuthService);
  private api = inject(BusinessApi);
  private saveTimer: ReturnType<typeof setTimeout> | null = null;
  readonly themes = THEMES;
  readonly primary = signal(THEMES[0].primary);
  readonly accent = signal(THEMES[0].accent);
  /** Light, dark, or follow the computer's setting. */
  readonly mode = signal<Mode>('light');

  constructor() {
    try {
      const saved = JSON.parse(localStorage.getItem(KEY) ?? 'null');
      if (saved && isHex(saved.primary) && isHex(saved.accent)) { this.primary.set(saved.primary); this.accent.set(saved.accent); }
    } catch { /* private window or bad data: use the default */ }
    try {
      const m = localStorage.getItem(MODE_KEY);
      if (m === 'light' || m === 'dark' || m === 'auto') this.mode.set(m);
    } catch { /* default */ }
    // in Auto mode, follow the computer when it switches between light and dark
    window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener('change', () => { if (this.mode() === 'auto') this.apply(); });
    this.apply();
  }

  setMode(m: Mode) {
    this.mode.set(m);
    try { localStorage.setItem(MODE_KEY, m); } catch { /* not remembered */ }
    this.apply();
  }

  isDark() { return this.mode() === 'dark' || (this.mode() === 'auto' && !!window.matchMedia?.('(prefers-color-scheme: dark)').matches); }

  /** Only the owner may change the colours; they are saved for the whole business. */
  canEdit() { return this.auth.isOwner(); }

  /** The business's saved colours (from the server). Empty means the default look. */
  applyShared(primary?: string, accent?: string) {
    this.primary.set(isHex(primary) ? primary : THEMES[0].primary);
    this.accent.set(isHex(accent) ? accent : THEMES[0].accent);
    this.cache();
    this.apply();
  }

  choose(t: ThemeChoice) { if (!this.canEdit()) return; this.primary.set(t.primary); this.accent.set(t.accent); this.save(); }
  /** A colour picked freely; the accent stays as it is. */
  setPrimary(hex: string) { if (this.canEdit() && isHex(hex)) { this.primary.set(hex); this.save(); } }
  setAccent(hex: string) { if (this.canEdit() && isHex(hex)) { this.accent.set(hex); this.save(); } }
  reset() { this.choose(THEMES[0]); }
  isActive(t: ThemeChoice) { return t.primary.toLowerCase() === this.primary().toLowerCase() && t.accent.toLowerCase() === this.accent().toLowerCase(); }

  /** Changes show at once; the save to the server waits until the owner stops choosing (half a second). */
  private save() {
    this.apply();
    this.cache();
    if (this.saveTimer) clearTimeout(this.saveTimer);
    this.saveTimer = setTimeout(() => {
      this.api.updateTheme({ primary: this.primary(), accent: this.accent() }).subscribe({ error: () => { /* the colours still work on this screen */ } });
    }, 500);
  }

  /** Remembered in this browser so the colours are right from the first moment, before the server answers. */
  private cache() {
    try { localStorage.setItem(KEY, JSON.stringify({ primary: this.primary(), accent: this.accent() })); } catch { /* not remembered */ }
  }

  private apply() {
    const p = this.primary(), a = this.accent();
    const style = document.documentElement.style;
    const set = (k: string, v: string) => style.setProperty(k, v);
    const pr = toRgb(p), ar = toRgb(a);
    const dark = this.isDark();
    document.documentElement.classList.toggle('app-dark', dark);
    style.setProperty('color-scheme', dark ? 'dark' : 'light');
    const card = '#151b28';   // the dark card colour (see styles.scss)
    set('--navy', dark ? mix(p, '#ffffff', 0.28) : p);
    set('--navy-deep', mix(p, '#000000', 0.28));
    set('--navy-soft', dark ? mix(p, card, 0.78) : mix(p, '#ffffff', 0.9));
    set('--navy-hover', dark ? mix(p, card, 0.9) : mix(p, '#ffffff', 0.95));
    set('--navy-rgb', pr.join(', '));
    set('--navy-light', mix(p, '#ffffff', 0.22));
    set('--logo-ink', mix(p, '#000000', 0.38));
    set('--logo-warm', a);
    set('--logo-pillow', mix(a, '#ffffff', 0.78));
    set('--accent', a);
    set('--accent-deep', mix(a, '#000000', 0.12));
    set('--accent-rgb', ar.join(', '));
    set('--accent-ink', toRgb(a).reduce((s, v) => s + v, 0) > 480 ? '#2a1a00' : '#ffffff');
    set('--grad', `linear-gradient(135deg, ${p} 0%, ${mix(p, '#ffffff', 0.22)} 100%)`);
    set('--side-a', mix(p, '#06090f', 0.8));
    set('--side-b', mix(p, '#0a1020', 0.58));
    set('--bg', dark ? mix(p, '#090d15', 0.92) : mix(p, '#ffffff', 0.955));
    set('--line', dark ? mix(p, '#2b3446', 0.82) : mix(p, '#e6e9ef', 0.88));

    // PrimeNG's own primary colour (buttons, focus rings, switches, selected rows...). Writing its CSS variables
    // directly changes everything at once; updatePrimaryPalette() rebuilds the whole theme and is slow.
    const shades: Record<number, string> = {
      50: mix(p, '#ffffff', 0.93), 100: mix(p, '#ffffff', 0.84), 200: mix(p, '#ffffff', 0.68), 300: mix(p, '#ffffff', 0.5),
      400: mix(p, '#ffffff', 0.26), 500: p, 600: mix(p, '#000000', 0.14), 700: mix(p, '#000000', 0.28),
      800: mix(p, '#000000', 0.42), 900: mix(p, '#000000', 0.56), 950: mix(p, '#000000', 0.7),
    };
    for (const [k, v] of Object.entries(shades)) set(`--p-primary-${k}`, v);
  }
}
