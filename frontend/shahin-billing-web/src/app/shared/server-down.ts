import { Component, inject } from '@angular/core';
import { ServerStatus } from '../core/server-status.service';
import { Bed3d } from './bed3d';

/**
 * The full-screen "we can't reach the server" screen. It appears by itself when the server does not answer (this is the website's
 * version of a 502 / 503 / 504 page), keeps trying in the background, and disappears when the server is back.
 */
@Component({
  selector: 'app-server-down',
  imports: [Bed3d],
  template: `
    @if (s.down()) {
      <div class="sd" role="alertdialog" aria-live="assertive" aria-labelledby="sd-title">
        <div class="glow g1"></div><div class="glow g2"></div>
        <div class="card">
          <app-bed3d class="bed" />
          <h2 id="sd-title">{{ s.offline() ? 'You’re offline' : 'We can’t reach the server' }}</h2>
          <p>
            @if (s.offline()) { Your device has no internet connection right now. Please check your Wi-Fi or mobile data. }
            @else { This is usually brief, for example while we update the system. Your work already saved is safe. }
            We will keep trying and bring you back automatically.
          </p>
          <button type="button" class="again" (click)="s.retryNow()" [disabled]="s.checking()">
            <i class="pi pi-refresh" [class.pi-spin]="s.checking()"></i>
            {{ s.checking() ? 'Checking…' : 'Try again now' }}
          </button>
          <span class="hint">{{ s.checking() ? 'Checking the server…' : 'Trying again in ' + s.nextTry() + 's' }}</span>
        </div>
      </div>
    }
  `,
  styles: `
    .sd { position: fixed; inset: 0; z-index: 3500; display: grid; place-items: center; padding: 20px; overflow: hidden; color: #fff;
          background: linear-gradient(160deg, var(--side-a), var(--side-b)); animation: sdIn 0.35s ease both; }
    @keyframes sdIn { from { opacity: 0; } to { opacity: 1; } }
    .glow { position: absolute; border-radius: 50%; filter: blur(70px); opacity: 0.5; pointer-events: none; }
    .g1 { width: 380px; height: 380px; left: -90px; top: 6%; background: rgba(var(--navy-rgb), 0.8); }
    .g2 { width: 320px; height: 320px; right: -60px; bottom: 4%; background: rgba(var(--accent-rgb), 0.4); }
    .card { position: relative; z-index: 1; max-width: 520px; text-align: center; display: flex; flex-direction: column; align-items: center; gap: 12px; }
    .bed { transform: scale(0.85); margin: -26px 0 -10px; }
    h2 { margin: 0; color: #fff; font-family: Montserrat, Manrope, sans-serif; font-weight: 800; font-size: clamp(1.5rem, 4vw, 2.1rem); }
    p { margin: 0; color: #c9d6ec; line-height: 1.6; max-width: 44ch; }
    .again { display: inline-flex; align-items: center; gap: 10px; margin-top: 8px; padding: 12px 26px; border-radius: 12px; border: 0; cursor: pointer; font: inherit; font-weight: 800;
             background: linear-gradient(135deg, var(--accent), var(--accent-deep)); color: var(--accent-ink); box-shadow: 0 10px 24px rgba(var(--accent-rgb), 0.35); }
    .again:disabled { opacity: 0.75; cursor: default; }
    .hint { font-size: 0.82rem; color: #9fb0cc; }
    @media (prefers-reduced-motion: reduce) { .sd { animation: none; } }
  `,
})
export class ServerDown {
  s = inject(ServerStatus);
}
