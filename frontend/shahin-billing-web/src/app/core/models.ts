// Shapes returned by the .NET API (camelCase JSON).

export interface Address { line1: string; line2: string; city: string; state: string; stateCode: string; pincode: string; }
export const emptyAddress = (): Address => ({ line1: '', line2: '', city: '', state: '', stateCode: '', pincode: '' });

export interface BankDetails { accountName: string; accountNumber: string; ifsc: string; bankName: string; branch: string; upiId: string; }
export interface InvoiceNumbering { prefix: string; nonGstPrefix: string; separator: string; padding: number; }

export interface TemplateSettings {
  layout: 'Classic' | 'Modern';
  primaryColor: string;
  fontSize: 'Small' | 'Normal' | 'Large';
  invoiceTitle: string;
  nonGstTitle: string;
  footerNote: string;
  showLogo: boolean; showDiscountColumn: boolean; showHsnColumn: boolean; showGstSummary: boolean;
  showBankDetails: boolean; showUpi: boolean; showTerms: boolean; showDeclaration: boolean;
  showSignature: boolean; showTransportDetails: boolean; showPoDetails: boolean;
  defaultCopies: string[];
}

export interface Business {
  id: string; name: string; logo: string; address: Address;
  phone: string; alternatePhone: string; email: string; website: string;
  gstin: string; pan: string; bank: BankDetails; signature: string; authorisedSignatoryName: string;
  termsAndConditions: string; declarationText: string;
  invoiceNumbering: InvoiceNumbering; template: TemplateSettings;
  requireLoginOtp: boolean;
}

export interface LoginResponse { otpRequired: boolean; challengeId: string | null; maskedEmail: string | null; auth: AuthResponse | null; }

export interface SpecialRate { id?: string; rate: number; clientIds: string[]; }
export interface ProductType {
  id: string; name: string; usesVariants: boolean; variants: string[]; clothTypes: string[]; clothColours: { cloth: string; colours: string[] }[]; sizes: string[];
}

export interface Item {
  id: string; typeId: string; typeName: string; variant: string; cloth: string; colour: string; size: string;
  name: string; sizeOrVariant: string; description: string; hsnCode: string; unit: string;
  gstRate: number; defaultRate: number; specialRates: SpecialRate[]; isActive: boolean;
}

export interface Client {
  id: string; name: string; gstin: string; pan: string; contactPerson: string; phone: string; email: string;
  billingAddress: Address; shippingSameAsBilling: boolean; shippingAddress: Address; cities: string[]; notes: string; billedWithoutGst: boolean; isActive: boolean;
}

export interface PartySnapshot { name: string; gstin: string; contactPerson: string; phone: string; address: Address; }
export interface TransportDetails {
  transporterName: string; vehicleNumber: string; ewayBillNumber: string; lrNumber: string; deliveryDate: string | null;
}

export type InvoiceStatus = 'Draft' | 'Final' | 'Cancelled';
export type PaymentStatus = 'Unpaid' | 'PartlyPaid' | 'Paid';

export interface InvoiceLine {
  itemId: string; name: string; sizeOrVariant: string; hsnCode: string; unit: string;
  quantity: number; rate: number; isSpecialRate: boolean; amount: number; discount: number;
  taxableValue: number; gstRate: number; cgst: number; sgst: number; igst: number; lineTotal: number;
}
export interface InvoiceTotals {
  totalQuantity: number; taxableTotal: number; discountTotal: number;
  cgstTotal: number; sgstTotal: number; igstTotal: number; roundOff: number; grandTotal: number;
}
export interface Payment { id: string; date: string; amount: number; mode: string; reference: string; note: string; }
export interface GstSummaryRow { gstRate: number; taxableValue: number; cgst: number; sgst: number; igst: number; }

export interface Invoice {
  id: string; invoiceNumber: string; invoiceDate: string; createdAt: string; financialYear: string;
  status: InvoiceStatus; cancelReason: string; cancelledAt: string | null;
  clientId: string; city: string; billTo: PartySnapshot; shipToSameAsBillTo: boolean; shipTo: PartySnapshot;
  isNonGst: boolean;
  placeOfSupplyState: string; placeOfSupplyStateCode: string; isInterState: boolean;
  poNumber: string; poDate: string | null; transport: TransportDetails;
  lines: InvoiceLine[]; totals: InvoiceTotals; gstSummary: GstSummaryRow[]; amountInWords: string; notes: string;
  payments: Payment[]; paymentStatus: PaymentStatus; amountPaid: number; balanceDue: number;
  businessSnapshot: { name: string; gstin: string; address: Address };
}

export interface InvoiceRequest {
  invoiceDate: string; clientId: string; city: string; nonGst: boolean; orderId?: string | null;
  shipToSameAsBillTo: boolean; shipTo: PartySnapshot | null; placeOfSupplyStateCode: string;
  poNumber: string; poDate: string | null; transport: TransportDetails; notes: string;
  lines: { itemId: string; quantity: number; rate: number | null; discount: number }[];
  finalize: boolean;
}

export interface InvoiceListItem {
  id: string; invoiceNumber: string; invoiceDate: string; clientId: string; clientName: string;
  grandTotal: number; amountPaid: number; balanceDue: number; status: InvoiceStatus; paymentStatus: PaymentStatus; isNonGst: boolean;
}
export interface PagedResult<T> { items: T[]; total: number; page: number; pageSize: number; }

export interface ClientSpecialRate { itemId: string; itemName: string; sizeOrVariant: string; defaultRate: number; specialRate: number; }

export interface Dashboard {
  thisMonthSales: number; lastMonthSales: number; unpaidAmount: number; unpaidBills: number; financialYear: string;
  monthlySales: { month: string; sales: number }[];
  topClients: { clientId: string; name: string; sales: number; bills: number }[];
  recentBills: InvoiceListItem[];
  periodLabel: string; previousLabel: string; customRange: boolean; topGroupBy: 'client' | 'city'; periodBills: number;
  payments: { paid: PaymentBucket; partPaid: PaymentBucket; unpaid: PaymentBucket };
}
export interface PaymentBucket { bills: number; total: number; received: number; balance: number; }

export interface UserInfo { id: string; name: string; email: string; role: string; businessId: string; businessName: string; }
export interface AuthResponse { token: string; expiresAt: string; user: UserInfo; }

export interface TeamMember { id: string; name: string; email: string; role: string; isActive: boolean; createdAt: string; }
export interface ActivityEntry { id: string; userId: string; userName: string; action: string; entityType: string; entityId: string | null; summary: string; at: string; }

export interface ImportRow { row: number; status: 'Ok' | 'Skipped' | 'Error'; message: string; summary: string; }
export interface ImportResult { total: number; ok: number; skipped: number; errors: number; imported: boolean; rows: ImportRow[]; }

export type OrderStatus = 'New' | 'Accepted' | 'Billed' | 'Cancelled';
/** What a client sees of a product: no price, no GST. */
export interface PortalCatalogItem { itemId: string; type: string; name: string; variant: string; cloth: string; colour: string; size: string; unit: string; label: string; }
export interface PortalProfile { clientName: string; businessName: string; cities: string[]; }
export interface PortalOrderLine { label: string; quantity: number; unit: string; }
export type OrderPriority = 'Normal' | 'High' | 'Urgent';
export interface PortalOrder { id: string; orderNumber: string; placedAt: string; status: OrderStatus; city: string; note: string; cancelReason: string; lines: PortalOrderLine[]; priority: OrderPriority; }
export interface OrderLine { itemId: string; label: string; name: string; variant: string; cloth: string; colour: string; size: string; unit: string; quantity: number; }
export interface Order {
  id: string; orderNumber: string; clientId: string; clientName: string; city: string; userName: string; userEmail: string;
  status: OrderStatus; note: string; lines: OrderLine[]; cancelReason: string; handledBy: string; handledAt: string | null;
  invoiceId: string | null; invoiceNumber: string | null; createdAt: string;
  priority: OrderPriority; source: 'Website' | 'Phone'; takenBy: string;
}
export interface PortalAccess { exists: boolean; email: string | null; isActive: boolean; createdAt: string | null; warning: string | null; }

export interface ReminderSettings { enabled: boolean; afterDays: number; repeatEveryDays: number; maxReminders: number; }

export interface BillFormat { id: string; name: string; settings: TemplateSettings; isActive: boolean; }

export interface EmailTemplate {
  id: string; name: string; recipient: 'Client' | 'Business'; triggers: string[]; attachPdf: boolean;
  subject: string; body: string; isActive: boolean;
}
