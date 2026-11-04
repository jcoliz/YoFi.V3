import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { createSSRApp } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import { renderToString } from 'vue/server-renderer'
import ts from 'typescript'

const require = createRequire(import.meta.url)

function compile(source, imports = {}) {
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  })
  const exports = {}
  new Function('require', 'exports', outputText)((id) => imports[id] ?? require(id), exports)
  return exports
}

const api = compile(readFileSync(new URL('../app/utils/apiclient.ts', import.meta.url), 'utf8'))
const filename = 'ReviewTable.vue'
const { descriptor } = parse(
  readFileSync(new URL('../app/components/import/ReviewTable.vue', import.meta.url), 'utf8'),
  { filename },
)
const script = compileScript(descriptor, { id: 'import-review-dates', inlineTemplate: true })
const ReviewTable = compile(script.content, { '~/utils/apiclient': api }).default

for (const timezone of ['America/Los_Angeles', 'UTC', 'Pacific/Kiritimati']) {
  test(`import review preserves date-only API values in ${timezone}`, async (t) => {
    const originalTimezone = process.env.TZ
    process.env.TZ = timezone
    t.after(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    const dates = [
      ['2026-10-01', 'Oct 1, 2026'],
      ['2026-11-03', 'Nov 3, 2026'],
      ['2026-01-01', 'Jan 1, 2026'],
      ['2024-02-29', 'Feb 29, 2024'],
      ['2026-03-08', 'Mar 8, 2026'],
      ['2026-11-01', 'Nov 1, 2026'],
    ]
    const transactions = dates.map(([date]) =>
      api.ImportReviewTransactionDto.fromJS({
        key: date,
        date,
        payee: 'Maple Grove Apartments',
        amount: -1500,
        duplicateStatus: api.DuplicateStatus.New,
        isSelected: true,
      }),
    )
    const app = createSSRApp(ReviewTable, { transactions })
    app.component('BaseSpinner', { render: () => null })
    const html = await renderToString(app)
    for (const [, expected] of dates) {
      assert.ok(html.includes(expected), `Expected ${expected} in ${timezone}`)
    }
    assert.ok(!html.includes('Sep 30, 2026'))
  })
}
