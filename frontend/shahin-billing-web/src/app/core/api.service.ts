import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Business, Client, ClientSpecialRate, ActivityEntry, BillFormat, Dashboard, EmailTemplate, ImportResult, Order, OrderPriority, OrderStatus, PortalAccess, PortalCatalogItem, PortalOrder, PortalProfile, ProductType, ReminderSettings, TeamMember, Invoice, InvoiceListItem, InvoiceRequest, Item, PagedResult, TemplateSettings,
} from './models';

/** Builds HttpParams, skipping empty values. */
function params(obj: Record<string, unknown>): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(obj)) {
    if (v !== null && v !== undefined && v !== '') p = p.set(k, String(v));
  }
  return p;
}

/**
 * One small service per area of the API. `providedIn: 'root'` makes each a singleton
 * that any component can get with `inject(...)`.
 */
@Injectable({ providedIn: 'root' })
export class BusinessApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/business`;
  get(): Observable<Business> { return this.http.get<Business>(this.url); }
  update(b: Business): Observable<Business> { return this.http.put<Business>(this.url, b); }
}

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/catalog`;
  list(): Observable<ProductType[]> { return this.http.get<ProductType[]>(this.url); }
  create(t: ProductType): Observable<ProductType> { return this.http.post<ProductType>(this.url, t); }
  update(t: ProductType): Observable<ProductType> { return this.http.put<ProductType>(`${this.url}/${t.id}`, t); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class ItemsApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/items`;
  list(search?: string, active?: boolean): Observable<Item[]> { return this.http.get<Item[]>(this.url, { params: params({ search, active }) }); }
  get(id: string): Observable<Item> { return this.http.get<Item>(`${this.url}/${id}`); }
  create(i: Item): Observable<Item> { return this.http.post<Item>(this.url, i); }
  update(i: Item): Observable<Item> { return this.http.put<Item>(`${this.url}/${i.id}`, i); }
  setActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/${id}/active`, { active }); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/${id}`); }
  /** Excel of the items, one sheet per type. typeId: '' = all, 'none' = no type, or a type id. */
  export(typeId?: string): Observable<Blob> { return this.http.get(`${this.url}/export`, { params: params({ typeId }), responseType: 'blob' }); }
}

@Injectable({ providedIn: 'root' })
export class ClientsApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/clients`;
  list(search?: string, active?: boolean): Observable<Client[]> { return this.http.get<Client[]>(this.url, { params: params({ search, active }) }); }
  get(id: string): Observable<Client> { return this.http.get<Client>(`${this.url}/${id}`); }
  create(c: Client): Observable<Client> { return this.http.post<Client>(this.url, c); }
  update(c: Client): Observable<Client> { return this.http.put<Client>(`${this.url}/${c.id}`, c); }
  setActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/${id}/active`, { active }); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/${id}`); }
  // ordering login for a client (owner only)
  portal(id: string): Observable<PortalAccess> { return this.http.get<PortalAccess>(`${this.url}/${id}/portal`); }
  createPortal(id: string): Observable<PortalAccess> { return this.http.post<PortalAccess>(`${this.url}/${id}/portal`, {}); }
  invitePortal(id: string) { return this.http.post<void>(`${this.url}/${id}/portal/invite`, {}); }
  setPortalActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/${id}/portal/active`, { active }); }
  removePortal(id: string) { return this.http.delete<void>(`${this.url}/${id}/portal`); }
  specialRates(id: string): Observable<ClientSpecialRate[]> { return this.http.get<ClientSpecialRate[]>(`${this.url}/${id}/special-rates`); }
}

export interface InvoiceFilter {
  search?: string; clientId?: string; from?: string | null; to?: string | null;
  status?: string | null; paymentStatus?: string | null; page?: number; pageSize?: number;
}

@Injectable({ providedIn: 'root' })
export class InvoicesApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/invoices`;
  list(f: InvoiceFilter): Observable<PagedResult<InvoiceListItem>> {
    return this.http.get<PagedResult<InvoiceListItem>>(this.url, { params: params({ ...f }) });
  }
  get(id: string): Observable<Invoice> { return this.http.get<Invoice>(`${this.url}/${id}`); }
  create(r: InvoiceRequest): Observable<Invoice> { return this.http.post<Invoice>(this.url, r); }
  update(id: string, r: InvoiceRequest): Observable<Invoice> { return this.http.put<Invoice>(`${this.url}/${id}`, r); }
  finalize(id: string): Observable<Invoice> { return this.http.post<Invoice>(`${this.url}/${id}/finalize`, {}); }
  cancel(id: string, reason: string): Observable<Invoice> { return this.http.post<Invoice>(`${this.url}/${id}/cancel`, { reason }); }
  /** A receipt PDF for one payment on a bill. */
  receipt(id: string, paymentId: string): Observable<Blob> { return this.http.get(`${this.url}/${id}/payments/${paymentId}/receipt`, { responseType: 'blob' }); }
  /** Renders the unsaved bill as a PDF (no number yet) so it can be checked before saving. */
  previewPdf(r: InvoiceRequest, invoiceId?: string): Observable<Blob> {
    return this.http.post(`${this.url}/preview-pdf`, r, { params: params({ invoiceId }), responseType: 'blob' });
  }
  nextNumber(nonGst = false): Observable<{ number: string }> {
    return this.http.get<{ number: string }>(`${this.url}/next-number`, { params: params({ nonGst: nonGst || '' }) });
  }
  /** Emails a payment reminder for this bill now. */
  remind(id: string): Observable<{ message: string }> { return this.http.post<{ message: string }>(`${this.url}/${id}/remind`, {}); }
  duplicate(id: string): Observable<Invoice> { return this.http.post<Invoice>(`${this.url}/${id}/duplicate`, {}); }
  addPayment(id: string, p: { date: string; amount: number; mode: string; reference: string; note: string }): Observable<Invoice> {
    return this.http.post<Invoice>(`${this.url}/${id}/payments`, p);
  }
  removePayment(id: string, paymentId: string): Observable<Invoice> {
    return this.http.delete<Invoice>(`${this.url}/${id}/payments/${paymentId}`);
  }
  /** notify=true is for the Download buttons: the server then emails the bill (if Email is switched on). Previews leave it off. */
  pdf(id: string, copies?: string[], notify = false): Observable<Blob> {
    return this.http.get(`${this.url}/${id}/pdf`, { params: params({ copies: copies?.join(','), notify: notify || '' }), responseType: 'blob' });
  }
}

@Injectable({ providedIn: 'root' })
export class EmailApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/email`;
  status(): Observable<{ smtpConfigured: boolean }> { return this.http.get<{ smtpConfigured: boolean }>(this.url); }
  reminders(): Observable<ReminderSettings> { return this.http.get<ReminderSettings>(`${this.url}/reminders`); }
  saveReminders(s: ReminderSettings): Observable<ReminderSettings> { return this.http.put<ReminderSettings>(`${this.url}/reminders`, s); }
  list(): Observable<EmailTemplate[]> { return this.http.get<EmailTemplate[]>(`${this.url}/templates`); }
  create(t: EmailTemplate): Observable<EmailTemplate> { return this.http.post<EmailTemplate>(`${this.url}/templates`, t); }
  update(t: EmailTemplate): Observable<EmailTemplate> { return this.http.put<EmailTemplate>(`${this.url}/templates/${t.id}`, t); }
  setActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/templates/${id}/active`, { active }); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/templates/${id}`); }
  test(id: string): Observable<{ message: string }> { return this.http.post<{ message: string }>(`${this.url}/templates/${id}/test`, {}); }
}

@Injectable({ providedIn: 'root' })
export class BillFormatsApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/template/formats`;
  list(): Observable<BillFormat[]> { return this.http.get<BillFormat[]>(this.url); }
  get(id: string): Observable<BillFormat> { return this.http.get<BillFormat>(`${this.url}/${id}`); }
  create(f: BillFormat): Observable<BillFormat> { return this.http.post<BillFormat>(this.url, f); }
  update(f: BillFormat): Observable<BillFormat> { return this.http.put<BillFormat>(`${this.url}/${f.id}`, f); }
  setActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/${id}/active`, { active }); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class TeamApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/team`;
  list(): Observable<TeamMember[]> { return this.http.get<TeamMember[]>(this.url); }
  create(f: { name: string; email: string; password: string }): Observable<TeamMember> { return this.http.post<TeamMember>(this.url, f); }
  setActive(id: string, active: boolean) { return this.http.patch<void>(`${this.url}/${id}/active`, { active }); }
  resetPassword(id: string, newPassword: string) { return this.http.post<void>(`${this.url}/${id}/reset-password`, { newPassword }); }
  remove(id: string) { return this.http.delete<void>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class ActivityApi {
  private http = inject(HttpClient);
  list(f: { from?: string | null; to?: string | null; userId?: string | null; type?: string | null; page?: number; pageSize?: number }): Observable<PagedResult<ActivityEntry>> {
    return this.http.get<PagedResult<ActivityEntry>>(`${environment.apiUrl}/activity`, { params: params({ ...f }) });
  }
}

@Injectable({ providedIn: 'root' })
export class ImportApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/import`;
  template(kind: 'clients' | 'items'): Observable<Blob> { return this.http.get(`${this.url}/${kind}/template`, { responseType: 'blob' }); }
  /** dryRun = true only checks the file; false saves the good rows. */
  run(kind: 'clients' | 'items', file: File, dryRun: boolean): Observable<ImportResult> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ImportResult>(`${this.url}/${kind}`, form, { params: { dryRun } });
  }
}

/** The ordering website for a client's login. Nothing here carries a price. */
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/portal`;
  profile(): Observable<PortalProfile> { return this.http.get<PortalProfile>(`${this.url}/profile`); }
  catalog(): Observable<PortalCatalogItem[]> { return this.http.get<PortalCatalogItem[]>(`${this.url}/catalog`); }
  orders(): Observable<PortalOrder[]> { return this.http.get<PortalOrder[]>(`${this.url}/orders`); }
  place(r: { lines: { itemId: string; quantity: number }[]; city: string; note: string; priority: OrderPriority }): Observable<PortalOrder> { return this.http.post<PortalOrder>(`${this.url}/orders`, r); }
  cancel(id: string): Observable<PortalOrder> { return this.http.post<PortalOrder>(`${this.url}/orders/${id}/cancel`, {}); }
}

/** The orders bucket for the owner and staff. */
@Injectable({ providedIn: 'root' })
export class OrdersApi {
  private http = inject(HttpClient);
  private url = `${environment.apiUrl}/orders`;
  list(f: { status?: OrderStatus | null; clientId?: string | null; from?: string | null; to?: string | null; page?: number; pageSize?: number }): Observable<PagedResult<Order>> {
    return this.http.get<PagedResult<Order>>(this.url, { params: params({ ...f }) });
  }
  get(id: string): Observable<Order> { return this.http.get<Order>(`${this.url}/${id}`); }
  /** An order taken over the phone for a client. */
  take(r: { clientId: string; lines: { itemId: string; quantity: number }[]; city: string; note: string; priority: OrderPriority; emailClient: boolean; emailMe: boolean }): Observable<Order> {
    return this.http.post<Order>(this.url, r);
  }
  setStatus(id: string, status: 'Accepted' | 'Cancelled', reason = ''): Observable<Order> { return this.http.patch<Order>(`${this.url}/${id}/status`, { status, reason }); }
}

@Injectable({ providedIn: 'root' })
export class MiscApi {
  private http = inject(HttpClient);
  private base = environment.apiUrl;
  dashboard(f: { clientId?: string | null; city?: string | null; from?: string | null; to?: string | null } = {}): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.base}/dashboard`, { params: params({ ...f }) });
  }
  template(): Observable<TemplateSettings> { return this.http.get<TemplateSettings>(`${this.base}/template`); }
  previewPdf(t: TemplateSettings): Observable<Blob> { return this.http.post(`${this.base}/template/preview-pdf`, t, { responseType: 'blob' }); }
  gstr1(month: string): Observable<Blob> { return this.http.get(`${this.base}/reports/gstr1`, { params: params({ month }), responseType: 'blob' }); }
  clientReport(clientId: string, from: string, to: string, city?: string): Observable<Blob> {
    return this.http.get(`${this.base}/reports/client`, { params: params({ clientId, from, to, city }), responseType: 'blob' });
  }
  clientStatement(clientId: string, from: string, to: string, city?: string): Observable<Blob> {
    return this.http.get(`${this.base}/reports/client-statement`, { params: params({ clientId, from, to, city }), responseType: 'blob' });
  }
  sales(from: string, to: string): Observable<Blob> { return this.http.get(`${this.base}/reports/sales`, { params: params({ from, to }), responseType: 'blob' }); }
  backupStatus(): Observable<{ enabled: boolean; lastBackupAt: string | null; keep: number }> {
    return this.http.get<{ enabled: boolean; lastBackupAt: string | null; keep: number }>(`${this.base}/backup/status`);
  }
  backup(): Observable<Blob> { return this.http.get(`${this.base}/backup/export`, { responseType: 'blob' }); }
  seed(): Observable<{ message: string }> { return this.http.post<{ message: string }>(`${this.base}/dev/seed`, {}); }
}

/** Saves a Blob as a file download. */
export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 2000);
}

/** Opens a PDF Blob in a new browser tab (handy for printing). */
export function openBlob(blob: Blob) {
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
