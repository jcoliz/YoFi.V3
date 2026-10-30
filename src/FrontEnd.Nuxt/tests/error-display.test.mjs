import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { test } from 'node:test'
import { compileScript, parse } from 'vue/compiler-sfc'
import { createRenderer, h, nextTick, ref } from 'vue'
import ts from 'typescript'

const require = createRequire(import.meta.url)

// Compile the actual SFCs; provide only the Nuxt auto-import used by the adapter.
function loadComponent(name, useProblemDetails) {
  const filename = `${name}.vue`
  const source = readFileSync(new URL(`../app/components/${filename}`, import.meta.url), 'utf8')
  const { descriptor } = parse(source, { filename })
  const script = compileScript(descriptor, { id: name, inlineTemplate: true })
  const { outputText } = ts.transpileModule(script.content, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  })
  const exports = {}
  new Function('require', 'exports', 'useProblemDetails', outputText)(
    require,
    exports,
    useProblemDetails,
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

function mount(t, render) {
  const root = node('root')
  const app = renderer.createApp({ render })
  app.component('ErrorDisplay', ErrorDisplay)
  app.component('FeatherIcon', { render: () => h('i') })
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
  target.props.onClick({ preventDefault() {} })
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
