import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { h } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import { renderToString } from 'vue/server-renderer'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const { descriptor } = parse(
  readFileSync(new URL('../app/components/CardTitle.vue', import.meta.url), 'utf8'),
)
const script = compileScript(descriptor, { id: 'card-title', inlineTemplate: true })
const compiled = ts.transpileModule(script.content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText
const exports = {}
new Function('require', 'exports', compiled)(require, exports)
const CardTitle = exports.default

test('requires a title and allows the subtitle to be omitted', async () => {
  assert.equal(CardTitle.props.title.required, true)
  assert.equal(CardTitle.props.subtitle.required, false)

  const html = await renderToString(h(CardTitle, { title: 'Upload Files' }))

  assert.match(html, /<h5 class="card-title mb-0">Upload Files<\/h5>/)
  assert.doesNotMatch(html, /<p/)
})

test('renders a muted subtitle with bottom spacing controlled by the caller', async () => {
  const html = await renderToString(
    h(CardTitle, {
      title: 'Upload Files',
      subtitle: 'Select OFX or QFX files from your bank to import transactions.',
      class: 'mb-3',
    }),
  )

  assert.match(html, /^<div class="mb-3">/)
  assert.match(html, /<h5 class="card-title">Upload Files<\/h5>/)
  assert.match(
    html,
    /<p class="text-muted mb-0">Select OFX or QFX files from your bank to import transactions.<\/p>/,
  )
})
