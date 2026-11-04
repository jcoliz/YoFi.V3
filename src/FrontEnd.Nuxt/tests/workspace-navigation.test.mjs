import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { createRenderer, h, nextTick, onMounted, reactive, ref } from 'vue'
import { compileScript, parse } from 'vue/compiler-sfc'
import ts from 'typescript'

const require = createRequire(import.meta.url)
const source = readFileSync(new URL('../app/pages/workspaces.vue', import.meta.url), 'utf8')
const { descriptor } = parse(source, { filename: 'workspaces.vue' })
const script = compileScript(descriptor, { id: 'workspace-navigation', inlineTemplate: true })
const { outputText } = ts.transpileModule(script.content, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
})
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

async function mount(t) {
  const tenants = [
    { key: 'first', name: 'First workspace', role: roles.Owner },
    { key: 'second', name: 'Second workspace', role: roles.Editor },
    { key: 'third', name: 'Third workspace', role: roles.Viewer },
  ]
  const store = reactive({
    currentTenant: tenants[0],
    get getCurrentTenantKey() {
      return this.currentTenant.key
    },
    get hasTenant() {
      return Boolean(this.currentTenant)
    },
    setCurrentTenant(tenant) {
      this.currentTenant = tenant
    },
    loadFromStorage() {},
  })
  const exports = {}
  const globals = {
    ref,
    onMounted,
    definePageMeta() {},
    useApiClient: () => ({ getTenants: async () => tenants }),
  }
  new Function('require', 'exports', ...Object.keys(globals), outputText)(
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
