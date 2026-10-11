import { inject, Injectable } from '@angular/core';
import { ConfirmationService } from 'primeng/api';

/** The "Are you sure?" pop-ups used by Save, Cancel and similar buttons, so they all look and read the same. */
@Injectable({ providedIn: 'root' })
export class Confirmer {
  private confirm = inject(ConfirmationService);

  /** Before saving something. `what` is a short name such as "this item". */
  save(what: string, run: () => void) {
    this.confirm.confirm({
      header: `Save ${what}?`, message: 'Please check the details before you continue. You can edit them again later.',
      icon: 'pi pi-save', acceptLabel: 'Yes, save', rejectLabel: 'Review again', accept: run,
    });
  }

  /** Before closing a form without saving. */
  discard(run: () => void) {
    this.confirm.confirm({
      header: 'Discard changes?', message: 'Your changes will be lost if you close this form. This cannot be undone.',
      icon: 'pi pi-exclamation-triangle', acceptLabel: 'Yes, discard', rejectLabel: 'Continue editing', acceptButtonStyleClass: 'p-button-danger', accept: run,
    });
  }

  /** Before any other one-click action (finalize, accept, hide, ...). */
  ask(header: string, message: string, acceptLabel: string, run: () => void, danger = false) {
    this.confirm.confirm({
      header, message, icon: danger ? 'pi pi-exclamation-triangle' : 'pi pi-question-circle',
      acceptLabel, rejectLabel: 'Go back', acceptButtonStyleClass: danger ? 'p-button-danger' : undefined, accept: run,
    });
  }
}
