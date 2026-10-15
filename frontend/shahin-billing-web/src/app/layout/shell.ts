import { Component, computed, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../core/auth.service';
import { OrderAlerts } from '../core/order-alerts.service';
import { BrandService } from '../core/brand.service';
import { ThemeService } from '../core/theme.service';
import { OrderBadge } from '../core/order-badge.service';
import { AppLogo } from '../shared/app-logo';
import { OrderAlertsComponent } from './order-alerts';

interface NavLink { label: string; icon: string; path: string; exact?: boolean; ownerOnly?: boolean; badge?: boolean; group?: string; }
interface NavGroup { label: string; links: NavLink[]; }

function readCollapsed() { try { return localStorage.getItem('shahin.sidebar') === '1'; } catch { return false; } }

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ButtonModule, OrderAlertsComponent, AppLogo],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell implements OnInit, OnDestroy {
  auth = inject(AuthService);
  badge = inject(OrderBadge);
  menuOpen = signal(false);
  private router = inject(Router);

  /** The dashboard has its own big "Good morning" heading, so the top bar keeps its greeting tucked away until that heading scrolls out of view. */
  onDashboard = signal(this.isDashboard(this.router.url));
  scrolled = signal(false);
  /** True while the next page is loading (shown as a thin bar at the top). It waits 150 ms first, so quick changes do not flicker. */
  navigating = signal(false);
  private navTimer: ReturnType<typeof setTimeout> | null = null;
  @HostListener('window:scroll') onScroll() { this.scrolled.set(window.scrollY > 70); }
  private isDashboard(url: string) { return url.split('?')[0] === '/'; }
  theme = inject(ThemeService);
  brand = inject(BrandService);
  themeOpen = signal(false);

  /** Reads the colour out of a colour-picker event. */
  colourOf(e: Event) { return (e.target as HTMLInputElement).value; }
  alerts = inject(OrderAlerts);

  /** Owner and staff: watch for new orders (popup, ring, badge) for as long as the app is open. */
  ngOnInit() {
    this.router.events.subscribe(e => {
      if (e instanceof NavigationStart) { this.navTimer = setTimeout(() => this.navigating.set(true), 150); }
      else if (e instanceof NavigationEnd || e instanceof NavigationCancel || e instanceof NavigationError) {
        if (this.navTimer) { clearTimeout(this.navTimer); this.navTimer = null; }
        this.navigating.set(false);
      }
    });
    this.router.events.subscribe(e => { if (e instanceof NavigationEnd) { this.onDashboard.set(this.isDashboard(e.urlAfterRedirects)); this.onScroll(); } });
    if (!this.auth.viewIsClient()) { this.alerts.start(); this.brand.load(); } }
  ngOnDestroy() { this.alerts.stop(); }

  private clientLinks: NavLink[] = [
    { label: 'Order products', icon: 'pi pi-shopping-cart', path: '/portal', exact: true },
    { label: 'My orders', icon: 'pi pi-list', path: '/portal/orders' },
  ];

  private allLinks: NavLink[] = [
    { label: 'Dashboard', icon: 'pi pi-home', path: '/', exact: true, ownerOnly: true, group: 'Sales' },
    { label: 'All bills', icon: 'pi pi-file', path: '/bills', exact: true, group: 'Sales' },
    { label: 'Orders', icon: 'pi pi-inbox', path: '/orders', badge: true, group: 'Sales' },
    { label: 'Clients', icon: 'pi pi-users', path: '/clients', group: 'Sales' },
    { label: 'Item setup', icon: 'pi pi-sitemap', path: '/catalog', ownerOnly: true, group: 'Products' },
    { label: 'Items', icon: 'pi pi-box', path: '/items', ownerOnly: true, group: 'Products' },
    { label: 'Import', icon: 'pi pi-upload', path: '/import', ownerOnly: true, group: 'Products' },
    { label: 'Reports', icon: 'pi pi-chart-bar', path: '/reports', ownerOnly: true, group: 'Insights' },
    { label: 'Activity', icon: 'pi pi-history', path: '/activity', ownerOnly: true, group: 'Insights' },
    { label: 'Bill formats', icon: 'pi pi-palette', path: '/bill-format', ownerOnly: true, group: 'Settings' },
    { label: 'Email templates', icon: 'pi pi-envelope', path: '/email', ownerOnly: true, group: 'Settings' },
    { label: 'Team', icon: 'pi pi-id-card', path: '/team', ownerOnly: true, group: 'Settings' },
    { label: 'My business', icon: 'pi pi-building', path: '/settings', ownerOnly: true, group: 'Settings' },
  ];

  /** Staff only see the screens they can use. */
  links = computed(() => this.auth.viewIsClient() ? this.clientLinks : this.allLinks.filter(l => !l.ownerOnly || this.auth.viewIsOwner()));

  /** The menu split under small headings (Sales, Products...). A heading with nothing the person may open is left out. */
  groups = computed<NavGroup[]>(() => {
    const out: NavGroup[] = [];
    for (const l of this.links()) {
      const name = l.group ?? '';
      const g = out.find(x => x.label === name);
      if (g) g.links.push(l); else out.push({ label: name, links: [l] });
    }
    return out;
  });

  /** Sidebar shrunk to icons only (desktop). Remembered in this browser. */
  collapsed = signal(readCollapsed());
  toggleCollapsed() {
    this.collapsed.update(v => !v);
    try { localStorage.setItem('shahin.sidebar', this.collapsed() ? '1' : '0'); } catch { /* not remembered */ }
  }

  greeting() {
    const h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
  }
  today() { return new Date().toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }); }
  firstName() { return (this.auth.viewUser()?.name ?? '').trim().split(/\s+/)[0] ?? ''; }
  initials() {
    const n = (this.auth.viewUser()?.name ?? '').trim();
    return n ? n.split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase() : '?';
  }

}
