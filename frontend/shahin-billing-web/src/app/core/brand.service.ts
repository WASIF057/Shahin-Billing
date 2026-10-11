import { inject, Injectable, signal } from '@angular/core';
import { BusinessApi } from './api.service';
import { ThemeService } from './theme.service';

/** The business logo shown in the sidebar. Loaded once after login; My business updates it when the logo is saved. */
@Injectable({ providedIn: 'root' })
export class BrandService {
  private api = inject(BusinessApi);
  private theme = inject(ThemeService);
  /** The logo as a data URL, or '' to show the default striped mark. */
  readonly logo = signal('');
  /** True once the business details have answered, so the sidebar does not show the built-in logo first and then swap it. */
  readonly loaded = signal(false);
  /** 'Themed' = show the built-in logo in the theme colours, even when an image is uploaded (bills still use the image). */
  readonly style = signal('');

  load() { this.api.get().subscribe({ next: b => { this.logo.set(b.logo ?? ''); this.style.set(b.logoStyle ?? ''); this.theme.applyShared(b.theme?.primary, b.theme?.accent); this.loaded.set(true); }, error: () => this.loaded.set(true) }); }
  set(logo: string | null | undefined, style?: string) { this.logo.set(logo ?? ''); this.style.set(style ?? ''); }
}
