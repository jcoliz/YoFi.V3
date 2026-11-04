/**
 * Date-only helpers for calendar dates such as transaction dates.
 *
 * The generated API client parses date-only strings ("2026-01-01") as UTC midnight,
 * but serializes Date values back to "yyyy-MM-dd" using local-time getters. These
 * helpers keep the calendar day stable in every viewer time zone across both paths.
 */

/**
 * Formats a date-only API value for display without shifting it into the viewer's time zone.
 *
 * @param date - Date parsed by the API client from a date-only value
 * @returns Locale-formatted date, or empty string if undefined
 */
export function formatDateOnly(date: Date | undefined): string {
  if (!date) return ''
  return new Date(date).toLocaleDateString(undefined, { timeZone: 'UTC' })
}

/**
 * Converts a date-only API value into an `<input type="date">` value.
 *
 * @param date - Date parsed by the API client from a date-only value
 * @returns Date in "yyyy-MM-dd" form, or empty string if undefined
 */
export function toDateInputValue(date: Date | undefined): string {
  if (!date) return ''
  return new Date(date).toISOString().slice(0, 10)
}

/**
 * Converts an `<input type="date">` value into a Date the API client serializes as the same day.
 *
 * @param value - Date in "yyyy-MM-dd" form
 * @returns Local-midnight Date for that calendar day
 */
export function fromDateInputValue(value: string): Date {
  const [year, month, day] = value.split('-').map(Number)
  return new Date(year!, month! - 1, day!)
}

/**
 * Gets today's local calendar date as an `<input type="date">` value.
 *
 * @returns Today in "yyyy-MM-dd" form
 */
export function todayDateInputValue(): string {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}
