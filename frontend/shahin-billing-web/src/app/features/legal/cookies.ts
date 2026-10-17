import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalPage } from './legal-page';

/**
 * The cookie policy. This site sets NO cookies itself. It keeps a few small items in the browser's own storage
 * (localStorage and sessionStorage), which is what this page lists. If a new item is stored in the browser, add it to the table
 * and update `updated`.
 */
@Component({
  selector: 'app-cookies',
  imports: [LegalPage, RouterLink],
  template: `
    <app-legal-page title="Cookie policy" [updated]="updated">
      <p>
        This page explains how the {{ company }} billing and ordering website uses cookies and similar storage in your browser. It goes
        with our <a routerLink="/privacy">Privacy policy</a>.
      </p>

      <h2>1. The short version</h2>
      <ul>
        <li>This site does <strong>not</strong> set any cookies of its own.</li>
        <li>It does <strong>not</strong> use advertising, analytics or tracking cookies, and it does not follow you around other websites.</li>
        <li>It saves a few small items in your browser's own storage so that the site works and remembers your choices. Everything in the table below is needed for that, so we do not ask for consent first.</li>
      </ul>

      <h2>2. What is saved in your browser</h2>
      <p>Browser storage works like cookies: small pieces of text kept on your own device. They are never used to identify you to anyone else.</p>
      <div class="legal-table-wrap">
        <table class="legal-table">
          <thead><tr><th>Name</th><th>What it is for</th><th>How long</th></tr></thead>
          <tbody>
            @for (r of items; track r.name) {
              <tr>
                <td><code>{{ r.name }}</code></td>
                <td>{{ r.use }}</td>
                <td>{{ r.keep }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>

      <h2>3. Cookies from other services</h2>
      <ul>
        <li><strong>Cloudflare</strong> carries the traffic to our site and helps protect it. To tell real visitors from automated attacks it may set its own security cookies in your browser. These are used only for security.</li>
        <li><strong>Google Fonts</strong> supplies the text style used on the pages. It does not set cookies for us, but your internet address and browser type are shared with Google when the fonts load.</li>
      </ul>
      <p>We do not control these services. You can read their own policies on their websites.</p>

      <h2>4. Your choices</h2>
      <ul>
        <li>You can clear the items above at any time in your browser's settings (look for "clear browsing data" or "site data"). You will be logged out and your choices, such as the colour and dark mode, will be forgotten.</li>
        <li>Logging out removes your login session from the browser.</li>
        <li>If you block browser storage completely, the site cannot keep you logged in, so you will not be able to use it.</li>
      </ul>

      <h2>5. Changes to this policy</h2>
      <p>If we start storing something new in your browser, we will add it to the table and update the date at the top of the page.</p>

      <h2>6. Contact us</h2>
      <p>
        {{ company }}<br />
        {{ address }}<br />
        Phone: <a [href]="'tel:' + phone">{{ phone }}</a>
      </p>
    </app-legal-page>
  `,
})
export class CookiesPage {
  /** Edit these when the business details change. */
  readonly company = 'Shahin Enterprises';
  readonly address = 'Bunglegudde, Karkala';
  readonly phone = '9845845109';
  readonly updated = '17 October 2026';

  /** Everything the site keeps in the browser. */
  readonly items = [
    { name: 'shahin-billing-auth', use: 'Keeps you logged in: your login session, which lasts up to 8 hours.', keep: 'Until you log out, the session ends, or you clear your browser data' },
    { name: 'shahin.theme', use: 'The colour theme of the site.', keep: 'Until you clear your browser data' },
    { name: 'shahin.mode', use: 'Your choice of light, dark or automatic mode.', keep: 'Until you clear your browser data' },
    { name: 'shahin.sidebar', use: 'Whether the side menu is open or collapsed.', keep: 'Until you clear your browser data' },
    { name: 'shahin.intro.seen', use: 'Remembers that you have seen the opening animation on the login page, so it does not play again straight away.', keep: 'Until you close the browser tab or window' },
    { name: 'orders-sound', use: 'Whether the new-order sound is on or off (owner and staff only).', keep: 'Until you clear your browser data' },
    { name: 'orders-seen', use: 'Which new orders have already rung the alert, so the same order does not ring twice (owner and staff only).', keep: 'Until you clear your browser data' },
  ];
}
