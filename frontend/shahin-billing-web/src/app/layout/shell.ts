import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../core/auth.service';
import { OrderAlerts } from '../core/order-alerts.service';
import { OrderBadge } from '../core/order-badge.service';
import { OrderAlertsComponent } from './order-alerts';

interface NavLink { label: string; icon: string; path: string; exact?: boolean; ownerOnly?: boolean; badge?: boolean; }

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, OrderAlertsComponent],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit, OnDestroy {
  auth = inject(AuthService);
  badge = inject(OrderBadge);
  menuOpen = signal(false);
  alerts = inject(OrderAlerts);

  /** Owner and staff: watch for new orders (popup, ring, badge) for as long as the app is open. */
  ngOnInit() { if (!this.auth.isClient()) this.alerts.start(); }
  ngOnDestroy() { this.alerts.stop(); }

  private clientLinks: NavLink[] = [
    { label: 'Order products', icon: 'pi pi-shopping-cart', path: '/portal', exact: true },
    { label: 'My orders', icon: 'pi pi-list', path: '/portal/orders' },
  ];

  private allLinks: NavLink[] = [
    { label: 'Dashboard', icon: 'pi pi-home', path: '/', exact: true, ownerOnly: true },
    { label: 'All bills', icon: 'pi pi-file', path: '/bills', exact: true },
    { label: 'Orders', icon: 'pi pi-inbox', path: '/orders', badge: true },
    { label: 'Item setup', icon: 'pi pi-sitemap', path: '/catalog', ownerOnly: true },
    { label: 'Items', icon: 'pi pi-box', path: '/items', ownerOnly: true },
    { label: 'Clients', icon: 'pi pi-users', path: '/clients' },
    { label: 'Import', icon: 'pi pi-upload', path: '/import', ownerOnly: true },
    { label: 'Reports', icon: 'pi pi-chart-bar', path: '/reports', ownerOnly: true },
    { label: 'Bill formats', icon: 'pi pi-palette', path: '/bill-format', ownerOnly: true },
    { label: 'Email templates', icon: 'pi pi-envelope', path: '/email', ownerOnly: true },
    { label: 'Team', icon: 'pi pi-id-card', path: '/team', ownerOnly: true },
    { label: 'Activity', icon: 'pi pi-history', path: '/activity', ownerOnly: true },
    { label: 'My business', icon: 'pi pi-building', path: '/settings', ownerOnly: true },
  ];

  /** Staff only see the screens they can use. */
  links = computed(() => this.auth.isClient() ? this.clientLinks : this.allLinks.filter(l => !l.ownerOnly || this.auth.isOwner()));

}
