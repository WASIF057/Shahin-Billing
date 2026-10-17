import { inject, Injectable } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** Puts the site name after each page's title in the browser tab: "All bills | Shahin Billing". Pages without a title show just the site name. */
@Injectable({ providedIn: 'root' })
export class PageTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot) {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `${page} | Shahin Billing` : 'Shahin Billing');
  }
}
