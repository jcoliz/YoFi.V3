import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { createPinia, storeToRefs } from 'pinia'
import { computed, createSSRApp, effectScope, h, nextTick, reactive, ref, watch } from 'vue'
import { renderToString } from 'vue/server-renderer'
import { compileScript, compileTemplate, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const storeSource = readFileSync(
  new URL('../app/stores/transactionSearch.ts', import.meta.url),
  'utf8',
)
const { descriptor } = parse(
  readFileSync(new URL('../app/pages/transactions/index.vue', import.meta.url), 'utf8'),
)
const pageSource = compileScript(descriptor, { id: 'transactions' }).content

function loadScript(source, globals, imports = {}) {
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText
  const exports = {}
  new Function('require', 'exports', ...Object.keys(globals), compiled)(
    (id) => imports[id] ?? require(id),
    exports,
    ...Object.values(globals),
  )
  return exports
}

const { useTransactionSearchStore } = loadScript(storeSource, { ref, computed })
const { ssrRender } = loadScript(
  compileTemplate({
    source: descriptor.template.content,
    filename: 'transactions.vue',
    id: 'transactions',
    ssr: true,
    ssrCssVars: [],
  }).code,
  {},
)

function renderPage(page) {
  const app = createSSRApp({ setup: () => ({ ...page }), ssrRender })
  for (const name of [
    'WorkspaceSelector',
    'FeatherIcon',
    'ErrorDisplay',
    'SearchFilterBar',
    'BaseSpinner',
    'TransactionCreateModal',
    'TransactionEditModal',
    'PayeeRulesDialog',
  ]) {
    app.component(name, {
      setup:
        (_props, { slots }) =>
        () =>
          h('div', slots.default?.()),
    })
  }
  app.component('PaginationBar', { render: () => h('div', { 'data-test-id': 'PaginationBar' }) })
  return renderToString(app)
}

function harness(t) {
  const pinia = createPinia()
  const store = useTransactionSearchStore(pinia)
  const preferences = reactive({
    getCurrentTenantKey: 'workspace-one',
    hasTenant: true,
    getCurrentTenant: { role: 3 },
    loadFromStorage() {},
  })
  const requests = []
  let failRequest = false
  let pendingRequest

  async function mount() {
    let initialize
    const scope = effectScope()
    t.after(() => scope.stop())
    const { default: component } = loadScript(
      pageSource,
      {
        ref,
        computed,
        watch,
        storeToRefs,
        definePageMeta() {},
        navigateTo() {},
        useApiClient: () => ({
          async getTransactions(...args) {
            requests.push(args)
            if (pendingRequest) await pendingRequest
            if (failRequest) throw new Error('Request failed')
            return {
              items: [{ key: `page-${args[0]}` }],
              metadata: { pageNumber: args[0], totalPages: 3 },
            }
          },
        }),
        useAuthenticationGuard: () => ({
          requireAuthenticatedUser(callback) {
            initialize = callback
          },
        }),
      },
      {
        '~/stores/transactionSearch': { useTransactionSearchStore: () => store },
        '~/stores/userPreferences': { useUserPreferencesStore: () => preferences },
        '~/utils/apiclient': { TenantRole: { Viewer: 1, Editor: 2, Owner: 3 } },
        '~/utils/errorHandler': { handleApiError: (error) => ({ detail: error.message }) },
        '~/utils/dateOnly': { formatDateOnly: (date) => date },
      },
    )
    const page = scope.run(() => component.setup({}, { expose() {} }))
    await initialize()
    return { page, unmount: () => scope.stop() }
  }

  return {
    store,
    preferences,
    requests,
    mount,
    fail: () => (failRequest = true),
    pause() {
      let resume
      pendingRequest = new Promise((resolve) => (resume = resolve))
      return resume
    },
  }
}

test('keeps the table and pagination in the layout while the next page is loading', async (t) => {
  const { mount, pause } = harness(t)
  const { page } = await mount()
  const resume = pause()
  const loading = page.loadTransactions(2)
  const html = await renderPage(page)

  assert.match(html, /data-test-id="transactions-card" aria-busy="true"/)
  assert.match(html, /class="position-absolute[^"]*" data-test-id="loading-state"/)
  assert.match(html, /class="card-body" inert/)
  assert.match(html, /data-test-id="transactions-table"/)
  assert.match(html, /data-test-id="row-page-1"/)
  assert.match(html, /data-test-id="PaginationBar"/)

  resume()
  await loading
  const loadedHtml = await renderPage(page)
  assert.match(loadedHtml, /aria-busy="false"/)
  assert.doesNotMatch(loadedHtml, /data-test-id="loading-state"/)
  assert.doesNotMatch(loadedHtml, /class="card-body" inert/)
  assert.match(loadedHtml, /data-test-id="row-page-2"/)
})

test('retains the selected page and filters when returning from transaction details', async (t) => {
  const { store, requests, mount } = harness(t)
  store.searchText = 'Coffee'
  store.fromDate = '2026-01-01'
  store.toDate = '2026-01-31'
  const first = await mount()
  assert.equal(requests.at(-1)[0], 1)

  first.page.handlePageChange(2)
  await nextTick()
  assert.equal(store.currentPage, 2)
  first.unmount()

  const second = await mount()
  assert.deepEqual(requests.at(-1), [
    2,
    new Date('2026-01-01'),
    new Date('2026-01-31'),
    'Coffee',
    'workspace-one',
  ])
  assert.equal(second.page.currentPage.value, 2)
  assert.equal(second.page.transactions.value[0].key, 'page-2')

  await second.page.loadTransactions()
  assert.equal(requests.at(-1)[0], 2)
})

test('search and filter changes return to page 1', async (t) => {
  const { store, requests, mount } = harness(t)
  const { page } = await mount()
  for (const clear of [page.clearSearch, page.clearFilters]) {
    await page.loadTransactions(2)
    store.searchText = 'Coffee'
    store.fromDate = '2026-01-01'
    clear()
    await nextTick()
    assert.equal(requests.at(-1)[0], 1)
    assert.equal(store.currentPage, 1)
  }
  await page.loadTransactions(2)
  store.searchText = 'Tea'
  await page.loadTransactions(1)
  assert.equal(store.currentPage, 1)
  assert.equal(requests.at(-1)[3], 'Tea')
})

test('workspace changes return to page 1', async (t) => {
  const { store, preferences, requests, mount } = harness(t)
  const { page } = await mount()
  await page.loadTransactions(2)
  preferences.getCurrentTenantKey = 'workspace-two'
  await nextTick()
  assert.equal(store.currentPage, 1)
  assert.equal(requests.at(-1)[0], 1)
  assert.equal(requests.at(-1)[4], 'workspace-two')
})

test('failed page loads preserve the last successfully loaded page and display an error', async (t) => {
  const { store, mount, fail } = harness(t)
  const { page } = await mount()
  await page.loadTransactions(2)
  fail()
  await page.loadTransactions(3)
  assert.equal(store.currentPage, 2)
  assert.equal(page.transactions.value[0].key, 'page-2')
  assert.equal(page.showError.value, true)
  assert.equal(page.error.value.detail, 'Request failed')
  const html = await renderPage(page)
  assert.doesNotMatch(html, /data-test-id="loading-state"/)
  assert.doesNotMatch(html, /class="card-body" inert/)
})

test('reset clears retained search state including pagination', () => {
  const store = useTransactionSearchStore(createPinia())
  store.searchText = 'Coffee'
  store.fromDate = '2026-01-01'
  store.toDate = '2026-01-31'
  store.currentPage = 2
  store.reset()
  assert.equal(store.searchText, '')
  assert.equal(store.fromDate, '')
  assert.equal(store.toDate, '')
  assert.equal(store.hasActiveFilters, false)
  assert.equal(store.currentPage, 1)
})
