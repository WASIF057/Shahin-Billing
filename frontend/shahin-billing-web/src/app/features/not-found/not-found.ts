import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Bed3d } from '../../shared/bed3d';

/** The page shown for any address that does not exist. Open to everyone, logged in or not. */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink, Bed3d],
  template: `
    <main class="nf">
      <div class="glow g1"></div><div class="glow g2"></div>
      <div class="card">
        <div class="code" aria-hidden="true">404</div>
        <app-bed3d class="bed" />
        <h1>We couldn’t find that page</h1>
        <p>The address may have been typed wrong, or the page has moved. Don’t worry, nothing is lost. The mattress here is just having a nap.</p>
        <div class="actions">
          <a routerLink="/" class="btn primary"><i class="pi pi-home"></i> Go to the home page</a>
          <button type="button" class="btn" (click)="back()"><i class="pi pi-arrow-left"></i> Go back</button>
        </div>
        <nav class="links" aria-label="Other pages">
          <a routerLink="/privacy">Privacy policy</a>
          <a routerLink="/terms">Terms</a>
          <a routerLink="/cookies">Cookies</a>
        </nav>
      </div>
    </main>
  `,
  styles: `
    :host { display: block; }
    .nf { min-height: 100vh; display: grid; place-items: center; padding: 24px; position: relative; overflow: hidden; color: #fff;
          background: linear-gradient(160deg, var(--side-a), var(--side-b)); }
    .glow { position: absolute; border-radius: 50%; filter: blur(70px); opacity: 0.5; pointer-events: none; }
    .g1 { width: 400px; height: 400px; left: -100px; top: 4%; background: rgba(var(--navy-rgb), 0.8); }
    .g2 { width: 340px; height: 340px; right: -70px; bottom: 2%; background: rgba(var(--accent-rgb), 0.4); }
    .card { position: relative; z-index: 1; text-align: center; max-width: 560px; display: flex; flex-direction: column; align-items: center; gap: 12px; }
    .code { font-family: Montserrat, Manrope, sans-serif; font-weight: 800; font-size: clamp(4.5rem, 16vw, 9rem); line-height: 1; letter-spacing: 0.04em;
            background: linear-gradient(180deg, #fff 30%, rgba(255, 255, 255, 0.25)); -webkit-background-clip: text; background-clip: text; color: transparent; }
    .bed { margin: -70px 0 -20px; transform: scale(0.9); }
    h1 { margin: 0; color: #fff; font-family: Montserrat, Manrope, sans-serif; font-weight: 800; font-size: clamp(1.5rem, 4vw, 2.2rem); }
    h1::after { display: none; }
    p { margin: 0; color: #c9d6ec; line-height: 1.6; max-width: 46ch; }
    .actions { display: flex; flex-wrap: wrap; gap: 12px; justify-content: center; margin-top: 10px; }
    .btn { display: inline-flex; align-items: center; gap: 9px; padding: 12px 22px; border-radius: 12px; font: inherit; font-weight: 800; text-decoration: none; cursor: pointer;
           color: #fff; background: rgba(255, 255, 255, 0.1); border: 1px solid rgba(255, 255, 255, 0.28); }
    .btn:hover { background: rgba(255, 255, 255, 0.18); }
    .btn.primary { background: linear-gradient(135deg, var(--accent), var(--accent-deep)); color: var(--accent-ink); border: 0; box-shadow: 0 10px 24px rgba(var(--accent-rgb), 0.35); }
    .links { display: flex; gap: 20px; margin-top: 14px; font-size: 0.85rem; }
    .links a { color: #9fb0cc; text-decoration: underline; text-underline-offset: 3px; }
    .links a:hover { color: #fff; }
  `,
})
export class NotFoundPage {
  back() { if (window.history.length > 1) window.history.back(); else window.location.assign('/'); }
}
