import { afterEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { Dropdown } from 'bootstrap'
import EditDeleteDropdown from '~/components/EditDeleteDropdown.vue'

enableAutoUnmount(afterEach)

function mountMenu(disabled = false) {
  return mount(EditDeleteDropdown, {
    attachTo: document.body,
    props: { triggerLabel: 'Actions for Groceries', disabled },
    global: {
      stubs: {
        FeatherIcon: true,
        DropDownPortable: {
          template: '<div><slot name="trigger" /><slot /></div>',
        },
      },
    },
  })
}

describe('shared edit/delete dropdown', () => {
  it('passes the template prop to the trigger', () => {
    const wrapper = mount(
      {
        components: { EditDeleteDropdown },
        template: '<EditDeleteDropdown :trigger-label="label" />',
        setup: () => ({ label: 'Actions for Groceries' }),
      },
      {
        global: {
          stubs: {
            FeatherIcon: true,
            DropDownPortable: { template: '<div><slot name="trigger" /><slot /></div>' },
          },
        },
      },
    )
    expect(wrapper.get('[data-test-id="row-actions-button"]').attributes('aria-label')).toBe(
      'Actions for Groceries',
    )
  })

  it('labels the trigger and delegates opening to Bootstrap', () => {
    const wrapper = mountMenu()
    const trigger = wrapper.get('[data-test-id="row-actions-button"]')
    expect(trigger.attributes('aria-label')).toBe('Actions for Groceries')
    expect(trigger.attributes('data-bs-toggle')).toBe('dropdown')
  })

  it.each(['edit', 'delete'])('emits %s and lets Bootstrap close the menu', async (action) => {
    const wrapper = mountMenu()
    const trigger = wrapper.get<HTMLButtonElement>('[data-test-id="row-actions-button"]').element
    const dropdown = new Dropdown(trigger)
    try {
      trigger.click()
      expect(trigger.getAttribute('aria-expanded')).toBe('true')
      await wrapper.get(`[data-test-id="${action}-button"]`).trigger('click')
      expect(wrapper.emitted(action)).toEqual([[]])
      expect(trigger.getAttribute('aria-expanded')).toBe('false')
    } finally {
      dropdown.dispose()
    }
  })

  it('disables both actions and the trigger', async () => {
    const wrapper = mountMenu(true)
    for (const id of ['row-actions-button', 'edit-button', 'delete-button']) {
      const button = wrapper.get<HTMLButtonElement>(`[data-test-id="${id}"]`)
      expect(button.element.disabled).toBe(true)
      await button.trigger('click')
    }
    expect(wrapper.emitted('edit')).toBeUndefined()
    expect(wrapper.emitted('delete')).toBeUndefined()
  })

  it('lets Bootstrap switch menus and close them on outside clicks', async () => {
    const first = mountMenu()
    const second = mountMenu()
    const firstTrigger = first.get<HTMLButtonElement>('[data-test-id="row-actions-button"]').element
    const secondTrigger = second.get<HTMLButtonElement>(
      '[data-test-id="row-actions-button"]',
    ).element
    const firstDropdown = new Dropdown(firstTrigger)
    const secondDropdown = new Dropdown(secondTrigger)
    try {
      firstTrigger.click()
      expect(firstTrigger.getAttribute('aria-expanded')).toBe('true')
      secondTrigger.click()
      expect(firstTrigger.getAttribute('aria-expanded')).toBe('false')
      expect(secondTrigger.getAttribute('aria-expanded')).toBe('true')
      document.body.click()
      expect(secondTrigger.getAttribute('aria-expanded')).toBe('false')
    } finally {
      firstDropdown.dispose()
      secondDropdown.dispose()
    }
  })
})
