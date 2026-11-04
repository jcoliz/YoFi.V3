/**
 * Default date ranges for the sample-data workflow.
 *
 * All values are local calendar dates in "yyyy-MM-dd" form, computed from the
 * browser's local clock, never a server or UTC date.
 */

export interface SampleDataDateRanges {
  /** First day of the month 12 months before today's month */
  historicalStart: string
  /** Last day of the month three months before today's month */
  historicalEnd: string
  /** First day of the month two months before today's month */
  ofxStart: string
  /** Today */
  ofxEnd: string
}

function toInputValue(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}

/**
 * Calculates the default historical-load and OFX-download ranges.
 *
 * @param today - Current local date; defaults to now
 * @returns Inclusive default ranges in "yyyy-MM-dd" form
 *
 * @example
 * // For 2026-06-15: 2025-06-01..2026-03-31 historical, 2026-04-01..2026-06-15 OFX
 * defaultSampleDataDateRanges(new Date(2026, 5, 15))
 */
export function defaultSampleDataDateRanges(today: Date = new Date()): SampleDataDateRanges {
  const year = today.getFullYear()
  const month = today.getMonth()
  return {
    historicalStart: toInputValue(new Date(year, month - 12, 1)),
    // Day 0 of a month is the last day of the previous month
    historicalEnd: toInputValue(new Date(year, month - 2, 0)),
    ofxStart: toInputValue(new Date(year, month - 2, 1)),
    ofxEnd: toInputValue(new Date(year, month, today.getDate())),
  }
}
