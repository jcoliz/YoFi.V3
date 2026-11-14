/**
 * Tests for ErrorDisplay and ProblemDetailsViewer components.
 *
 * ErrorDisplay: pure Vue component (no Nuxt composables), tests behavioral
 * contracts — validation message rendering, diagnostic expansion, dismissal.
 *
 * ProblemDetailsViewer: thin adapter over ErrorDisplay that reads from the
 * shared useProblemDetails composable. Tests that it wires the composable
 * correctly.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, nextTick } from 'vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import ProblemDetailsViewer from '~/components/ProblemDetailsViewer.vue'

// Stub child components that are not under test
const FeatherIcon = { template: '<i />' }

// useProblemDetails is a Nuxt auto-import (no explicit import in the component).
// Stub it as a global so ProblemDetailsViewer can call it.
const useProblemDetailsMock = vi.fn()
vi.stubGlobal('useProblemDetails', useProblemDetailsMock)

// ---------------------------------------------------------------------------
// ErrorDisplay
// ---------------------------------------------------------------------------

describe('ErrorDisplay', () => {
  it('renders title and detail when shown', () => {
    // Given: a problem with title and detail
    const wrapper = mount(ErrorDisplay, {
      props: {
        show: true,
        problem: { title: 'Validation failed', detail: 'Check your input.', status: 400 },
      },
      global: { stubs: { FeatherIcon } },
    })

    // Then: title and detail are visible
    expect(wrapper.find('[data-test-id="title-display"]').text()).toBe('Validation failed')
    expect(wrapper.find('[data-test-id="detail-display"]').text()).toContain('Check your input.')
  })

  it('shows default title when problem has no title', () => {
    // Given: a problem with no title
    const wrapper = mount(ErrorDisplay, {
      props: { show: true, problem: { errors: {} } },
      global: { stubs: { FeatherIcon } },
    })

    // Then: default title is shown
    expect(wrapper.find('[data-test-id="title-display"]').text()).toBe(
      'Please fix the following errors:',
    )
  })

  it('is hidden when show is false', () => {
    // Given: show is false
    const wrapper = mount(ErrorDisplay, {
      props: { show: false, problem: { title: 'Error' } },
      global: { stubs: { FeatherIcon } },
    })

    // Then: the error display is not rendered
    expect(wrapper.find('[data-test-id="error-display"]').exists()).toBe(false)
  })

  it('renders validation errors inline', () => {
    // Given: a problem with validation errors
    const wrapper = mount(ErrorDisplay, {
      props: {
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
        },
      },
      global: { stubs: { FeatherIcon } },
    })

    // Then: validation errors are shown
    const validationEl = wrapper.find('[data-test-id="validation-errors"]')
    expect(validationEl.exists()).toBe(true)
    const text = validationEl.text()
    expect(text).toContain('Name is required.')
    expect(text).toContain('Name is too short.')
    expect(text).toContain('Amount must be positive.')
    expect(text).toContain('Please check the form.')

    // And: field-less errors have no label prefix
    expect(text).not.toMatch(/:\s*Please check the form\./)
  })

  it('hides validation section when errors object is empty', () => {
    // Given: a problem with an empty errors object
    const wrapper = mount(ErrorDisplay, {
      props: { show: true, problem: { errors: {} } },
      global: { stubs: { FeatherIcon } },
    })

    // Then: no validation errors section
    expect(wrapper.find('[data-test-id="validation-errors"]').exists()).toBe(false)
  })

  it('hides validation section when error arrays are empty', async () => {
    // Given: a problem with errors containing empty arrays
    const wrapper = mount(ErrorDisplay, {
      props: { show: true, problem: { errors: { Name: [] } } },
      global: { stubs: { FeatherIcon } },
    })

    // Then: no validation errors section
    expect(wrapper.find('[data-test-id="validation-errors"]').exists()).toBe(false)
  })

  it('toggles diagnostic details on more-button click', async () => {
    // Given: a problem with a traceId (additional field)
    const wrapper = mount(ErrorDisplay, {
      props: {
        show: true,
        problem: {
          title: 'Validation failed',
          detail: 'Check your input.',
          status: 400,
          errors: { Name: ['Name is required.'] },
          traceId: 'trace-123',
        },
      },
      global: { stubs: { FeatherIcon } },
    })

    // Then: more-text is initially hidden
    expect(wrapper.find('[data-test-id="more-text"]').exists()).toBe(false)

    // When: more-button is clicked
    await wrapper.find('[data-test-id="more-button"]').trigger('click')

    // Then: diagnostics are shown with traceId but not validation errors
    const moreText = wrapper.find('[data-test-id="more-text"]').text()
    expect(moreText).toContain('trace-123')
    expect(moreText).not.toContain('Name is required')

    // When: more-button is clicked again
    await wrapper.find('[data-test-id="more-button"]').trigger('click')

    // Then: diagnostics are hidden again
    expect(wrapper.find('[data-test-id="more-text"]').exists()).toBe(false)
  })

  it('auto-expands diagnostics for server errors (5xx)', async () => {
    // Given: a 500 server error with traceId
    const wrapper = mount(ErrorDisplay, {
      props: {
        show: true,
        problem: { status: 500, traceId: 'server-trace', context: { operation: 'save' } },
      },
      global: { stubs: { FeatherIcon } },
    })

    // Then: diagnostics are auto-expanded
    const moreText = wrapper.find('[data-test-id="more-text"]')
    expect(moreText.exists()).toBe(true)
    expect(moreText.text()).toContain('Please contact support immediately')
    expect(moreText.text()).toContain('server-trace')
    expect(moreText.text()).toContain('"operation":"save"')
  })

  it('resets expanded state when switching from server to client error', async () => {
    // Given: a 500 server error (auto-expanded)
    const problem = ref({ status: 500, traceId: 'server-trace', context: { op: 'save' } })
    const wrapper = mount(ErrorDisplay, {
      props: { show: true, problem: problem.value },
      global: { stubs: { FeatherIcon } },
    })
    expect(wrapper.find('[data-test-id="more-text"]').exists()).toBe(true)

    // When: problem changes to a 400 client error
    await wrapper.setProps({ problem: { status: 400, traceId: 'client-trace' } })

    // Then: diagnostics are collapsed
    expect(wrapper.find('[data-test-id="more-text"]').exists()).toBe(false)
  })

  it('emits update:show false when close button is clicked', async () => {
    // Given: a visible error display
    const wrapper = mount(ErrorDisplay, {
      props: { show: true, problem: { title: 'Error' } },
      global: { stubs: { FeatherIcon } },
    })

    // When: close button is clicked
    await wrapper.find('[data-test-id="close-button"]').trigger('click')

    // Then: update:show false is emitted
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })
})

// ---------------------------------------------------------------------------
// ProblemDetailsViewer
// ---------------------------------------------------------------------------

describe('ProblemDetailsViewer', () => {
  beforeEach(() => {
    useProblemDetailsMock.mockReset()
  })

  // ProblemDetailsViewer uses <ErrorDisplay> as a Nuxt auto-imported component.
  // Register it globally so mount() can resolve it.
  const globalConfig = {
    components: { ErrorDisplay },
    stubs: { FeatherIcon },
  }

  it('is hidden when there is no problem', () => {
    // Given: no active problem
    useProblemDetailsMock.mockReturnValue({
      problem: ref(undefined),
      clearError: vi.fn(),
    })

    const wrapper = mount(ProblemDetailsViewer, { global: globalConfig })

    // Then: the viewer is not rendered
    expect(wrapper.find('[data-test-id="problem-details-viewer"]').exists()).toBe(false)
  })

  it('shows the problem when one is set', async () => {
    // Given: an active problem
    const problem = ref<object | undefined>({
      title: 'Global failure',
      errors: { Name: ['Required.'] },
    })
    useProblemDetailsMock.mockReturnValue({
      problem,
      clearError: vi.fn(),
    })

    const wrapper = mount(ProblemDetailsViewer, { global: globalConfig })

    // Then: the viewer is rendered with the problem title
    expect(wrapper.find('[data-test-id="problem-details-viewer"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="title-display"]').text()).toBe('Global failure')
    expect(wrapper.find('[data-test-id="validation-errors"]').text()).toContain('Required.')
  })

  it('calls clearError when close button is clicked', async () => {
    // Given: an active problem
    const clearError = vi.fn()
    const problem = ref<object | undefined>({ title: 'Global failure' })
    useProblemDetailsMock.mockReturnValue({ problem, clearError })

    const wrapper = mount(ProblemDetailsViewer, { global: globalConfig })

    // When: close button is clicked
    await wrapper.find('[data-test-id="close-button"]').trigger('click')

    // Then: clearError is called
    expect(clearError).toHaveBeenCalledOnce()
  })

  it('updates when the problem changes', async () => {
    // Given: an initial problem
    const problem = ref<object | undefined>({ title: 'First failure' })
    useProblemDetailsMock.mockReturnValue({
      problem,
      clearError: vi.fn(),
    })

    const wrapper = mount(ProblemDetailsViewer, { global: globalConfig })
    expect(wrapper.find('[data-test-id="title-display"]').text()).toBe('First failure')

    // When: problem changes
    problem.value = { title: 'Updated failure' }
    await nextTick()

    // Then: the new title is shown
    expect(wrapper.find('[data-test-id="title-display"]').text()).toBe('Updated failure')
  })
})
