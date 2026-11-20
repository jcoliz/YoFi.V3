/** Defines the depth choices offered by report display controls. */
export const MAX_REPORT_DISPLAY_DEPTH = 4

/** Returns whether a report display depth is an integer within the offered range. */
export function isSupportedReportDisplayDepth(value: number): boolean {
  return Number.isInteger(value) && value >= 1 && value <= MAX_REPORT_DISPLAY_DEPTH
}
