import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import { Confirmation, ConfirmationService, MessageService } from 'primeng/api';
import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth.interceptor';

/** PrimeNG theme: Aura with a deep "ticking-stripe" navy as the primary colour. */
const ShahinPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eef3f8', 100: '#d6e1ec', 200: '#afc4d9', 300: '#84a3c2', 400: '#5a81a8',
      500: '#2a5b8f', 600: '#23497a', 700: '#1d3b63', 800: '#162d4c', 900: '#0f203a', 950: '#09152a',
    },
  },
});

/**
 * Every "Are you sure?" pop-up gets a round icon in a matching tone (red for delete and cancel, blue for the rest),
 * even where the code that asks did not choose an icon.
 */
class FriendlyConfirmation extends ConfirmationService {
  override confirm(c: Confirmation) {
    const danger = (c.acceptButtonStyleClass ?? '').includes('danger');
    const icon = c.icon || (danger ? 'pi pi-exclamation-triangle' : 'pi pi-question-circle');
    return super.confirm({ ...c, icon: `${icon} ${danger ? 'pop-danger' : 'pop-info'}` });
  }
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // withComponentInputBinding: route params like :id arrive as component inputs
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    providePrimeNG({
      theme: { preset: ShahinPreset, options: { darkModeSelector: '.app-dark', cssLayer: false } },
      ripple: false,
    }),
    MessageService,
    { provide: ConfirmationService, useClass: FriendlyConfirmation },
  ],
};
