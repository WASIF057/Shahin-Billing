import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ChartModule } from 'primeng/chart';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ClientsApi, MiscApi } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { formatIndian, toIsoDate } from '../../core/gst';
import { Client, Dashboard } from '../../core/models';
import { Notify } from '../../core/notify.service';
import { InrPipe } from '../../shared/inr.pipe';
import { StatusTag } from '../../shared/status-tag';

@Component({
  selector: 'app-dashboard',
  imports: [DatePipe, FormsModule, RouterLink, ButtonModule, ChartModule, DatePickerModule, SelectModule, InrPipe, StatusTag],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardPage implements OnInit {
  private api = inject(MiscApi);
  private clientsApi = inject(ClientsApi);
  private notify = inject(Notify);
  auth = inject(AuthService);

  data = signal<Dashboard | null>(null);
  loading = signal(false);

  // filters (all optional)
  clients = signal<Client[]>([]);
  clientId: string | null = null;
  city = '';
  from: Date | null = null;
  to: Date | null = null;

  get filtered() { return !!(this.clientId || this.from || this.to); }

  /** City choices appear only for a client that has more than one city. */
  cityOptions() {
    const c = this.clients().find(x => x.id === this.clientId);
    return c && (c.cities?.length ?? 0) > 1 ? [{ label: 'All cities', value: '' }, ...c.cities.map(x => ({ label: x, value: x }))] : [];
  }

  subtitle() {
    const d = this.data();
    const c = this.clients().find(x => x.id === this.clientId);
    const parts: string[] = [];
    if (c) parts.push(c.name + (this.city ? ` (${this.city})` : ''));
    if (d?.customRange) parts.push(d.periodLabel);
    return parts.length ? `Showing ${parts.join(' · ')}.` : `Here’s how billing looks${d ? ' for FY ' + d.financialYear : ''}.`;
  }

  change = computed(() => {
    const d = this.data();
    if (!d || !d.lastMonthSales) return null;
    return Math.round(((d.thisMonthSales - d.lastMonthSales) / d.lastMonthSales) * 100);
  });

  chartData = computed(() => {
    const d = this.data();
    if (!d) return null;
    return {
      labels: d.monthlySales.map(m => m.month),
      datasets: [{ data: d.monthlySales.map(m => m.sales), backgroundColor: '#23466b', borderRadius: 6, maxBarThickness: 34 }],
    };
  });

  chartOptions = {
    maintainAspectRatio: false,
    plugins: {
      legend: { display: false },
      tooltip: { callbacks: { label: (c: { parsed: { y: number } }) => '₹' + formatIndian(c.parsed.y, 0) } },
    },
    scales: {
      y: { beginAtZero: true, grid: { color: '#edf0f4' }, ticks: { callback: (v: number) => '₹' + formatIndian(v, 0) } },
      x: { grid: { display: false } },
    },
  };

  greeting = (() => {
    const h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
  })();

  ngOnInit() {
    this.clientsApi.list().subscribe(c => this.clients.set(c));
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.dashboard({
      clientId: this.clientId, city: this.city || null, from: toIsoDate(this.from), to: toIsoDate(this.to),
    }).subscribe({
      next: d => { this.data.set(d); this.loading.set(false); },
      error: e => { this.notify.error(e); this.loading.set(false); },
    });
  }

  clientChanged() { this.city = ''; this.load(); }

  clear() { this.clientId = null; this.city = ''; this.from = null; this.to = null; this.load(); }
}
