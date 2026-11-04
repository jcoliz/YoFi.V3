import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import {
  computed,
  createRenderer,
  h,
  nextTick,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch,
} from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)
function compileFile(path) {
  const source = readFileSync(new URL(`../app/${path}`, import.meta.url), 'utf8')
  const { descriptor } = parse(source, { filename: path })
  const script = compileScript(descriptor, { id: path, inlineTemplate: true })
  return ts.transpileModule(script.content, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText
}
const pageScript = compileFile('pages/workspaces.vue')
const selectorScript = compileFile('components/workspace/Selector.vue')
const copyScript = compileFile('components/workspace/CopyKeyButton.vue')
const roles = { Owner: 1, Editor: 2, Viewer: 3 }

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

function find(root, testId) {
  if (root.props['data-test-id'] === testId) return root
  return root.children.map((child) => find(child, testId)).find(Boolean)
}

async function mount(t, { selector = false, clipboard = { writeText: async () => {} } } = {}) {
  const tenants = [
    { key: 'first', name: 'First workspace', role: roles.Owner },
    { key: 'second', name: 'Second workspace', role: roles.Editor },
    { key: 'third', name: 'Third workspace', role: roles.Viewer },
  ]
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
    loadFromStorage() {},
    clearPreferences() {
      this.currentTenant = undefined
    },
  })
  const exports = {}
  const globals = {
    ref,
    computed,
    onMounted,
    definePageMeta() {},
    useApiClient: () => ({ getTenants: async () => tenants }),
  }
  new Function(
    'require',
    'exports',
    ...Object.keys(globals),
    selector ? selectorScript : pageScript,
  )(
    (id) => {
      if (id === '~/stores/userPreferences') return { useUserPreferencesStore: () => store }
      if (id === '~/utils/apiclient') return { TenantRole: roles, TenantClient: class {} }
      if (id === '~/utils/errorHandler') return { handleApiError: (error) => error }
      return require(id)
    },
    exports,
    ...Object.values(globals),
  )

  const root = node('root')
  const app = renderer.createApp(exports.default)
  const copyExports = {}
  const copyGlobals = { ref, watch, onBeforeUnmount, navigator: { clipboard } }
  new Function('require', 'exports', ...Object.keys(copyGlobals), copyScript)(
    require,
    copyExports,
    ...Object.values(copyGlobals),
  )
  app.component('WorkspaceCopyKeyButton', copyExports.default)
  app.component('DropDownPortable', {
    setup(_props, { slots }) {
      return () => h('div', [slots.trigger(), slots.default()])
    },
  })
  let destination
  app.component('NuxtLink', {
    props: ['to'],
    emits: ['click'],
    setup(props, { attrs, slots, emit }) {
      return () =>
        h(
          'a',
          {
            ...attrs,
            href: props.to,
            onClick(event) {
              emit('click', event)
              destination = { path: props.to, workspace: store.getCurrentTenantKey }
            },
          },
          slots.default(),
        )
    },
  })
  app.component('WorkspaceEditPane', {
    emits: ['cancel', 'updated'],
    setup(_props, { emit }) {
      return () =>
        h('button', { 'data-test-id': 'cancel-edit', onClick: () => emit('cancel') }, 'Cancel')
    },
  })
  for (const name of [
    'WorkspaceSelector',
    'WorkspaceCreatePane',
    'WorkspaceDeleteModal',
    'ErrorDisplay',
    'BaseSpinner',
    'FeatherIcon',
  ]) {
    app.component(name, { render: () => null })
  }
  app.mount(root)
  t.after(() => app.unmount())
  await nextTick()
  await nextTick()
  return { root, store, destination: () => destination }
}

test('opening any role selects its workspace before navigating, including the current workspace', async (t) => {
  const { root, store, destination } = await mount(t)
  for (const key of ['first', 'second', 'third', 'third']) {
    const card = find(root, `workspace-card-${key}`)
    const link = find(card, 'open-workspace-link')
    assert.equal(link.type, 'a')
    assert.equal(link.props.href, '/transactions')
    assert.match(link.props.class, /stretched-link/)
    assert.match(link.props.class, /btn btn-sm btn-outline-primary/)
    assert.ok(link.children.some((child) => child.text.trim() === 'Open'))
    assert.match(link.props['aria-label'], /^Open workspace /)
    await link.props.onClick({})
    await nextTick()
    assert.equal(store.getCurrentTenantKey, key)
    assert.deepEqual(destination(), { path: '/transactions', workspace: key })
    assert.ok(find(card, 'current-workspace-badge'))
    assert.match(card.props.class, /border-primary/)
    if (key !== 'first') {
      assert.equal(find(card, 'edit-workspace-button'), undefined)
      assert.equal(find(card, 'delete-workspace-button'), undefined)
    }
  }
})

test('editing removes card navigation until cancelled and management actions do not select a workspace', async (t) => {
  const { root, store, destination } = await mount(t)
  await find(find(root, 'workspace-card-second'), 'open-workspace-link').props.onClick({})
  await nextTick()
  const previousDestination = destination()
  const card = find(root, 'workspace-card-first')
  for (const action of ['delete-workspace-button', 'edit-workspace-button']) {
    const button = find(card, action)
    assert.match(button.props.class, /position-relative z-2/)
    await button.props.onClick({})
    await nextTick()
    assert.equal(store.getCurrentTenantKey, 'second')
    assert.deepEqual(destination(), previousDestination)
  }
  assert.equal(find(card, 'open-workspace-link'), undefined)
  assert.doesNotMatch(card.props.class, /workspace-card-navigable/)
  await find(card, 'cancel-edit').props.onClick({})
  await nextTick()
  assert.ok(find(card, 'open-workspace-link'))
  assert.match(card.props.class, /workspace-card-navigable/)
})

async function copy(root) {
  let stopped = false
  await find(root, 'copy-workspace-key-button').props.onClick({
    stopPropagation() {
      stopped = true
    },
  })
  await nextTick()
  assert.ok(stopped, 'Copy must not bubble to navigation or close the dropdown')
}

test('only the copy control sits above the card navigation overlay and copying does not open or select', async (t) => {
  const keys = []
  const { root, store, destination } = await mount(t, {
    clipboard: { writeText: async (key) => keys.push(key) },
  })
  const card = find(root, 'workspace-card-second')
  const key = find(card, 'workspace-key')
  assert.doesNotMatch(key.parent.parent.props.class, /position-relative|z-2/)
  const button = find(card, 'copy-workspace-key-button')
  assert.match(button.parent.props.class, /position-relative z-2/)
  assert.match(button.props.class, /btn-link/)
  assert.doesNotMatch(button.props.class, /btn-outline/)
  assert.equal(key.text, 'second')
  await copy(card)
  assert.deepEqual(keys, ['second'])
  assert.equal(store.getCurrentTenantKey, 'first')
  assert.equal(destination(), undefined)
  assert.equal(find(card, 'copy-workspace-key-status').text, 'Copied!')
})

test('selector menu does not expose workspace keys or copy controls', async (t) => {
  const { root, store } = await mount(t, { selector: true })
  assert.equal(find(root, 'copy-workspace-key-button'), undefined)
  const hasCode = (node) => node.type === 'code' || node.children.some(hasCode)
  assert.equal(hasCode(root), false)
  store.setCurrentTenant({ key: 'second', name: 'Second workspace' })
  await nextTick()
  assert.equal(find(root, 'copy-workspace-key-button'), undefined)
  store.clearPreferences()
  await nextTick()
  assert.equal(find(root, 'copy-workspace-key-button'), undefined)
})

test('copy confirmation disappears after two seconds', async (t) => {
  const { root } = await mount(t)
  t.mock.timers.enable({ apis: ['setTimeout'] })
  await copy(root)
  assert.equal(find(root, 'copy-workspace-key-status').text, 'Copied!')
  t.mock.timers.tick(2000)
  await nextTick()
  assert.equal(find(root, 'copy-workspace-key-status').text, '')
})

for (const failure of ['denied', 'unavailable']) {
  test(`clipboard ${failure} shows an explicit error and allows retry`, async (t) => {
    const clipboard = {}
    if (failure === 'denied') {
      clipboard.writeText = async () => {
        throw new Error('Permission denied')
      }
    }
    const log = t.mock.method(console, 'error', () => {})
    const { root } = await mount(t, { clipboard })
    await copy(root)
    assert.match(
      find(root, 'copy-workspace-key-error').text,
      /Check clipboard permissions and try again/,
    )
    assert.equal(find(root, 'copy-workspace-key-status').text, '')
    assert.equal(log.mock.callCount(), 1)
    assert.equal(find(root, 'copy-workspace-key-button').props.disabled, false)
    clipboard.writeText = async () => {}
    await copy(root)
    assert.equal(find(root, 'copy-workspace-key-error'), undefined)
    assert.equal(find(root, 'copy-workspace-key-status').text, 'Copied!')
  })
}
