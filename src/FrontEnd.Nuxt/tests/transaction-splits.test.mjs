import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { computed, createRenderer, h, nextTick, ref, watch } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const source = readFileSync(new URL('../app/components/transaction/EditModal.vue', import.meta.url), 'utf8')
const { descriptor } = parse(source)
const compiled = compileScript(descriptor, { id: 'transaction-edit', inlineTemplate: true })
const script = ts.transpileModule(compiled.content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText

function node(type, text = '') {
  return {
    type, text, props: {}, children: [], parent: undefined,
    addEventListener() {}, removeEventListener() {},
    getRootNode: () => ({ activeElement: null }),
  }
}
const renderer = createRenderer({
  createElement: node,
  createText: (text) => node('#text', text),
  createComment: (text) => node('#comment', text),
  setText: (element, text) => (element.text = text),
  setElementText: (element, text) => (element.text = text),
  patchProp: (element, key, _previous, value) => (element.props[key] = value),
  parentNode: (element) => element.parent,
  nextSibling: () => null,
  insert(element, parent) {
    parent.children.push(element)
    element.parent = parent
  },
  remove(element) {
    element.parent.children.splice(element.parent.children.indexOf(element), 1)
  },
})

function find(root, id) {
  if (root.props['data-test-id'] === id) return root
  return root.children.map((child) => find(child, id)).find(Boolean)
}

class TransactionsClient {}
class TransactionQuickEditDto {
  constructor(data) {
    Object.assign(this, data)
  }
}

for (const hasMultipleSplits of [false, true]) {
  test(`quick edit ${hasMultipleSplits ? 'protects split categories' : 'allows normal category edits'}`, async (t) => {
    const originalDocument = globalThis.Document
    const originalShadowRoot = globalThis.ShadowRoot
    globalThis.Document = class Document {}
    globalThis.ShadowRoot = class ShadowRoot {}
    t.after(() => {
      if (originalDocument === undefined) delete globalThis.Document
      else globalThis.Document = originalDocument
      if (originalShadowRoot === undefined) delete globalThis.ShadowRoot
      else globalThis.ShadowRoot = originalShadowRoot
    })
    const calls = []
    const globals = { ref, computed, watch, useApiClient: () => ({
      quickEditTransaction: async (...args) => calls.push(args),
    }) }
    const exports = {}
    new Function('require', 'exports', ...Object.keys(globals), script)(
      (id) => {
        if (id === '~/utils/apiclient') return { TransactionsClient, TransactionQuickEditDto }
        if (id === '~/stores/userPreferences')
          return { useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace' }) }
        if (id === '~/utils/errorHandler') return { handleApiError: (error) => error }
        return require(id)
      }, exports, ...Object.values(globals),
    )
    const root = node('root')
    const app = renderer.createApp({
      setup: () => () => h(exports.default, {
        show: true,
        transaction: { key: 'transaction', payee: 'Store', memo: 'Note', category: 'Food', hasMultipleSplits },
      }),
    })
    app.component('ModalDialog', { setup: (_props, { slots }) => () => h('div', [slots.default?.(), slots.footer?.()]) })
    for (const name of ['ErrorDisplay', 'FeatherIcon', 'BaseSpinner'])
      app.component(name, { render: () => h('span') })
    app.mount(root)
    t.after(() => app.unmount())
    const category = find(root, 'edit-transaction-category')
    assert.equal(category.props.disabled, hasMultipleSplits)
    assert.equal(category.props['aria-describedby'], 'editCategoryHelp')
    const help = find(root, 'edit-transaction-category-help')
    assert.ok(help.text.includes(hasMultipleSplits ? 'split across multiple categories' : 'Optional category'))
    find(root, 'edit-transaction-payee').props['onUpdate:modelValue']('Updated payee')
    find(root, 'edit-transaction-memo').props['onUpdate:modelValue']('Updated memo')
    category.props['onUpdate:modelValue']('Changed category')
    find(root, 'edit-submit-button').props.onClick()
    await nextTick()
    assert.equal(calls.length, 1)
    assert.equal(calls[0][2].payee, 'Updated payee')
    assert.equal(calls[0][2].memo, 'Updated memo')
    assert.equal(calls[0][2].category, hasMultipleSplits ? undefined : 'Changed category')
  })
}

test('list distinguishes split transactions and protects them when creating rules', () => {
  const page = readFileSync(new URL('../app/pages/transactions/index.vue', import.meta.url), 'utf8')
  assert.match(page, /v-if="transaction\.hasMultipleSplits"[\s\S]*?data-test-id="transaction-split-badge"/)
  assert.match(page, /title="Allocated across multiple splits\."/)
  assert.match(page, /if \(!selectedTransaction\.value\?\.hasMultipleSplits && rule\.category/)
})
