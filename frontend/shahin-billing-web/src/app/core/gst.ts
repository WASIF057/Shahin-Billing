/**
 * Business rules mirrored from the backend (see CLAUDE.md section 4) so the bill
 * updates instantly while typing. The server always recalculates on save and wins.
 */

export interface StateOption { code: string; name: string; }

export const STATES: StateOption[] = [
  ['01', 'Jammu and Kashmir'], ['02', 'Himachal Pradesh'], ['03', 'Punjab'], ['04', 'Chandigarh'],
  ['05', 'Uttarakhand'], ['06', 'Haryana'], ['07', 'Delhi'], ['08', 'Rajasthan'], ['09', 'Uttar Pradesh'],
  ['10', 'Bihar'], ['11', 'Sikkim'], ['12', 'Arunachal Pradesh'], ['13', 'Nagaland'], ['14', 'Manipur'],
  ['15', 'Mizoram'], ['16', 'Tripura'], ['17', 'Meghalaya'], ['18', 'Assam'], ['19', 'West Bengal'],
  ['20', 'Jharkhand'], ['21', 'Odisha'], ['22', 'Chhattisgarh'], ['23', 'Madhya Pradesh'], ['24', 'Gujarat'],
  ['25', 'Daman and Diu (old)'], ['26', 'Dadra and Nagar Haveli and Daman and Diu'], ['27', 'Maharashtra'],
  ['28', 'Andhra Pradesh (old)'], ['29', 'Karnataka'], ['30', 'Goa'], ['31', 'Lakshadweep'], ['32', 'Kerala'],
  ['33', 'Tamil Nadu'], ['34', 'Puducherry'], ['35', 'Andaman and Nicobar Islands'], ['36', 'Telangana'],
  ['37', 'Andhra Pradesh'], ['38', 'Ladakh'], ['97', 'Other Territory'], ['99', 'Centre Jurisdiction'],
].map(([code, name]) => ({ code, name }));

export const stateName = (code: string | null | undefined) => STATES.find(s => s.code === code)?.name ?? '';

export const GST_RATES = [0, 5, 12, 18, 28];
export const UNITS = ['PCS', 'NOS', 'SET', 'PAIR', 'KG', 'MTR'];

// ---------- GSTIN ----------
const CHARS = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const GSTIN_RE = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$/;

export function gstinChecksum(first14: string): string {
  let sum = 0;
  for (let i = 0; i < 14; i++) {
    const p = CHARS.indexOf(first14[i]) * (i % 2 === 0 ? 1 : 2);
    sum += Math.floor(p / 36) + (p % 36);
  }
  return CHARS[(36 - (sum % 36)) % 36];
}

export function isValidGstin(value: string | null | undefined): boolean {
  if (!value) return false;
  const g = value.trim().toUpperCase();
  if (!GSTIN_RE.test(g)) return false;
  if (!STATES.some(s => s.code === g.slice(0, 2))) return false;
  return g[14] === gstinChecksum(g.slice(0, 14));
}

export const stateFromGstin = (g: string | null | undefined) =>
  g && g.length >= 2 && STATES.some(s => s.code === g.slice(0, 2)) ? g.slice(0, 2) : '';

// ---------- Money ----------
/** Round half away from zero to 2 decimals, matching C# MidpointRounding.AwayFromZero. */
export function round2(v: number): number {
  const sign = v < 0 ? -1 : 1;
  return (sign * Math.round(Math.abs(v) * 100 + 1e-9)) / 100;
}
/** 1234567.5 -> "12,34,567.50" */
export function formatIndian(v: number | null | undefined, decimals = 2): string {
  const n = Number(v ?? 0);
  const neg = n < 0;
  const [int, frac] = Math.abs(n).toFixed(decimals).split('.');
  let grouped = int;
  if (int.length > 3) {
    const last3 = int.slice(-3);
    let rest = int.slice(0, -3);
    const groups: string[] = [];
    while (rest.length > 2) { groups.unshift(rest.slice(-2)); rest = rest.slice(0, -2); }
    if (rest) groups.unshift(rest);
    grouped = groups.join(',') + ',' + last3;
  }
  return (neg ? '-' : '') + grouped + (decimals > 0 ? '.' + frac : '');
}

// ---------- Rates (rule 4.1) ----------
export interface RateSource { defaultRate: number; specialRates: { rate: number; clientIds: string[] }[]; }

export function resolveRate(item: RateSource, clientId: string | null | undefined): { rate: number; isSpecial: boolean } {
  if (clientId) {
    const s = item.specialRates?.find(r => r.clientIds.includes(clientId));
    if (s) return { rate: s.rate, isSpecial: true };
  }
  return { rate: item.defaultRate, isSpecial: false };
}

// ---------- Calculation (rules 4.2, 4.3) ----------
export interface CalcLineInput { quantity: number; rate: number; discount: number; gstRate: number; }
export interface CalcLine extends CalcLineInput {
  amount: number; taxableValue: number; cgst: number; sgst: number; igst: number; lineTotal: number;
}
export interface CalcTotals {
  totalQuantity: number; taxableTotal: number; discountTotal: number;
  cgstTotal: number; sgstTotal: number; igstTotal: number; roundOff: number; grandTotal: number;
}

export const isInterState = (businessState: string, placeOfSupply: string) =>
  !!businessState && !!placeOfSupply && businessState !== placeOfSupply;

export function calculate(input: CalcLineInput[], interState: boolean): { lines: CalcLine[]; totals: CalcTotals } {
  const lines = input.map(l => {
    const quantity = Number(l.quantity) || 0, rate = Number(l.rate) || 0;
    const discount = Number(l.discount) || 0, gstRate = Number(l.gstRate) || 0;
    const amount = round2(quantity * rate);
    const taxableValue = round2(amount - discount);
    let cgst = 0, sgst = 0, igst = 0;
    if (interState) igst = round2((taxableValue * gstRate) / 100);
    else { cgst = round2((taxableValue * (gstRate / 2)) / 100); sgst = cgst; }
    return { quantity, rate, discount, gstRate, amount, taxableValue, cgst, sgst, igst,
             lineTotal: round2(taxableValue + cgst + sgst + igst) };
  });
  const sum = (f: (l: CalcLine) => number) => round2(lines.reduce((a, l) => a + f(l), 0));
  const taxableTotal = sum(l => l.taxableValue), cgstTotal = sum(l => l.cgst);
  const sgstTotal = sum(l => l.sgst), igstTotal = sum(l => l.igst);
  const exact = round2(taxableTotal + cgstTotal + sgstTotal + igstTotal);
  const grandTotal = exact;   // no rounding to the whole rupee (mirrors the server)
  return {
    lines,
    totals: {
      totalQuantity: lines.reduce((a, l) => a + l.quantity, 0),
      discountTotal: sum(l => l.discount),
      taxableTotal, cgstTotal, sgstTotal, igstTotal,
      roundOff: 0, grandTotal,
    },
  };
}

// ---------- Amount in words (rule 4.7) ----------
const ONES = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve',
  'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen'];
const TENS = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety'];

function twoDigits(n: number): string {
  if (n < 20) return ONES[n];
  const t = TENS[Math.floor(n / 10)];
  return n % 10 === 0 ? t : `${t}-${ONES[n % 10]}`;
}

export function numberToWords(n: number): string {
  if (n === 0) return 'Zero';
  const parts: string[] = [];
  const crore = Math.floor(n / 10_000_000); n %= 10_000_000;
  const lakh = Math.floor(n / 100_000); n %= 100_000;
  const thousand = Math.floor(n / 1000); n %= 1000;
  const hundred = Math.floor(n / 100); n %= 100;
  if (crore) parts.push(`${numberToWords(crore)} Crore`);
  if (lakh) parts.push(`${twoDigits(lakh)} Lakh`);
  if (thousand) parts.push(`${twoDigits(thousand)} Thousand`);
  if (hundred) parts.push(`${ONES[hundred]} Hundred`);
  if (n) parts.push(twoDigits(n));
  return parts.join(' ');
}

export function amountInWords(amount: number): string {
  const a = round2(Math.abs(amount));
  const rupees = Math.floor(a);
  const paise = Math.round((a - rupees) * 100);
  let s = `Rupees ${rupees === 0 ? 'Zero' : numberToWords(rupees)}`;
  if (paise > 0) s += ` and ${twoDigits(paise)} Paise`;
  return s + ' Only';
}

// ---------- Financial year / numbering (rule 4.4) ----------
export function financialYear(d: Date): string {
  const start = d.getMonth() >= 3 ? d.getFullYear() : d.getFullYear() - 1;
  return `${start}-${String((start + 1) % 100).padStart(2, '0')}`;
}

export function formatInvoiceNumber(n: { prefix: string; nonGstPrefix?: string; separator: string; padding: number }, seq: number, nonGst = false) {
  const num = String(seq).padStart(Math.min(Math.max(n.padding || 1, 1), 8), '0');
  return [(nonGst ? n.nonGstPrefix : n.prefix)?.trim(), num].filter(Boolean).join(n.separator ?? '');
}

/** Date -> "yyyy-MM-dd" in local time (what the API expects for dates). */
export function toIsoDate(d: Date | string | null | undefined): string | null {
  if (!d) return null;
  const x = typeof d === 'string' ? new Date(d) : d;
  return `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}`;
}

/** API date (UTC midnight ISO) -> local Date for the date pickers. */
export function fromApiDate(s: string | null | undefined): Date | null {
  if (!s) return null;
  const [y, m, d] = s.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}
