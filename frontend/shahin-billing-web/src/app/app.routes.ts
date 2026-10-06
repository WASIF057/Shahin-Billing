import { Routes } from '@angular/router';
import { authGuard, clientGuard, guestGuard, notClientGuard, ownerGuard } from './core/auth.guard';
import type { BillEditor } from './features/billing/bill-editor';

/** Asks before leaving a bill with unsaved changes. */
const leaveBill = (c: BillEditor) => c.canLeave();

// loadComponent = lazy loading: each screen's code downloads only when first opened.
export const routes: Routes = [
  { path: 'login', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login').then(m => m.Login) },
  { path: 'forgot-password', canActivate: [guestGuard], loadComponent: () => import('./features/auth/forgot-password').then(m => m.ForgotPassword) },
  { path: 'register', canActivate: [guestGuard], loadComponent: () => import('./features/auth/register').then(m => m.Register) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then(m => m.Shell),
    children: [
      { path: '', canActivate: [ownerGuard], title: 'Dashboard', loadComponent: () => import('./features/dashboard/dashboard').then(m => m.DashboardPage) },
      { path: 'portal', canActivate: [clientGuard], title: 'Order products', loadComponent: () => import('./features/portal/order-page').then(m => m.PortalOrderPage) },
      { path: 'portal/orders', canActivate: [clientGuard], title: 'My orders', loadComponent: () => import('./features/portal/my-orders').then(m => m.MyOrdersPage) },
      { path: 'orders', canActivate: [notClientGuard], title: 'Orders', loadComponent: () => import('./features/orders/orders').then(m => m.OrdersPage) },
      { path: 'bills', canActivate: [notClientGuard], title: 'All bills', loadComponent: () => import('./features/bills/bills-list').then(m => m.BillsList) },
      { path: 'bills/new', canActivate: [notClientGuard], title: 'New bill', canDeactivate: [leaveBill], loadComponent: () => import('./features/billing/bill-editor').then(m => m.BillEditor) },
      { path: 'bills/:id/edit', canActivate: [notClientGuard], title: 'Edit bill', canDeactivate: [leaveBill], loadComponent: () => import('./features/billing/bill-editor').then(m => m.BillEditor) },
      { path: 'bills/:id', canActivate: [notClientGuard], title: 'Bill', loadComponent: () => import('./features/bills/bill-view').then(m => m.BillView) },
      { path: 'catalog', canActivate: [ownerGuard], title: 'Item setup', loadComponent: () => import('./features/catalog/catalog').then(m => m.CatalogPage) },
      { path: 'items', canActivate: [ownerGuard], title: 'Items', loadComponent: () => import('./features/items/items').then(m => m.ItemsPage) },
      { path: 'clients', canActivate: [notClientGuard], title: 'Clients', loadComponent: () => import('./features/clients/clients').then(m => m.ClientsPage) },
      { path: 'reports', canActivate: [ownerGuard], title: 'Reports', loadComponent: () => import('./features/reports/reports').then(m => m.ReportsPage) },
      { path: 'bill-format', canActivate: [ownerGuard], title: 'Bill formats', loadComponent: () => import('./features/bill-format/bill-format-list').then(m => m.BillFormatList) },
      { path: 'bill-format/new', canActivate: [ownerGuard], title: 'New bill format', loadComponent: () => import('./features/bill-format/bill-format').then(m => m.BillFormat) },
      { path: 'bill-format/:id', canActivate: [ownerGuard], title: 'Edit bill format', loadComponent: () => import('./features/bill-format/bill-format').then(m => m.BillFormat) },
      { path: 'email', canActivate: [ownerGuard], title: 'Email', loadComponent: () => import('./features/email/email-settings').then(m => m.EmailSettingsPage) },
      { path: 'import', canActivate: [ownerGuard], title: 'Import', loadComponent: () => import('./features/import/import').then(m => m.ImportPage) },
      { path: 'team', canActivate: [ownerGuard], title: 'Team', loadComponent: () => import('./features/team/team').then(m => m.TeamPage) },
      { path: 'activity', canActivate: [ownerGuard], title: 'Activity', loadComponent: () => import('./features/activity/activity').then(m => m.ActivityPage) },
      { path: 'account', title: 'My account', loadComponent: () => import('./features/account/account').then(m => m.AccountPage) },
      { path: 'settings', canActivate: [ownerGuard], title: 'My business', loadComponent: () => import('./features/settings/business-settings').then(m => m.BusinessSettings) },
    ],
  },
  { path: '**', redirectTo: '' },
];
