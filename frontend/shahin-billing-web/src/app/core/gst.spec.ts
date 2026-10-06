import { describe, expect, it } from 'vitest';
import {
  amountInWords, calculate, financialYear, formatIndian, formatInvoiceNumber, isInterState, isValidGstin,
  resolveRate, round2, stateFromGstin,
} from './gst';

/**
 * These mirror backend/tests/ShahinBilling.Tests/BusinessRuleTests.cs on purpose, with the same numbers:
 * the browser calculates live while you type, the server calculates on save, and the two must agree.
 */
const line = (quantity: number, rate: number, gstRate: number, discount = 0) => ({ quantity, rate, discount, gstRate });

describe('calculate: GST split and totals (rules 4.2, 4.3)', () => {
  it('splits intra-state tax into CGST and SGST, half each', () => {
    const { lines, totals } = calculate([line(10, 100, 18)], false);
    expect(lines[0].taxableValue).toBe(1000);
    expect(lines[0].cgst).toBe(90);
    expect(lines[0].sgst).toBe(90);
    expect(lines[0].igst).toBe(0);
    expect(totals.grandTotal).toBe(1180);
    expect(totals.roundOff).toBe(0);
  });

  it('charges IGST for inter-state sales', () => {
    const { lines, totals } = calculate([line(10, 100, 18)], true);
    expect(lines[0].igst).toBe(180);
    expect(lines[0].cgst).toBe(0);
    expect(totals.grandTotal).toBe(1180);
  });

  it('takes the discount off before tax', () => {
    const { lines, totals } = calculate([line(10, 6500, 18, 500)], false);
    expect(lines[0].taxableValue).toBe(64500);
    expect(lines[0].cgst).toBe(5805);
    expect(totals.grandTotal).toBe(76110);
  });

  it('keeps the paise: there is no round off', () => {
    // 3 x 33.33 = 99.99 ; 9% = 8.9991 -> 9.00 each ; total 117.99 (not 118)
    const { totals } = calculate([line(3, 33.33, 18)], false);
    expect(totals.grandTotal).toBe(117.99);
    expect(totals.roundOff).toBe(0);
  });

  it('is exact even with no tax', () => {
    expect(calculate([line(1, 100.4, 0)], false).totals.grandTotal).toBe(100.4);
  });

  it('a Non-GST bill (rate 0) has no tax and the plain total', () => {
    const { totals } = calculate([line(3, 90, 0)], false);
    expect(totals.cgstTotal + totals.sgstTotal + totals.igstTotal).toBe(0);
    expect(totals.grandTotal).toBe(270);
  });

  it('adds up several lines', () => {
    const { totals } = calculate([line(10, 6500, 18, 500), line(40, 120, 18)], false);
    expect(totals.taxableTotal).toBe(69300);
    expect(totals.cgstTotal).toBe(6237);
    expect(totals.grandTotal).toBe(81774);
  });

  it('treats empty or text input as zero instead of breaking', () => {
    const { totals } = calculate([{ quantity: NaN, rate: 100, discount: 0, gstRate: 18 }], false);
    expect(totals.grandTotal).toBe(0);
  });
});

describe('round2 rounds half away from zero, like the server', () => {
  it.each([[1.005, 1.01], [2.675, 2.68], [-1.005, -1.01], [0.004, 0]])('%s -> %s', (input, expected) => {
    expect(round2(input)).toBe(expected);
  });
});

describe('tax type from the states (rule 4.2)', () => {
  it('same state -> CGST+SGST, different -> IGST, unknown -> not inter-state', () => {
    expect(isInterState('29', '29')).toBe(false);
    expect(isInterState('29', '27')).toBe(true);
    expect(isInterState('', '27')).toBe(false);
    expect(isInterState('29', '')).toBe(false);
  });
});

describe('client rate resolution (rule 4.1)', () => {
  const item = { defaultRate: 100, specialRates: [{ rate: 80, clientIds: ['a', 'b'] }, { rate: 70, clientIds: ['c'] }] };
  it('uses the special rate of the group the client is in', () => {
    expect(resolveRate(item, 'a')).toEqual({ rate: 80, isSpecial: true });
    expect(resolveRate(item, 'c')).toEqual({ rate: 70, isSpecial: true });
  });
  it('falls back to the default rate', () => {
    expect(resolveRate(item, 'z')).toEqual({ rate: 100, isSpecial: false });
    expect(resolveRate(item, null)).toEqual({ rate: 100, isSpecial: false });
  });
});

describe('amount in words, Indian system (rule 4.7)', () => {
  it.each([
    [0, 'Rupees Zero Only'],
    [1180, 'Rupees One Thousand One Hundred Eighty Only'],
    [123450, 'Rupees One Lakh Twenty-Three Thousand Four Hundred Fifty Only'],
    [10000000, 'Rupees One Crore Only'],
    [25075021, 'Rupees Two Crore Fifty Lakh Seventy-Five Thousand Twenty-One Only'],
    [10.5, 'Rupees Ten and Fifty Paise Only'],
  ])('%s', (amount, words) => expect(amountInWords(amount)).toBe(words));
});

describe('Indian number format', () => {
  it.each([[1234567.5, '12,34,567.50'], [999, '999.00'], [100000, '1,00,000.00'], [-1500, '-1,500.00']])('%s', (n, text) => {
    expect(formatIndian(n)).toBe(text);
  });
});

describe('GSTIN (rule 4.8)', () => {
  it.each([
    ['27AAPFU0939F1ZV', true],
    ['29AAGCB7383J1Z4', true],
    ['27aapfu0939f1zv', true],       // lower case accepted
    ['27AAPFU0939F1ZX', false],      // wrong checksum
    ['27AAPFU0939F1Z', false],       // too short
    ['00AAPFU0939F1ZV', false],      // bad state
    ['', false],
  ])('%s -> %s', (g, ok) => expect(isValidGstin(g)).toBe(ok));

  it('takes the state from the first two digits', () => {
    expect(stateFromGstin('29AAGCB7383J1Z4')).toBe('29');
    expect(stateFromGstin('00XXXX')).toBe('');
  });
});

describe('invoice numbers (rule 4.4)', () => {
  const n = { prefix: 'SE', nonGstPrefix: 'NG', separator: '-', padding: 4 };
  it('GST and Non-GST bills use their own prefix and keep counting', () => {
    expect(formatInvoiceNumber(n, 7)).toBe('SE-0007');
    expect(formatInvoiceNumber(n, 7, true)).toBe('NG-0007');
  });
  it('skips an empty prefix', () => {
    expect(formatInvoiceNumber({ prefix: '', separator: '-', padding: 3 }, 12)).toBe('012');
  });
});

describe('financial year starts in April', () => {
  it.each([[new Date(2026, 2, 31), '2025-26'], [new Date(2026, 3, 1), '2026-27'], [new Date(2026, 11, 15), '2026-27']])('%s', (d, fy) => {
    expect(financialYear(d)).toBe(fy);
  });
});
