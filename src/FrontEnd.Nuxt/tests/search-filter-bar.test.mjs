import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { createSSRApp, h } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import { renderToString } from 'vue/server-renderer'
import ts from 'typescript'

const require = createRequire(import.meta.url)

function loadComponent(name) {
  const { descriptor } = parse(
    readFileSync(new URL(`../app/components/${name}.vue`, import.meta.url), 'utf8'),
  )
  const script = compileScript(descriptor, { id: name, inlineTemplate: true })
  const compiled = ts.transpileModule(script.content, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText
  const exports = {}
  new Function('require', 'exports', compiled)(require, exports)
  return exports.default
}

const CardTitle = loadComponent('CardTitle')
const SearchFilterBar = loadComponent('SearchFilterBar')

function render(props = {}) {
  const app = createSSRApp({
    render: () => h(SearchFilterBar, props, { default: () => h('span', 'Extra controls') }),
  })
  app.component('CardTitle', CardTitle)
  app.component('FeatherIcon', { render: () => h('span') })
  return renderToString(app)
}

test('uses CardTitle instead of a visible label and keeps an accessible input name', async () => {
  const html = await render()

  assert.match(html, /<div class="mb-3"><h5 class="card-title mb-0">Search<\/h5>/)
  assert.doesNotMatch(html, /<label/)
  assert.match(html, /<input[^>]*aria-label="Search"/)
  assert.match(html, /Extra controls/)
})

test('preserves custom labels, input IDs, search text, and disabled buttons', async () => {
  const html = await render({
    label: 'Find transactions',
    inputId: 'transaction-search',
    modelValue: 'Coffee',
    disabled: true,
  })

  assert.match(html, /<h5 class="card-title mb-0">Find transactions<\/h5>/)
  assert.match(
    html,
    /<input[^>]*id="transaction-search"[^>]*aria-label="Find transactions"[^>]*value="Coffee"/,
  )
  assert.match(html, /data-test-id="search-button" disabled/)
  assert.match(html, /data-test-id="clear-search-button" disabled/)
})
