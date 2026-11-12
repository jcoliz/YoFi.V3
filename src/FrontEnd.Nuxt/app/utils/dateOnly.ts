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

export type TransactionDatePreset =
  'last30Days' | 'last3Months' | 'last12Months' | 'thisYear' | 'allTime'

/**
 * Resolves a date preset using local calendar dates, returning inclusive bounds.
 */
export function transactionDatePreset(
  preset: TransactionDatePreset,
  today = todayDateInputValue(),
): { fromDate: string; toDate: string } {
  if (preset === 'allTime') return { fromDate: '', toDate: '' }

  const [year, month, day] = today.split('-').map(Number)
  const current = new Date(Date.UTC(year!, month! - 1, day!))

  if (preset === 'thisYear') {
    return { fromDate: `${year}-01-01`, toDate: `${year}-12-31` }
  }

  if (preset === 'last30Days') {
    current.setUTCDate(current.getUTCDate() - 29)
  } else {
    const monthsBack = preset === 'last3Months' ? 3 : 12
    const targetMonthIndex = year! * 12 + month! - 1 - monthsBack
    const targetYear = Math.floor(targetMonthIndex / 12)
    const targetMonth = targetMonthIndex % 12
    const lastDayOfTargetMonth = new Date(Date.UTC(targetYear, targetMonth + 1, 0)).getUTCDate()
    current.setUTCFullYear(targetYear, targetMonth, Math.min(day!, lastDayOfTargetMonth))
    current.setUTCDate(current.getUTCDate() + 1)
  }

  const fromDate = [
    current.getUTCFullYear(),
    String(current.getUTCMonth() + 1).padStart(2, '0'),
    String(current.getUTCDate()).padStart(2, '0'),
  ].join('-')

  return { fromDate, toDate: today }
}
