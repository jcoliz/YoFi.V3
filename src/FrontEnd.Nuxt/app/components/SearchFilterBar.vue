<script setup lang="ts">
/**
 * Search and Filter Bar Component
 *
 * General-purpose search bar with search and clear buttons. Performs no data
 * loading itself; the parent page reacts to the emitted events.
 *
 * Additional controls (e.g. a sort selector) can be supplied via the default slot,
 * and are rendered to the right of the search input.
 */

/**
 * Props for the component
 */
interface Props {
  /**
   * Label displayed above the search input
   */
  label?: string
  /**
   * Placeholder text for the search input
   */
  placeholder?: string
  /**
   * DOM id for the search input (associates the label with the input)
   */
  inputId?: string
  /**
   * Whether the search and clear buttons are disabled
   */
  disabled?: boolean
}

withDefaults(defineProps<Props>(), {
  label: 'Search',
  placeholder: 'Search...',
  inputId: 'searchText',
  disabled: false,
})

/**
 * Current search text (v-model)
 */
const searchText = defineModel<string>({ default: '' })

/**
 * Events emitted by the component
 */
const emit = defineEmits<{
  /**
   * User requested a search (Enter key or search button)
   */
  search: [value: string]
  /**
   * User cleared the search text
   */
  clear: []
}>()

/**
 * Emits a search request with the current search text.
 */
function handleSearch() {
  emit('search', searchText.value)
}

/**
 * Clears the search text and notifies the parent.
 */
function handleClear() {
  searchText.value = ''
  emit('clear')
}
</script>

<template>
  <div
    class="card mb-4"
    data-test-id="search-filter-bar"
  >
    <div class="card-body">
      <label
        :for="inputId"
        class="form-label"
        >{{ label }}</label
      >
      <div class="d-flex gap-2">
        <div class="input-group flex-grow-1">
          <input
            :id="inputId"
            v-model="searchText"
            type="text"
            class="form-control"
            :placeholder="placeholder"
            data-test-id="search-input"
            @keyup.enter="handleSearch"
          />
          <button
            class="btn btn-outline-secondary"
            data-test-id="search-button"
            :disabled="disabled"
            @click="handleSearch"
          >
            <FeatherIcon
              icon="search"
              size="16"
            />
          </button>
          <button
            v-if="searchText"
            class="btn btn-outline-secondary"
            data-test-id="clear-search-button"
            :disabled="disabled"
            @click="handleClear"
          >
            <FeatherIcon
              icon="x"
              size="16"
            />
          </button>
        </div>
        <slot />
      </div>
    </div>
  </div>
</template>
