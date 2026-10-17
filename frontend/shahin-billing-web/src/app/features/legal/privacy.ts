import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalPage } from './legal-page';

/**
 * The privacy policy. It is a public page (no login needed) and describes what this app really does with data.
 * If the app changes how it handles data, or the contact details change, edit this file and update `updated`.
 */
@Component({
  selector: 'app-privacy',
  imports: [LegalPage, RouterLink],
  template: `
    <app-legal-page title="Privacy policy" [updated]="updated">
        <p>
          This policy explains what information the Shahin Enterprises billing and ordering website collects, why, who can see it,
          and what choices you have. We have tried to write it in plain language.
        </p>

        <h2>1. Who we are</h2>
        <p>
          {{ company }} is a manufacturer of beds and pillows based in {{ place }}. This website is our GST billing and ordering
          system. It is used by us, our staff and our dealers and clients (together, "you").
        </p>

        <h2>2. What information we collect</h2>
        <ul>
          <li><strong>Your login:</strong> your name, email address and role. Your password is never stored as typed; only a one-way scrambled form of it is kept.</li>
          <li><strong>Business details:</strong> our business name, address, GSTIN, bank details, logo and signature, which are printed on bills.</li>
          <li><strong>Dealer and client details:</strong> name, GSTIN, phone number, email address, towns or cities, and notes we keep about them, including any special rates.</li>
          <li><strong>Billing records:</strong> bills, items, payments, orders and the notes written on them.</li>
          <li><strong>Activity record:</strong> who did what and when inside the system (for example "bill created" or "payment recorded"). It is kept for 365 days.</li>
          <li><strong>Login safety:</strong> a one-time code sent to your email when you log in or sign up (it expires after 10 minutes), the letters you type in the security check, and your internet address, which is used only for a moment to slow down repeated attempts and is not stored.</li>
          <li><strong>On your own device:</strong> your login session, your colour and light or dark choice, whether the menu is collapsed, and whether you have seen the opening animation. We do not use advertising or tracking cookies. See our <a routerLink="/cookies">Cookie policy</a> for the full list.</li>
        </ul>

        <h2>3. How we use it</h2>
        <ul>
          <li>To create bills, record payments and take and process orders.</li>
          <li>To send emails you or we have set up, such as a copy of a bill, a payment update, a payment reminder or an order confirmation. These can include bill details and a PDF of the bill.</li>
          <li>To keep the system safe: logging in, preventing misuse, and finding and fixing problems.</li>
          <li>To keep backups and to meet our tax, accounting and legal duties.</li>
        </ul>
        <p>We do not sell your information, and we do not use it for advertising.</p>

        <h2>4. Who can see it</h2>
        <ul>
          <li><strong>Our owner and staff</strong> can see what their role allows. Staff cannot see reports, settings, or other restricted screens.</li>
          <li><strong>Dealers and clients</strong> with an ordering login can see only their own orders and the products on offer. They cannot see prices set for other clients, other clients' orders, or any billing records.</li>
          <li><strong>Service providers</strong> who help us run the site: Cloudflare, which carries the traffic between you and our server; our email provider, which delivers the emails we send; and Google Fonts, which supplies the text style used on the pages (loading it shares your internet address and browser type with Google).</li>
          <li><strong>Authorities</strong>, only when the law requires us to share something.</li>
        </ul>

        <h2>5. Where it is stored and how we protect it</h2>
        <p>
          Your information is stored in a database on our own server. Traffic to the website is encrypted (HTTPS). Passwords are kept only in
          scrambled form. Each person sees only what their role allows, the number of login and other requests is limited to stop guessing and
          flooding, and logins are protected by an emailed one-time code and a security check. A copy of the data is saved automatically every day
          and the newest 14 copies are kept. No system is completely risk free, but we work to keep your information safe.
        </p>

        <h2>6. How long we keep it</h2>
        <ul>
          <li>Bills, payments and related records: for as long as tax and accounting law requires us to keep them.</li>
          <li>Activity record: 365 days.</li>
          <li>One-time login codes: 10 minutes.</li>
          <li>Backup copies: the newest 14 daily copies.</li>
          <li>Your login and contact details: while your account is active, and for a reasonable time afterwards where we need them for our records.</li>
        </ul>

        <h2>7. Your choices</h2>
        <p>
          You can ask us to show you the information we hold about you, to correct it, or to delete it. Some records, such as bills, must be kept for
          the period the law requires, so we may not be able to delete those until then. You can also ask us to close an ordering login. If you
          agreed to something, you can take that agreement back. To do any of this, contact us using the details below. We will reply as soon as we can.
        </p>

        <h2>8. Children</h2>
        <p>This website is for businesses. It is not meant for children, and we do not knowingly collect information about them.</p>

        <h2>9. Changes to this policy</h2>
        <p>If we change this policy we will update the date at the top of the page. For larger changes we will also tell you in the system.</p>

        <h2>10. Contact us</h2>
        <p>
          {{ company }}<br />
          {{ address }}<br />
          Phone: <a [href]="'tel:' + phone">{{ phone }}</a>
        </p>
    </app-legal-page>
  `,
})
export class PrivacyPage {
  /** Edit these when the business details change. */
  readonly company = 'Shahin Enterprises';
  readonly place = 'Karkala';
  readonly address = 'Bunglegudde, Karkala';
  readonly phone = '9845845109';
  readonly updated = '17 October 2026';
}
