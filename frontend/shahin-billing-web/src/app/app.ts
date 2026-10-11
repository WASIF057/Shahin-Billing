import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ThemeService } from './core/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastModule, ConfirmDialogModule],
  template: `
    <router-outlet />
    <p-toast position="top-right" [breakpoints]="{ '640px': { width: '100%', right: '0', left: '0' } }" />
    <p-confirmdialog [style]="{ width: '26rem', maxWidth: '94vw' }" />
  `,
})
export class App {
  /** Created at start-up so the saved colour theme is applied on every page, including login. */
  private theme = inject(ThemeService);
}
