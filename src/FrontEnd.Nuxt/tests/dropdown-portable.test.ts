import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import DropDownPortable from '~/components/DropDownPortable.vue'
import DropDownClientOnly from '~/components/DropDownClientOnly.vue'

const bootstrap = vi.hoisted(() => ({
  create: vi.fn(),
  dispose: vi.fn(),
}))

vi.mock('bootstrap', () => ({
  Dropdown: class {
    static getInstance() {
      return null
    }

    constructor(element: HTMLElement, options: unknown) {
      bootstrap.create(element, options)
    }

    dispose() {
      bootstrap.dispose()
    }
  },
}))

vi.stubGlobal('useDebugLog', () => ({ logger: { error: vi.fn(), debug: vi.fn() } }))
enableAutoUnmount(afterEach)
beforeEach(() => vi.clearAllMocks())

function mountDropdown(fixed?: boolean) {
  return mount(DropDownPortable, {
    props: { fixed },
    slots: {
      trigger: '<button data-bs-toggle="dropdown">Actions</button>',
      default: '<ul class="dropdown-menu"><li><button>Edit</button></li></ul>',
    },
    global: {
      components: {
        ClientOnly: { template: '<slot />' },
        DropDownClientOnly,
      },
    },
  })
}

describe('DropDownPortable compatibility', () => {
  it('keeps Bootstrap defaults and both slots for existing callers', async () => {
    const wrapper = mountDropdown()
    await nextTick()

    expect(bootstrap.create).toHaveBeenCalledWith(wrapper.get('button').element, {})
    expect(wrapper.text()).toContain('Actions')
    expect(wrapper.text()).toContain('Edit')
  })

  it('opts into fixed positioning only when requested', async () => {
    const wrapper = mountDropdown(true)
    await nextTick()

    expect(bootstrap.create).toHaveBeenCalledWith(wrapper.get('button').element, {
      popperConfig: { strategy: 'fixed' },
    })
  })

  it('disposes its Bootstrap instance on unmount', async () => {
    const wrapper = mountDropdown()
    await nextTick()
    wrapper.unmount()

    expect(bootstrap.dispose).toHaveBeenCalledOnce()
  })
})
