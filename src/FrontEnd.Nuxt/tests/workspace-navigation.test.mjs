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
const pageScript = compileFile('pages/workspaces/index.vue')
const manageScript = compileFile('pages/workspaces/[key].vue')
const selectorScript = compileFile('components/workspace/Selector.vue')
const copyScript = compileFile('components/workspace/CopyKeyButton.vue')
const cardTitleScript = compileFile('components/CardTitle.vue')
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

async function mount(
  t,
  {
    selector = false,
    manage = false,
    getTenant,
    authStatus = 'authenticated',
    claims = ['second:Owner', 'third:Owner', 'missing:Owner'].map((value) => ({
      type: 'tenant_role',
      value,
    })),
    clipboard = { writeText: async () => {} },
  } = {},
) {
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
  const route = reactive({ params: { key: 'second' } })
  const guards = {}
  const panes = {}
  const auth = { status: ref(authStatus), data: ref({ claims }) }
  const requests = []
  let destination
  const globals = {
    ref,
    computed,
    watch,
    onMounted,
    onBeforeUnmount,
    onBeforeRouteLeave: (guard) => (guards.leave = guard),
    onBeforeRouteUpdate: (guard) => (guards.update = guard),
    useRoute: () => route,
    useAuth: () => auth,
    useAuthenticationGuard: () => ({
      requireAuthenticatedUser(action) {
        onMounted(() => {
          if (auth.status.value === 'authenticated') action()
        })
        watch(auth.status, (status) => {
          if (status === 'authenticated') action()
        })
      },
    }),
    useRouter: () => ({
      push: async (path) => {
        if (guards.leave?.() !== false) destination = { path }
      },
    }),
    definePageMeta() {},
    useApiClient: () => ({
      getTenants: async () => tenants,
      getTenant: (key) => {
        requests.push(key)
        return getTenant ? getTenant(key) : Promise.resolve(tenants.find((tenant) => tenant.key === key))
      },
    }),
  }
  new Function(
    'require',
    'exports',
    ...Object.keys(globals),
    selector ? selectorScript : manage ? manageScript : pageScript,
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
  const cardTitleExports = {}
  new Function('require', 'exports', cardTitleScript)(require, cardTitleExports)
  app.component('CardTitle', cardTitleExports.default)
  app.component('BaseAlert', {
    setup(_props, { attrs, slots }) {
      return () => h('div', attrs, [slots.default?.(), slots.detail?.()])
    },
  })
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
    props: ['workspace'],
    emits: ['cancel', 'updated', 'update:busy'],
    setup(props, { emit }) {
      panes.edit = { props, emit }
      return () =>
        h('button', { 'data-test-id': 'cancel-edit', onClick: () => emit('cancel') }, 'Cancel')
    },
  })
  app.component('WorkspaceDeleteModal', {
    props: ['workspace', 'show'],
    emits: ['deleted', 'update:show', 'update:busy'],
    setup(props, { emit }) {
      panes.delete = { props, emit }
      return () => null
    },
  })
  let problem
  app.component('ErrorDisplay', {
    props: ['problem', 'show'],
    setup(props) {
      return () => {
        problem = props.show ? props.problem : undefined
        return null
      }
    },
  })
  for (const name of [
    'PageBreadcrumb',
    'WorkspaceSelector',
    'WorkspaceCreatePane',
    'BaseSpinner',
    'FeatherIcon',
  ]) {
    app.component(name, { render: () => null })
  }
  app.mount(root)
  t.after(() => app.unmount())
  await nextTick()
  await nextTick()
  return {
    root,
    auth,
    requests,
    store,
    route,
    guards,
    panes,
    problem: () => problem,
    destination: () => destination,
  }
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

test('management loads the route workspace, supports editing, and guards pending writes', async (t) => {
  const { root, store, route, panes, guards } = await mount(t, { manage: true })
  assert.equal(find(root, 'page-heading').text, 'Second workspace')
  assert.equal(store.getCurrentTenantKey, 'second')
  await find(root, 'edit-workspace-button').props.onClick()
  await nextTick()
  assert.equal(panes.edit.props.workspace.key, 'second')
  panes.edit.emit('update:busy', true)
  await nextTick()
  assert.equal(guards.leave(), false)
  assert.equal(guards.update(), false)
  assert.equal(find(root, 'delete-workspace-button').props.disabled, true)
  panes.edit.emit('update:busy', false)
  panes.edit.emit('updated')
  await nextTick()
  await nextTick()
  assert.equal(find(root, 'cancel-edit'), undefined)
  assert.equal(guards.leave(), true)
  route.params.key = 'third'
  await nextTick()
  await nextTick()
  assert.equal(find(root, 'page-heading').text, 'Third workspace')
  assert.equal(store.getCurrentTenantKey, 'third')
})

test('deletion targets the route workspace and returns to the list after completion', async (t) => {
  const { root, panes, destination, guards } = await mount(t, { manage: true })
  await find(root, 'delete-workspace-button').props.onClick()
  await nextTick()
  assert.equal(panes.delete.props.show, true)
  assert.equal(panes.delete.props.workspace.key, 'second')
  panes.delete.emit('update:busy', true)
  assert.equal(guards.leave(), false)
  panes.delete.emit('deleted')
  await nextTick()
  assert.deepEqual(destination(), { path: '/workspaces' })
})

test('an unavailable route clears previous settings and reports the error without fallback', async (t) => {
  const { root, route, store, problem } = await mount(t, {
    manage: true,
    getTenant: async (key) => {
      if (key === 'missing') throw new Error('Workspace unavailable')
      return { key, name: 'Route workspace' }
    },
  })
  route.params.key = 'missing'
  await nextTick()
  await nextTick()
  assert.equal(find(root, 'page-heading'), undefined)
  assert.equal(find(root, 'edit-workspace-button'), undefined)
  assert.equal(find(root, 'delete-workspace-button'), undefined)
  assert.match(problem().message, /Workspace unavailable/)
  assert.equal(store.getCurrentTenantKey, 'second')
})

test('late responses cannot replace a newer route workspace', async (t) => {
  let finishFirstLoad
  const { root, route, store } = await mount(t, {
    manage: true,
    getTenant: (key) =>
      key === 'second'
        ? new Promise((resolve) => (finishFirstLoad = resolve))
        : Promise.resolve({ key, name: 'New route workspace' }),
  })
  route.params.key = 'third'
  await nextTick()
  await nextTick()
  finishFirstLoad({ key: 'second', name: 'Old route workspace' })
  await nextTick()
  await nextTick()
  assert.equal(find(root, 'page-heading').text, 'New route workspace')
  assert.equal(store.getCurrentTenantKey, 'third')
})

test('management waits for authentication before checking claims or fetching workspace details', async (t) => {
  const { root, auth, requests, store } = await mount(t, {
    manage: true,
    authStatus: 'loading',
  })
  assert.deepEqual(requests, [])
  assert.ok(find(root, 'loading-state'))
  assert.equal(find(root, 'access-denied'), undefined)
  assert.equal(find(root, 'edit-workspace-button'), undefined)
  assert.equal(store.getCurrentTenantKey, 'first')
  auth.status.value = 'authenticated'
  await nextTick()
  await nextTick()
  assert.deepEqual(requests, ['second'])
  assert.equal(find(root, 'page-heading').text, 'Second workspace')
})

for (const claims of [
  [],
  [{ type: 'tenant_role', value: 'second:Editor' }],
  [{ type: 'tenant_role', value: 'second:Viewer' }],
  [{ type: 'tenant_role', value: 'first:Owner' }],
  [{ type: 'other', value: 'second:Owner' }],
  [{ type: 'tenant_role', value: 'second:Owner:extra' }],
]) {
  test(`management denies nonmatching claims ${JSON.stringify(claims)} without fetching`, async (t) => {
    const { root, requests, store } = await mount(t, { manage: true, claims })
    assert.deepEqual(requests, [])
    assert.ok(find(root, 'access-denied'))
    assert.equal(find(root, 'loading-state'), undefined)
    assert.equal(find(root, 'page-heading'), undefined)
    assert.equal(find(root, 'edit-workspace-button'), undefined)
    assert.equal(find(root, 'delete-workspace-button'), undefined)
    assert.equal(store.getCurrentTenantKey, 'first')
  })
}

test('route and claim changes revoke access and invalidate outstanding workspace loads', async (t) => {
  let finishLoad
  const { root, auth, route, requests, store } = await mount(t, {
    manage: true,
    getTenant: () => new Promise((resolve) => (finishLoad = resolve)),
  })
  route.params.key = 'first'
  await nextTick()
  assert.ok(find(root, 'access-denied'))
  assert.deepEqual(requests, ['second'])
  finishLoad({ key: 'second', name: 'Late workspace' })
  await nextTick()
  await nextTick()
  assert.equal(find(root, 'page-heading'), undefined)
  assert.equal(store.getCurrentTenantKey, 'first')
  auth.data.value.claims = [{ type: 'tenant_role', value: 'first:Owner' }]
  await nextTick()
  assert.deepEqual(requests, ['second', 'first'])
  finishLoad({ key: 'first', name: 'Allowed workspace' })
  await nextTick()
  await nextTick()
  assert.ok(find(root, 'edit-workspace-button'))
  auth.data.value.claims = []
  await nextTick()
  assert.ok(find(root, 'access-denied'))
  assert.equal(find(root, 'edit-workspace-button'), undefined)
  assert.equal(find(root, 'page-heading'), undefined)
})

test('authentication revalidation hides loaded workspace controls until authentication resolves', async (t) => {
  const { root, auth } = await mount(t, { manage: true })
  auth.status.value = 'loading'
  await nextTick()
  assert.ok(find(root, 'loading-state'))
  assert.equal(find(root, 'access-denied'), undefined)
  assert.equal(find(root, 'edit-workspace-button'), undefined)
  auth.status.value = 'authenticated'
  await nextTick()
  await nextTick()
  assert.ok(find(root, 'edit-workspace-button'))
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
