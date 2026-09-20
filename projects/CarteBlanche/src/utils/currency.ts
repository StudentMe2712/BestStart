/**
 * Currency utility functions for Carte Blanche Fine Dining Menu.
 * Supports Kazakhstani Tenge (₸) formatting and arithmetic helpers.
 */

/**
 * Formats a numeric value into Kazakhstani Tenge (₸) with space thousand-separators.
 * Example: 14500 -> "14 500 ₸"
 * Example: 7200 -> "7 200 ₸"
 * Example: 0 -> "0 ₸"
 */
export function formatKZT(amount: number): string {
  if (amount === undefined || amount === null || isNaN(amount)) {
    return '0 ₸';
  }

  const rounded = Math.round(amount);
  const formatted = rounded.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
  return `${formatted} ₸`;
}

/**
 * Parses a KZT formatted string or arbitrary numeric string into a clean number.
 * Example: "14 500 ₸" -> 14500
 * Example: "7 200" -> 7200
 */
export function parseKZT(value: string): number {
  if (!value) return 0;
  const cleaned = value.replace(/[^0-9.-]+/g, '');
  const parsed = parseFloat(cleaned);
  return isNaN(parsed) ? 0 : parsed;
}

/**
 * Calculates service charge (e.g. 10%, 15%) for fine dining bill.
 */
export function calculateServiceCharge(subtotal: number, percentage: number): number {
  return Math.round((subtotal * percentage) / 100);
}

/**
 * Calculates per-person split of a total KZT bill.
 */
export function splitBillKZT(total: number, guestCount: number): number {
  if (guestCount <= 0) return total;
  return Math.round(total / guestCount);
}
