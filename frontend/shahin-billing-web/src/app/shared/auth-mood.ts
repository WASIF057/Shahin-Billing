import { Injectable, signal } from '@angular/core';

/** How the mattress on the login pages should react to what the person is doing. */
export type Mood = 'idle' | 'privacy' | 'joy' | 'oops';

/**
 * Shared between a login page and the mattress (app-bed3d):
 * 'privacy' = a password box has focus (the blanket covers the mattress, a lock appears),
 * 'joy' = it worked (the mattress hops and throws sparks), 'oops' = it failed (the mattress shakes).
 */
@Injectable({ providedIn: 'root' })
export class AuthMood {
  readonly mood = signal<Mood>('idle');
  private timer: ReturnType<typeof setTimeout> | null = null;

  set(m: Mood) { this.clear(); this.mood.set(m); }

  /** Shows a mood for a moment, then goes back to idle. */
  pulse(m: Mood, ms: number) {
    this.set(m);
    this.timer = setTimeout(() => this.mood.set('idle'), ms);
  }

  reset() { this.set('idle'); }

  private clear() { if (this.timer) { clearTimeout(this.timer); this.timer = null; } }
}
