import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LegalPage } from './legal-page';

/**
 * Terms and conditions for using the website. These are NOT the sales terms printed on bills (those are set in My business).
 * Edit the text here when the rules change, and update `updated`.
 */
@Component({
  selector: 'app-terms',
  imports: [LegalPage, RouterLink],
  template: `
    <app-legal-page title="Terms and conditions" [updated]="updated">
      <p>
        These terms explain the rules for using the {{ company }} billing and ordering website. By creating an account, logging in or placing an
        order through the site, you agree to them. If you do not agree, please do not use the site.
      </p>

      <h2>1. Who can use the site</h2>
      <ul>
        <li>The site is for businesses and their authorised people: our owner and staff, and the dealers and clients we have given an ordering login to.</li>
        <li>You must give correct information when you sign up or place an order, and keep it up to date.</li>
        <li>Some accounts are created by us for you. Please use them only for the purpose we gave them.</li>
      </ul>

      <h2>2. Your login</h2>
      <ul>
        <li>Keep your password and the one-time codes we email you private. Do not share them.</li>
        <li>You are responsible for everything done with your login. If you think someone else has used it, change your password and tell us straight away.</li>
        <li>We may switch off a login, for example when someone leaves the business or if we suspect misuse.</li>
      </ul>

      <h2>3. Orders placed on the site</h2>
      <ul>
        <li>An order placed on the site is a request to buy. It becomes a firm order only when we accept it. We may decline or change an order, for example if an item is not available.</li>
        <li>You can cancel an order yourself while it is still new. After that, please contact us.</li>
        <li>Prices are not shown on the ordering pages. The price of an order is the price on the bill we send you, as agreed with you.</li>
        <li>Delivery, payment and other sales terms are the ones agreed with you and printed on the bill.</li>
      </ul>

      <h2>4. Bills and records</h2>
      <ul>
        <li>The site helps us prepare GST bills and keep records. The people who enter the information are responsible for its accuracy.</li>
        <li>Bills cannot be deleted once they are numbered. A wrong bill is cancelled and a new one is made, so that the numbering stays complete for GST.</li>
        <li>Please keep your own copies of important bills and records. We save backups, but we cannot promise that nothing will ever be lost.</li>
      </ul>

      <h2>5. What you must not do</h2>
      <ul>
        <li>Try to see, change or delete information that is not yours, or try to get past the site's security.</li>
        <li>Use scripts or tools to flood the site, to guess passwords or codes, or to copy its content in bulk.</li>
        <li>Put in anything that is false, unlawful, or that harms others.</li>
        <li>Copy, sell or reuse the site, its design, its logo or its software without our written permission.</li>
      </ul>
      <p>To protect everyone, the site limits how many requests can be made in a minute and may block anyone who misuses it.</p>

      <h2>6. Your information</h2>
      <p>How we collect, use and protect information is explained in our <a routerLink="/privacy">Privacy policy</a>. By using the site you accept it.</p>

      <h2>7. Our ownership</h2>
      <p>
        The site, its design, its logo and its software belong to {{ company }} or the people who made them for us. You may use the site only as
        these terms allow. The business information you put in, such as your clients and bills, remains yours.
      </p>

      <h2>8. Availability</h2>
      <p>
        We work to keep the site running, but we do not promise that it will always be available or free of mistakes. We may stop it for a short
        time for repairs and improvements, and we may change or remove features.
      </p>

      <h2>9. Our responsibility</h2>
      <p>
        We provide the site as it is. As far as the law allows, we are not responsible for loss caused by the site being unavailable, by wrong
        information entered by users, by misuse of a login that was not kept private, or by events outside our control. Nothing in these terms
        limits any right you have under law that cannot be limited.
      </p>

      <h2>10. Ending your use</h2>
      <p>
        You can stop using the site at any time. We can suspend or close an account that breaks these terms or that is no longer needed. Records
        we must keep by law, such as bills, will be kept for the period the law requires even after an account is closed.
      </p>

      <h2>11. Changes to these terms</h2>
      <p>We may change these terms. We will update the date at the top of the page, and for larger changes we will also tell you in the system. If you keep using the site after a change, you accept the new terms.</p>

      <h2>12. Law and courts</h2>
      <p>These terms are governed by the laws of India. Any dispute will be handled by the courts at {{ court }}.</p>

      <h2>13. Contact us</h2>
      <p>
        {{ company }}<br />
        {{ address }}<br />
        Phone: <a [href]="'tel:' + phone">{{ phone }}</a>
      </p>
    </app-legal-page>
  `,
})
export class TermsPage {
  /** Edit these when the business details change. */
  readonly company = 'Shahin Enterprises';
  readonly address = 'Bunglegudde, Karkala';
  readonly phone = '9845845109';
  readonly court = 'Karkala, Karnataka';
  readonly updated = '17 October 2026';
}
