import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { defaultSampleDataDateRanges } from '~/utils/sampleDataDates'

const cases: [number[], Record<string, string>][] = [
  [
    [2026, 5, 15],
    {
      historicalStart: '2025-06-01',
      historicalEnd: '2026-03-31',
      ofxStart: '2026-04-01',
      ofxEnd: '2026-06-15',
    },
  ],
  [
    [2026, 0, 1],
    {
      historicalStart: '2025-01-01',
      historicalEnd: '2025-10-31',
      ofxStart: '2025-11-01',
      ofxEnd: '2026-01-01',
    },
  ],
  [
    [2024, 4, 31],
    {
      historicalStart: '2023-05-01',
      historicalEnd: '2024-02-29',
      ofxStart: '2024-03-01',
      ofxEnd: '2024-05-31',
    },
  ],
  [
    [2025, 11, 31],
    {
      historicalStart: '2024-12-01',
      historicalEnd: '2025-09-30',
      ofxStart: '2025-10-01',
      ofxEnd: '2025-12-31',
    },
  ],
]

for (const timezone of ['America/Los_Angeles', 'UTC', 'Pacific/Kiritimati']) {
  describe(`sample data default ranges use local calendar dates in ${timezone}`, () => {
    let originalTimezone: string | undefined

    beforeEach(() => {
      originalTimezone = process.env.TZ
      process.env.TZ = timezone
    })

    afterEach(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    for (const [[year, month, day], expected] of cases) {
      it(`computes correct ranges for ${year}-${String(month + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`, () => {
        // Late evening local time must not roll into the next UTC day
        const today = new Date(year, month, day, 23, 30)
        expect({ ...defaultSampleDataDateRanges(today) }).toEqual(expected)
      })
    }
  })
}
