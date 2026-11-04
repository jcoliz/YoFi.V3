import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { computed, createRenderer, h, nextTick, ref, watch } from 'vue'
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
const deleteModalScript = compileFile('components/workspace/DeleteModal.vue')
const modalDialogScript = compileFile('components/ModalDialog.vue')

function node(type, text = '') {
  return {
    type,
    text,
    props: {},
    children: [],
    parent: undefined,
    addEventListener() {},
    removeEventListener() {},
  }
}

const body = node('body')
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
  querySelector: () => body,
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

class TenantClient {}
class TenantDataAdminClient {}
class TenantDataDeletionRequest {
  constructor(data) {
    Object.assign(this, data)
  }
}

function mount(t, scope) {
  const calls = []
  const store = {
    getCurrentTenantKey: 'second',
    cleared: false,
    clearPreferences() {
      this.cleared = true
    },
  }
  const clients = new Map([
    [TenantClient, { deleteTenant: async (key) => calls.push(['deleteTenant', key]) }],
    [
      TenantDataAdminClient,
      {
        deleteAllData: async (key, request) => {
          calls.push(['deleteAllData', key, request])
          return { deleted: { transactions: 5 } }
        },
      },
    ],
  ])
  const globals = { ref, computed, watch, useApiClient: (type) => clients.get(type) }
  const exports = {}
  new Function('require', 'exports', ...Object.keys(globals), deleteModalScript)(
    (id) => {
      if (id === '~/utils/apiclient')
        return { TenantClient, TenantDataAdminClient, TenantDataDeletionRequest }
      if (id === '~/stores/userPreferences') return { useUserPreferencesStore: () => store }
      if (id === '~/utils/errorHandler') return { handleApiError: (error) => error }
      return require(id)
    },
    exports,
    ...Object.values(globals),
  )

  const emitted = []
  const show = ref(true)
  const root = node('root')
  const app = renderer.createApp({
    setup: () => () =>
      h(exports.default, {
        show: show.value,
        scope,
        workspace: { key: 'second', name: 'Second Workspace' },
        'onUpdate:show': (value) => {
          emitted.push(['update:show', value])
          show.value = value
        },
        onDeleted: (result) => emitted.push(['deleted', result]),
      }),
  })
  const modalExports = {}
  new Function('require', 'exports', 'computed', modalDialogScript)(require, modalExports, computed)
  app.component('ModalDialog', modalExports.default)
  for (const name of ['ErrorDisplay', 'CopyTextButton', 'BaseSpinner']) {
    app.component(name, { render: () => null })
  }
  app.mount(root)
  t.after(() => {
    app.unmount()
    body.children = []
  })
  return { calls, emitted, store }
}

async function typeName(modal, value) {
  const input = find(modal, 'confirmation-name-input')
  input.value = value
  input.props['onUpdate:modelValue'](value)
  await nextTick()
}

test('workspace deletion requires the exact workspace name before deleting', async (t) => {
  const { calls, emitted, store } = mount(t, undefined)
  await nextTick()
  const modal = find(body, 'delete-modal')
  assert.ok(modal)
  assert.equal(find(body, 'delete-data-modal'), undefined)
  const submit = () => find(modal, 'delete-submit-button')
  assert.equal(submit().props.disabled, true)

  for (const wrong of ['second workspace', 'Second Workspace ', 'Second']) {
    await typeName(modal, wrong)
    assert.equal(submit().props.disabled, true, wrong)
  }
  await submit().props.onClick()
  assert.deepEqual(calls, [])

  await typeName(modal, 'Second Workspace')
  assert.equal(submit().props.disabled, false)
  await submit().props.onClick()
  await nextTick()
  assert.deepEqual(calls, [['deleteTenant', 'second']])
  assert.equal(store.cleared, true)
  assert.deepEqual(emitted, [
    ['update:show', false],
    ['deleted', undefined],
  ])
})

test('data deletion sends the typed confirmation name and reports the result', async (t) => {
  const { calls, emitted, store } = mount(t, 'data')
  await nextTick()
  const modal = find(body, 'delete-data-modal')
  assert.ok(modal)
  assert.equal(find(body, 'delete-modal'), undefined)
  assert.equal(find(modal, 'delete-submit-button').props.disabled, true)

  await typeName(modal, 'Second Workspace')
  await find(modal, 'delete-submit-button').props.onClick()
  await nextTick()
  assert.equal(calls.length, 1)
  const [method, key, request] = calls[0]
  assert.equal(method, 'deleteAllData')
  assert.equal(key, 'second')
  assert.equal(request.confirmationWorkspaceName, 'Second Workspace')
  assert.equal(store.cleared, false)
  assert.deepEqual(emitted, [
    ['update:show', false],
    ['deleted', { deleted: { transactions: 5 } }],
  ])
})
