<script setup lang="ts">
/**
 * Payee Rule Sort Selector Component
 *
 * Provides a dropdown menu for selecting sort order for payee rules.
 * Modeled after LoginState.vue to ensure proper DropDownPortable usage.
 */

import { PayeeRuleSortBy } from '~/utils/apiclient'

/**
 * Props for the component
 */
interface Props {
  /**
   * Current sort selection
   */
  modelValue: PayeeRuleSortBy
  /**
   * Whether the control is disabled
   */
  disabled?: boolean
}

defineProps<Props>()

/**
 * Emits for v-model support
 */
const emit = defineEmits<{
  'update:modelValue': [value: PayeeRuleSortBy]
}>()

/**
 * Handles sort option selection
 */
function selectSort(sortBy: PayeeRuleSortBy) {
  emit('update:modelValue', sortBy)
}

const ready = ref(false)
onMounted(() => {
  ready.value = true
})
</script>

<template>
  <DropDownPortable data-test-id="sort-selector-dropdown">
    <template #trigger>
      <button
        class="btn btn-outline-secondary dropdown-toggle"
        type="button"
        title="Sort by"
        data-bs-toggle="dropdown"
        aria-expanded="false"
      >
        <FeatherIcon
          icon="sliders"
          size="16"
        />
      </button>
    </template>
    <template #default>
      <ul
        class="dropdown-menu dropdown-menu-end"
        data-test-id="sort-dropdown"
      >
        <li>
          <a
            class="dropdown-item"
            :class="{ active: modelValue === PayeeRuleSortBy.PayeePattern }"
            data-test-id="sort-payee-pattern"
            @click="selectSort(PayeeRuleSortBy.PayeePattern)"
          >
            <FeatherIcon
              icon="type"
              size="14"
              class="me-2"
            />
            Payee Pattern (A-Z)
          </a>
        </li>
        <li>
          <a
            class="dropdown-item"
            :class="{ active: modelValue === PayeeRuleSortBy.Category }"
            data-test-id="sort-category"
            @click="selectSort(PayeeRuleSortBy.Category)"
          >
            <FeatherIcon
              icon="tag"
              size="14"
              class="me-2"
            />
            Category (A-Z)
          </a>
        </li>
        <li>
          <a
            class="dropdown-item"
            :class="{ active: modelValue === PayeeRuleSortBy.LastUsedAt }"
            data-test-id="sort-last-used"
            @click="selectSort(PayeeRuleSortBy.LastUsedAt)"
          >
            <FeatherIcon
              icon="clock"
              size="14"
              class="me-2"
            />
            Last Used
          </a>
        </li>
      </ul>
    </template>
  </DropDownPortable>
</template>
