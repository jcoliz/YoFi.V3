import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { computed, createRenderer, h, nextTick, ref, watch } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const source = readFileSync(
  new URL('../app/components/transaction/EditModal.vue', import.meta.url),
  'utf8',
)
const { descriptor } = parse(source)
const compiled = compileScript(descriptor, { id: 'transaction-edit', inlineTemplate: true })
const script = ts.transpileModule(compiled.content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText

function node(type, text = '') {
  return {
    type,
    text,
    props: {},
    children: [],
    parent: undefined,
    addEventListener() {},
    removeEventListener() {},
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
    const globals = {
      ref,
      computed,
      watch,
      useApiClient: () => ({
        quickEditTransaction: async (...args) => calls.push(args),
      }),
    }
    const exports = {}
    new Function('require', 'exports', ...Object.keys(globals), script)(
      (id) => {
        if (id === '~/utils/apiclient') return { TransactionsClient, TransactionQuickEditDto }
        if (id === '~/stores/userPreferences')
          return { useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace' }) }
        if (id === '~/utils/errorHandler') return { handleApiError: (error) => error }
        return require(id)
      },
      exports,
      ...Object.values(globals),
    )
    const root = node('root')
    const app = renderer.createApp({
      setup: () => () =>
        h(exports.default, {
          show: true,
          transaction: {
            key: 'transaction',
            payee: 'Store',
            memo: 'Note',
            category: 'Food',
            hasMultipleSplits,
          },
        }),
    })
    app.component('ModalDialog', {
      setup:
        (_props, { slots }) =>
        () =>
          h('div', [slots.default?.(), slots.footer?.()]),
    })
    for (const name of ['ErrorDisplay', 'FeatherIcon', 'BaseSpinner'])
      app.component(name, { render: () => h('span') })
    app.mount(root)
    t.after(() => app.unmount())
    const category = find(root, 'edit-transaction-category')
    assert.equal(category.props.disabled, hasMultipleSplits)
    assert.equal(category.props['aria-describedby'], 'editCategoryHelp')
    const help = find(root, 'edit-transaction-category-help')
    assert.ok(
      help.text.includes(
        hasMultipleSplits ? 'split across multiple categories' : 'Optional category',
      ),
    )
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
  assert.match(
    page,
    /v-if="transaction\.hasMultipleSplits"[\s\S]*?data-test-id="transaction-split-badge"/,
  )
  assert.match(page, /title="Allocated across multiple splits\."/)
  assert.match(page, /if \(!selectedTransaction\.value\?\.hasMultipleSplits && rule\.category/)
})

function textContent(element) {
  return element.text + element.children.map(textContent).join('')
}

for (const splits of [
  undefined,
  [],
  [
    { key: 'salary', category: 'Income:Salary', amount: 100, memo: 'Gross pay', order: 0 },
    { key: 'tax', category: 'Taxes', amount: -25, memo: null, order: 1 },
  ],
]) {
  test(`details ${splits?.length ? 'displays included splits' : 'keeps the normal category display'}`, async (t) => {
    const pageSource = readFileSync(
      new URL('../app/pages/transactions/[key].vue', import.meta.url),
      'utf8',
    )
    const { descriptor } = parse(pageSource)
    const compiled = compileScript(descriptor, { id: 'transaction-details', inlineTemplate: true })
    const pageScript = ts.transpileModule(compiled.content, {
      compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    }).outputText
    const transaction = {
      key: 'transaction',
      payee: 'Employer',
      amount: 75,
      date: new Date('2026-01-01'),
      category: splits?.length ? '' : 'Income',
      splits,
    }
    const roles = { Viewer: 1, Editor: 2, Owner: 3 }
    const role = splits?.length ? roles.Viewer : roles.Editor
    let load
    const globals = {
      ref,
      computed,
      definePageMeta() {},
      useRoute: () => ({ params: { key: 'transaction' } }),
      useRouter: () => ({ push() {} }),
      useApiClient: () => ({ getTransactionById: async () => transaction }),
      useAuthenticationGuard: () => ({
        requireAuthenticatedUser: (callback) => {
          load = callback
        },
      }),
    }
    const exports = {}
    new Function('require', 'exports', ...Object.keys(globals), pageScript)(
      (id) => {
        if (id === '~/utils/apiclient') return { TransactionsClient, TenantRole: roles }
        if (id === '~/stores/userPreferences')
          return {
            useUserPreferencesStore: () => ({
              getCurrentTenantKey: 'workspace',
              hasTenant: true,
              getCurrentTenant: { role },
              loadFromStorage() {},
            }),
          }
        if (id === '~/utils/errorHandler') return { handleApiError: (error) => error }
        if (id === '~/utils/dateOnly') return { formatDateOnly: () => '1/1/2026' }
        return require(id)
      },
      exports,
      ...Object.values(globals),
    )
    const root = node('root')
    const app = renderer.createApp(exports.default)
    for (const name of [
      'WorkspaceSelector',
      'PageBreadcrumb',
      'ErrorDisplay',
      'BaseSpinner',
      'FeatherIcon',
      'TransactionEditPane',
      'ModalDialog',
    ])
      app.component(name, { render: () => h('span') })
    app.component('CardTitle', {
      props: ['title'],
      setup: (props) => () => h('div', props.title),
    })
    app.mount(root)
    t.after(() => app.unmount())
    await load()
    await nextTick()
    if (splits?.length) {
      assert.equal(textContent(find(root, 'transaction-category')), '(none)')
      const splitsCard = find(root, 'transaction-splits')
      const detailsCard = find(root, 'transaction-details-card')
      assert.equal(splitsCard.props.class, 'card mt-3')
      assert.equal(splitsCard.parent, detailsCard.parent)
      const table = find(root, 'transaction-splits-table')
      assert.equal(table.props['aria-labelledby'], 'transaction-splits-title')
      const salary = find(root, 'transaction-split-salary')
      const tax = find(root, 'transaction-split-tax')
      assert.deepEqual(salary.children.map(textContent), ['Income:Salary', '$100.00', 'Gross pay'])
      assert.deepEqual(tax.children.map(textContent), ['Taxes', '-$25.00', ''])
      assert.ok(salary.parent.children.indexOf(salary) < salary.parent.children.indexOf(tax))
    } else {
      assert.equal(textContent(find(root, 'transaction-category')), 'Income')
      assert.ok(find(root, 'transaction-splits'))
      assert.equal(textContent(find(root, 'transaction-no-splits')).trim(), 'No additional splits.')
      assert.ok(find(root, 'add-split-button'))
    }
  })
}

test('details add-split form submits the API DTO and retains failures in the form', () => {
  const pageSource = readFileSync(
    new URL('../app/pages/transactions/[key].vue', import.meta.url),
    'utf8',
  )
  assert.match(
    pageSource,
    /transactionsClient\.addSplit\([\s\S]*?transaction\.value\.key,[\s\S]*?currentTenantKey\.value,[\s\S]*?new SplitEditDto/,
  )
  assert.match(
    pageSource,
    /amount: Number\(splitForm\.value\.amount\)[\s\S]*?category: splitForm\.value\.category\.trim\(\) \|\| undefined[\s\S]*?memo: splitForm\.value\.memo\.trim\(\) \|\| undefined/,
  )
  assert.match(pageSource, /transaction\.value = updatedTransaction[\s\S]*?showAddSplit\.value = false/)
  assert.match(
    pageSource,
    /catch \(err\) \{[\s\S]*?splitError\.value = handleApiError\(err, 'Add Split Failed', 'Failed to add split'\)[\s\S]*?showSplitError\.value = true/,
  )
  assert.match(pageSource, /data-test-id="add-split-form"[\s\S]*?@submit\.prevent="addSplit"/)
})
