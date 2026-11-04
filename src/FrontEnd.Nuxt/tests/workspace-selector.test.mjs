import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { computed, createRenderer, h, nextTick, onMounted, reactive, ref } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)

function compileComponent(path, id) {
  const { descriptor } = parse(readFileSync(new URL(path, import.meta.url), 'utf8'), {
    filename: path,
  })
  const script = compileScript(descriptor, { id, inlineTemplate: true })
  return ts.transpileModule(script.content, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText
}

const selectorSource = compileComponent('../app/components/workspace/Selector.vue', 'selector')
const menuSource = compileComponent('../app/components/workspace/MenuSection.vue', 'menu')

function node(type, text = '') {
  return { type, text, props: {}, children: [], parent: undefined }
}

const renderer = createRenderer({
  createElement: (type) => node(type),
  createText: (text) => node('#text', text),
  createComment: (text) => node('#comment', text),
  setText: (element, text) => (element.text = text),
  setElementText(element, text) {
    element.children = []
    element.text = text
  },
  patchProp: (element, key, _previous, value) => (element.props[key] = value),
  parentNode: (element) => element.parent,
  nextSibling(element) {
    const siblings = element.parent?.children ?? []
    return siblings[siblings.indexOf(element) + 1]
  },
  insert(element, parent, anchor) {
    if (element.parent) {
      element.parent.children.splice(element.parent.children.indexOf(element), 1)
    }
    const index = anchor ? parent.children.indexOf(anchor) : parent.children.length
    parent.children.splice(index, 0, element)
    element.parent = parent
  },
  remove(element) {
    element.parent.children.splice(element.parent.children.indexOf(element), 1)
    element.parent = undefined
  },
})

function findAll(root, predicate, found = []) {
  if (predicate(root)) found.push(root)
  for (const child of root.children) findAll(child, predicate, found)
  return found
}

const byTestId = (root, testId) => findAll(root, (n) => n.props['data-test-id'] === testId)

function text(root) {
  return (root.text + root.children.map(text).join('')).replace(/\s+/g, ' ').trim()
}

const tenants = ['First', 'Second', 'Third', 'Fourth', 'Fifth', 'Sixth'].map((name) => ({
  key: name.toLowerCase(),
  name: `${name} workspace`,
}))

async function mount(t, componentSource, { listeners = {} } = {}) {
  const store = reactive({
    currentTenant: tenants[0],
    get getCurrentTenantKey() {
      return this.currentTenant?.key
    },
    get hasTenant() {
      return Boolean(this.currentTenant)
    },
    setCurrentTenant(tenant) {
      this.currentTenant = tenant
    },
    clearPreferences() {
      this.currentTenant = undefined
    },
    loadFromStorage() {},
  })
  const exports = {}
  const globals = {
    ref,
    computed,
    onMounted,
    navigateTo: async () => {},
    window: { location: { pathname: '/transactions' } },
    useApiClient: () => ({ getTenants: async () => tenants }),
  }
  new Function('require', 'exports', ...Object.keys(globals), componentSource)(
    (id) => {
      if (id === '~/stores/userPreferences') return { useUserPreferencesStore: () => store }
      if (id === '~/utils/apiclient') return { TenantClient: class {} }
      return require(id)
    },
    exports,
    ...Object.values(globals),
  )

  const root = node('root')
  const app = renderer.createApp({ render: () => h(exports.default, listeners) })
  app.component('DropDownPortable', {
    setup:
      (_props, { slots }) =>
      () =>
        h('div', [slots.trigger?.(), slots.default?.()]),
  })
  app.component('NuxtLink', {
    props: ['to'],
    setup:
      (props, { attrs, slots }) =>
      () =>
        h('a', { ...attrs, href: props.to }, slots.default()),
  })
  app.component('FeatherIcon', {
    props: ['icon'],
    setup: (props) => () => h('svg', { icon: props.icon }),
  })
  app.component('BaseSpinner', { render: () => null })
  app.component('WorkspaceCopyKeyButton', { render: () => null })
  app.mount(root)
  t.after(() => app.unmount())
  await nextTick()
  await nextTick()
  return { root, store }
}

function workspaceSection(root, header) {
  return findAll(root, (n) => n.children.some((c) => c.type === 'div' && text(c) === header))[0]
}

test('selector panel matches the menu layout and lists every other workspace', async (t) => {
  const { root } = await mount(t, selectorSource)

  assert.equal(findAll(root, (n) => n.type === 'select').length, 0)
  const section = workspaceSection(root, 'Workspace')
  assert.ok(section, 'Expected a Workspace section header')

  const [current] = byTestId(root, 'selector-current-workspace')
  assert.equal(text(current), 'First workspace')
  assert.match(current.props.class, /workspace-active.*bg-light rounded/)
  assert.ok(findAll(current, (n) => n.props.icon === 'check').length === 1)

  const options = byTestId(root, 'workspace-option')
  assert.deepEqual(
    options.map(text),
    tenants.slice(1).map((tenant) => tenant.name),
  )
  for (const option of options) {
    assert.equal(option.type, 'button')
    assert.equal(option.props.type, 'button')
    assert.match(option.props.class, /dropdown-item dropdown-item-sm py-1/)
  }

  const panel = current.parent.parent
  const divider = findAll(panel, (n) => n.type === 'hr')[0]
  assert.match(divider.props.class, /dropdown-divider/)
  const [manage] = byTestId(root, 'manage-workspaces-button')
  assert.equal(manage.props.href, '/workspaces')
  assert.equal(text(manage), 'All Workspaces')
  const children = panel.children.filter((n) => n.type !== '#comment' && n.type !== '#text')
  assert.equal(children.at(-2), divider)
  assert.ok(findAll(children.at(-1), (n) => n === manage).length === 1)
})

test('selector current workspace uses the same markup as the account menu', async (t) => {
  const selector = await mount(t, selectorSource)
  const menu = await mount(t, menuSource)

  const menuCurrent = findAll(menu.root, (n) => /workspace-current/.test(n.props.class ?? ''))[0]
  const [selectorCurrent] = byTestId(selector.root, 'selector-current-workspace')
  const classes = (n) => n.props.class.split(' ').filter((c) => !/^workspace-/.test(c))
  assert.deepEqual(classes(selectorCurrent), classes(menuCurrent))
  assert.equal(text(selectorCurrent), text(menuCurrent))

  const menuOption = findAll(menu.root, (n) => /dropdown-item-sm/.test(n.props.class ?? ''))[0]
  const [selectorOption] = byTestId(selector.root, 'workspace-option')
  assert.equal(selectorOption.props.class, menuOption.props.class)
})

test('choosing a workspace selects it, emits change, and updates the highlight', async (t) => {
  const changes = []
  const { root, store } = await mount(t, selectorSource, {
    listeners: { onChange: (tenant) => changes.push(tenant.key) },
  })

  const target = byTestId(root, 'workspace-option').find((o) => text(o) === 'Fifth workspace')
  target.props.onClick()
  await nextTick()

  assert.equal(store.getCurrentTenantKey, 'fifth')
  assert.deepEqual(changes, ['fifth'])
  assert.equal(text(byTestId(root, 'selector-current-workspace')[0]), 'Fifth workspace')
  assert.equal(text(byTestId(root, 'current-workspace')[0]), 'Fifth workspace')
  const remaining = byTestId(root, 'workspace-option').map(text)
  assert.ok(!remaining.includes('Fifth workspace'))
  assert.ok(remaining.includes('First workspace'))
})

test('selector shows a no-workspace message when nothing is selected', async (t) => {
  const { root, store } = await mount(t, selectorSource)
  store.clearPreferences()
  await nextTick()

  assert.equal(byTestId(root, 'selector-current-workspace').length, 0)
  assert.equal(text(byTestId(root, 'no-workspace-message')[0]), 'No workspace selected')
  assert.equal(byTestId(root, 'workspace-option').length, tenants.length)
})
