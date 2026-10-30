import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { compileScript, parse } from 'vue/compiler-sfc'
import { computed, createRenderer, h, nextTick, ref, watch } from 'vue'
import ts from 'typescript'

const require = createRequire(import.meta.url)

// Compile the actual SFCs, with injectable imports and Nuxt auto-imports for component tests.
function loadComponent(name, useProblemDetails, { imports = {}, globals = {} } = {}) {
  const filename = `${name}.vue`
  const source = readFileSync(new URL(`../app/components/${filename}`, import.meta.url), 'utf8')
  const { descriptor } = parse(source, { filename })
  const script = compileScript(descriptor, { id: name, inlineTemplate: true })
  const { outputText } = ts.transpileModule(script.content, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  })
  const exports = {}
  new Function('require', 'exports', 'useProblemDetails', ...Object.keys(globals), outputText)(
    (id) => imports[id] ?? require(id),
    exports,
    useProblemDetails,
    ...Object.values(globals),
  )
  return exports.default
}

// Vue's in-memory renderer exercises reactivity, events, and attribute fallthrough without a DOM.
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

const ErrorDisplay = loadComponent('ErrorDisplay')

function mount(t, render, components = {}) {
  const root = node('root')
  const app = renderer.createApp({ render })
  app.component('ErrorDisplay', ErrorDisplay)
  app.component('FeatherIcon', { render: () => h('i') })
  app.component('BaseSpinner', { render: () => h('span') })
  for (const [name, component] of Object.entries(components)) {
    app.component(name, component)
  }
  app.mount(root)
  t.after(() => app.unmount())
  return root
}

function find(root, testId) {
  if (root.props['data-test-id'] === testId) return root
  return root.children.map((child) => find(child, testId)).find(Boolean)
}

function text(root) {
  if (root.type === '#comment') return ''
  return root.text + root.children.map(text).join('')
}

async function click(root, testId) {
  const target = find(root, testId)
  assert.ok(target, `Missing ${testId}`)
  await target.props.onClick({ preventDefault() {} })
  await nextTick()
}

test('validation messages stay inline while diagnostics are collapsed', async (t) => {
  const root = mount(t, () =>
    h(ErrorDisplay, {
      show: true,
      problem: {
        title: 'Validation failed',
        detail: 'Check your input.',
        status: 400,
        errors: {
          Name: ['Name is required.', 'Name is too short.'],
          Amount: 'Amount must be positive.',
          '': ['Please check the form.'],
        },
        traceId: 'trace-123',
      },
    }),
  )

  assert.equal(text(find(root, 'title-display')), 'Validation failed')
  assert.match(text(find(root, 'detail-display')), /Check your input\./)
  const validation = find(root, 'validation-errors')
  assert.ok(validation)
  assert.match(text(validation), /Name:.*Name is required\./s)
  assert.match(text(validation), /Name:.*Name is too short\./s)
  assert.match(text(validation), /Amount:.*Amount must be positive\./s)
  assert.match(text(validation), /Please check the form\./)
  assert.doesNotMatch(text(validation), /:\s*Please check the form\./)
  assert.equal(find(root, 'more-text'), undefined)

  await click(root, 'more-button')
  const diagnostics = text(find(root, 'more-text'))
  assert.match(diagnostics, /traceId:.*trace-123/s)
  assert.doesNotMatch(diagnostics, /Name is required|errors:/)
  await click(root, 'more-button')
  assert.equal(find(root, 'more-text'), undefined)
  assert.ok(find(root, 'validation-errors'))
})

test('empty validation collections do not create an empty list or diagnostic toggle', async (t) => {
  const problem = ref({ errors: {} })
  const root = mount(t, () => h(ErrorDisplay, { show: true, problem: problem.value }))
  assert.equal(find(root, 'validation-errors'), undefined)
  assert.equal(find(root, 'more-button'), undefined)
  assert.equal(text(find(root, 'title-display')), 'Please fix the following errors:')

  problem.value = { errors: { Name: [] } }
  await nextTick()
  assert.equal(find(root, 'validation-errors'), undefined)
  assert.equal(find(root, 'more-button'), undefined)
})

test('server diagnostics auto-expand and reset for a non-server problem', async (t) => {
  const problem = ref({ status: 500, traceId: 'server-trace', context: { operation: 'save' } })
  const root = mount(t, () => h(ErrorDisplay, { show: true, problem: problem.value }))
  assert.match(text(find(root, 'more-text')), /Please contact support immediately/)
  assert.match(text(find(root, 'more-text')), /server-trace/)
  assert.match(text(find(root, 'more-text')), /\{"operation":"save"\}/)

  await click(root, 'more-button')
  assert.equal(find(root, 'more-text'), undefined)
  problem.value = { status: 503, traceId: 'new-trace' }
  await nextTick()
  assert.match(text(find(root, 'more-text')), /new-trace/)

  problem.value = { status: 400, traceId: 'client-trace' }
  await nextTick()
  assert.equal(find(root, 'more-text'), undefined)
})

test('global adapter follows shared state, retains its selector, and clears on dismissal', async (t) => {
  const problem = ref()
  let clearCount = 0
  const Viewer = loadComponent('ProblemDetailsViewer', () => ({
    problem,
    clearError() {
      clearCount++
      problem.value = undefined
    },
  }))
  const root = mount(t, () => h(Viewer))
  assert.equal(find(root, 'problem-details-viewer'), undefined)

  problem.value = { title: 'Global failure', errors: { Name: ['Required.'] } }
  await nextTick()
  assert.equal(text(find(root, 'title-display')), 'Global failure')
  assert.ok(find(root, 'problem-details-viewer'))
  assert.equal(find(root, 'error-display'), undefined)
  assert.match(text(find(root, 'validation-errors')), /Required\./)

  problem.value = { title: 'Updated failure' }
  await nextTick()
  assert.equal(text(find(root, 'title-display')), 'Updated failure')
  await click(root, 'close-button')
  assert.equal(clearCount, 1)
  assert.equal(problem.value, undefined)
  assert.equal(find(root, 'problem-details-viewer'), undefined)
})

test('local dismissal hides the display without changing local or global problems', async (t) => {
  const globalProblem = ref({ title: 'Global failure' })
  const Viewer = loadComponent('ProblemDetailsViewer', () => ({
    problem: globalProblem,
    clearError() {
      globalProblem.value = undefined
    },
  }))
  const localProblem = { title: 'Local failure' }
  const show = ref(true)
  const root = mount(t, () =>
    h('section', [
      h(Viewer),
      h(ErrorDisplay, {
        show: show.value,
        problem: localProblem,
        'onUpdate:show': (value) => (show.value = value),
      }),
    ]),
  )

  await click(find(root, 'error-display'), 'close-button')
  assert.equal(show.value, false)
  assert.equal(localProblem.title, 'Local failure')
  assert.equal(globalProblem.value.title, 'Global failure')
  assert.equal(find(root, 'error-display'), undefined)
  assert.ok(find(root, 'problem-details-viewer'))
})

function mountTransactionModal(t, { transaction = {}, save = async () => {} } = {}) {
  const calls = []
  const events = []
  const show = ref(true)
  const Modal = loadComponent('TransactionEditModal', undefined, {
    imports: {
      '~/utils/apiclient': {
        TransactionsClient: class {},
        TransactionQuickEditDto: class {
          constructor(data) {
            Object.assign(this, data)
          }
        },
      },
      '~/stores/userPreferences': {
        useUserPreferencesStore: () => ({ getCurrentTenantKey: 'workspace-key' }),
      },
      '~/utils/errorHandler': {
        handleApiError: () => ({ title: 'Update Failed', detail: 'The request failed.' }),
      },
      // Native input events are outside the in-memory renderer; drive the compiled model handlers.
      vue: { ...require('vue'), vModelText: {} },
    },
    globals: {
      ref,
      computed,
      watch,
      useApiClient: () => ({
        async quickEditTransaction(...args) {
          calls.push(args)
          await save(...args)
        },
      }),
    },
  })
  const root = mount(
    t,
    () =>
      h(Modal, {
        show: show.value,
        transaction: { key: 'transaction-key', payee: 'Original payee', ...transaction },
        'onUpdate:show': (value) => events.push(['show', value]),
        onUpdated: () => events.push(['updated']),
      }),
    {
      ModalDialog: {
        setup(_props, { slots }) {
          return () => h('section', [slots.default?.(), slots.footer?.()])
        },
      },
    },
  )
  return { root, calls, events, show }
}

async function setField(root, field, value) {
  find(root, `edit-transaction-${field}`).props['onUpdate:modelValue'](value)
  await nextTick()
}

function isInvalid(root, field) {
  return find(root, `edit-transaction-${field}`).props.class.includes('is-invalid')
}

test('quick edit shares validation messages between summary and fields without calling the API', async (t) => {
  const { root, calls } = mountTransactionModal(t, {
    transaction: { payee: '   ', category: 'c'.repeat(101), memo: 'm'.repeat(1001) },
  })
  await click(root, 'edit-submit-button')
  assert.equal(calls.length, 0)
  assert.equal(text(find(root, 'title-display')), 'Please fix the following errors:')
  const summary = text(find(root, 'validation-errors'))
  for (const [field, message] of [
    ['payee', 'Payee is required'],
    ['category', 'Category cannot exceed 100 characters'],
    ['memo', 'Memo cannot exceed 1000 characters'],
  ]) {
    assert.ok(isInvalid(root, field))
    assert.ok(summary.includes(message))
    const input = find(root, `edit-transaction-${field}`)
    const feedback = input.parent.children.find((child) =>
      child.props.class?.includes('invalid-feedback'),
    )
    assert.equal(text(feedback).trim(), message)
  }

  await click(root, 'close-button')
  assert.equal(find(root, 'error-display'), undefined)
  assert.ok(isInvalid(root, 'payee'))
  await click(root, 'edit-submit-button')
  assert.ok(find(root, 'validation-errors'))
  assert.equal(calls.length, 0)
})

test('quick edit clears a stale API failure before showing validation and submits corrected boundary values', async (t) => {
  let shouldFail = true
  const { root, calls, events } = mountTransactionModal(t, {
    save: async () => {
      if (shouldFail) throw new Error('Request failed')
    },
  })
  await click(root, 'edit-submit-button')
  assert.equal(text(find(root, 'title-display')), 'Update Failed')
  assert.equal(calls.length, 1)

  await setField(root, 'payee', '')
  await click(root, 'edit-submit-button')
  assert.equal(text(find(root, 'title-display')), 'Please fix the following errors:')
  assert.equal(find(root, 'detail-display'), undefined)
  assert.equal(calls.length, 1)

  shouldFail = false
  await setField(root, 'payee', '  Corrected payee  ')
  await setField(root, 'category', 'c'.repeat(100))
  await setField(root, 'memo', 'm'.repeat(1000))
  await click(root, 'edit-submit-button')
  assert.equal(calls.length, 2)
  const [key, tenant, dto] = calls[1]
  assert.equal(key, 'transaction-key')
  assert.equal(tenant, 'workspace-key')
  assert.equal(dto.payee, 'Corrected payee')
  assert.equal(dto.category.length, 100)
  assert.equal(dto.memo.length, 1000)
  assert.equal(find(root, 'error-display'), undefined)
  assert.ok(!isInvalid(root, 'payee'))
  assert.deepEqual(events, [['show', false], ['updated']])
})

test('reopening quick edit resets validation and summary', async (t) => {
  const { root, show } = mountTransactionModal(t, { transaction: { payee: '' } })
  await click(root, 'edit-submit-button')
  assert.ok(isInvalid(root, 'payee'))
  show.value = false
  await nextTick()
  show.value = true
  await nextTick()
  assert.equal(find(root, 'error-display'), undefined)
  assert.ok(!isInvalid(root, 'payee'))
})

test('quick edit ignores duplicate submissions while loading and preserves optional fields', async (t) => {
  let finish
  const pending = new Promise((resolve) => (finish = resolve))
  const { root, calls } = mountTransactionModal(t, {
    transaction: { memo: '  ', category: '' },
    save: () => pending,
  })
  const button = find(root, 'edit-submit-button')
  const submission = button.props.onClick()
  try {
    await nextTick()
    assert.equal(button.props.disabled, true)
    await button.props.onClick()
    assert.equal(calls.length, 1)
    assert.equal(calls[0][2].memo, undefined)
    assert.equal(calls[0][2].category, undefined)
  } finally {
    finish()
    await submission
  }
})
