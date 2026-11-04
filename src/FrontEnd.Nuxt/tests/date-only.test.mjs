import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import ts from 'typescript'

const require = createRequire(import.meta.url)

function compile(path) {
  const { outputText } = ts.transpileModule(readFileSync(new URL(path, import.meta.url), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  })
  const exports = {}
  new Function('require', 'exports', outputText)(require, exports)
  return exports
}

const api = compile('../app/utils/apiclient.ts')
const dateOnly = compile('../app/utils/dateOnly.ts')

const dates = ['2026-01-01', '2025-12-31', '2024-02-29', '2026-03-08', '2026-11-01', '2026-10-01']

for (const timezone of ['America/Los_Angeles', 'UTC', 'Pacific/Kiritimati']) {
  test(`date-only transaction values keep their calendar day in ${timezone}`, (t) => {
    const originalTimezone = process.env.TZ
    process.env.TZ = timezone
    t.after(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    for (const value of dates) {
      const [year, month, day] = value.split('-').map(Number)
      const expectedDisplay = new Date(year, month - 1, day).toLocaleDateString()
      const parsed = api.TransactionResultDto.fromJS({ key: value, date: value }).date

      assert.equal(dateOnly.formatDateOnly(parsed), expectedDisplay, `display ${value}`)
      assert.equal(dateOnly.toDateInputValue(parsed), value, `edit form ${value}`)

      const dto = new api.TransactionEditDto({
        date: dateOnly.fromDateInputValue(value),
        payee: 'x',
      })
      assert.equal(dto.toJSON().date, value, `save ${value}`)
    }

    assert.equal(dateOnly.formatDateOnly(undefined), '')
    assert.equal(dateOnly.toDateInputValue(undefined), '')

    const now = new Date()
    const today = dateOnly.todayDateInputValue()
    assert.match(today, /^\d{4}-\d{2}-\d{2}$/)
    assert.equal(dateOnly.fromDateInputValue(today).getDate(), now.getDate())
  })
}
