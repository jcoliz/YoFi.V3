import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { test } from 'node:test'
import ts from 'typescript'

const { outputText } = ts.transpileModule(
  readFileSync(new URL('../app/utils/sampleDataDates.ts', import.meta.url), 'utf8'),
  { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } },
)
const exports = {}
new Function('exports', outputText)(exports)
const { defaultSampleDataDateRanges } = exports

const cases = [
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
  test(`sample data default ranges use local calendar dates in ${timezone}`, (t) => {
    const originalTimezone = process.env.TZ
    process.env.TZ = timezone
    t.after(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    for (const [[year, month, day], expected] of cases) {
      // Late evening local time must not roll into the next UTC day
      const today = new Date(year, month, day, 23, 30)
      assert.deepEqual({ ...defaultSampleDataDateRanges(today) }, expected)
    }
  })
}
