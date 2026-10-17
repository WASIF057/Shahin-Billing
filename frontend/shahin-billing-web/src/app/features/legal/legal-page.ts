import { Component, input, ViewEncapsulation } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppLogo } from '../../shared/app-logo';

/**
 * The frame shared by the legal documents (privacy policy, terms and conditions): logo, back button, title, date and a
 * card for the text. The text itself goes inside the tag: <app-legal-page title="..." updated="..."> ... </app-legal-page>.
 * Styles are not encapsulated so they reach the text that is placed inside.
 */
@Component({
  selector: 'app-legal-page',
  imports: [RouterLink, AppLogo],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="legal">
      <header class="legal-top">
        <a routerLink="/" class="legal-logo" aria-label="Shahin Enterprises"><app-logo /></a>
        <a routerLink="/" class="legal-back"><i class="pi pi-arrow-left"></i> Back</a>
      </header>

      <article class="legal-doc">
        <h1>{{ title() }}</h1>
        <p class="legal-muted">Last updated: {{ updated() }}</p>
        <ng-content />
        <nav class="legal-end" aria-label="Legal pages">
          <a routerLink="/privacy">Privacy policy</a>
          <a routerLink="/terms">Terms and conditions</a>
          <a routerLink="/cookies">Cookie policy</a>
          <a routerLink="/">Back to the site</a>
        </nav>
      </article>
    </div>
  `,
  styles: `
    app-legal-page { display: block; min-height: 100vh; background: var(--bg); }
    .legal { max-width: 820px; margin: 0 auto; padding: 22px 20px 70px; }
    .legal-top { display: flex; align-items: center; justify-content: space-between; gap: 16px; margin-bottom: 18px; }
    .legal-logo { display: block; width: 200px; background: #fff; border-radius: 14px; padding: 8px 12px; box-shadow: var(--shadow-sm); border: 1px solid var(--line); }
    .legal-back { display: inline-flex; align-items: center; gap: 8px; font-weight: 700; text-decoration: none; padding: 8px 14px; border-radius: 999px; background: var(--surface); border: 1px solid var(--line); color: var(--navy); }
    .legal-back:hover { border-color: var(--navy); }
    .legal-doc { background: var(--surface); border: 1px solid var(--line); border-radius: 20px; padding: 34px 38px; box-shadow: var(--shadow-sm); line-height: 1.7; }
    .legal-doc h1 { font-size: 2rem; margin-bottom: 4px; }
    .legal-doc h1::after { display: none; }
    .legal-doc h2 { font-size: 1.12rem; margin: 28px 0 8px; color: var(--navy); }
    .legal-doc p, .legal-doc li { color: var(--ink); }
    .legal-doc ul { padding-left: 1.2rem; margin: 8px 0; display: grid; gap: 8px; }
    .legal-muted { color: var(--muted); margin: 0 0 18px; }
    .legal-table-wrap { overflow-x: auto; margin: 10px 0; border: 1px solid var(--line); border-radius: 12px; }
    .legal-table { width: 100%; border-collapse: collapse; font-size: 0.92rem; line-height: 1.5; }
    .legal-table th { text-align: left; padding: 10px 14px; background: var(--navy-hover); font-size: 0.78rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--muted); }
    .legal-table td { padding: 10px 14px; border-top: 1px solid var(--line); vertical-align: top; }
    .legal-table code { background: var(--navy-soft); color: var(--navy); padding: 2px 7px; border-radius: 6px; font-size: 0.85em; white-space: nowrap; }
    .legal-end { display: flex; flex-wrap: wrap; gap: 8px 22px; margin-top: 30px; padding-top: 16px; border-top: 1px solid var(--line); font-weight: 600; }
    @media (max-width: 640px) { .legal-doc { padding: 22px 18px; } .legal-doc h1 { font-size: 1.6rem; } .legal-logo { width: 160px; } }
  `,
})
export class LegalPage {
  title = input.required<string>();
  updated = input.required<string>();
}
