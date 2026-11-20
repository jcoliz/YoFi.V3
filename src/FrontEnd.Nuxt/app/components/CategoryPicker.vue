<script setup lang="ts">
/**
 * Provides free-form category entry with accessible, tenant-scoped suggestions from persisted categories.
 */
import { TransactionsClient } from '~/utils/apiclient'

const props = withDefaults(
  defineProps<{
    /** Complete category text controlled by the parent form. */
    modelValue: string
    /** DOM identifier assigned to the category text input. */
    inputId: string
    /** Test identifier assigned to the category text input. */
    inputTestId: string
    /** Current tenant key used to scope category suggestions. */
    tenantKey: string
    /** Maximum number of characters accepted by the input. */
    maxlength?: number
    /** Whether the category input is disabled. */
    disabled?: boolean
    /** Placeholder text displayed when the input is empty. */
    placeholder?: string
    /** Whether Bootstrap validation styling should mark the input invalid. */
    invalid?: boolean
    /** Optional identifier for the element that describes the input. */
    describedBy?: string
  }>(),
  {
    maxlength: 200,
    disabled: false,
    placeholder: '',
    invalid: false,
    describedBy: undefined,
  },
)

const emit = defineEmits<{
  /** Emits the complete category text after an input or suggestion selection. */
  'update:modelValue': [value: string]
  /** Forwards the input blur event to the surrounding form. */
  blur: [event: FocusEvent]
  /** Forwards the input keydown event to the surrounding form. */
  keydown: [event: KeyboardEvent]
  /** Forwards the input keyup event to the surrounding form. */
  keyup: [event: KeyboardEvent]
}>()

const transactionsClient = useApiClient(TransactionsClient)
const wrapper = ref<HTMLElement | null>(null)
const suggestions = ref<string[]>([])
const open = ref(false)
const activeIndex = ref(-1)
let debounceTimer: ReturnType<typeof setTimeout> | undefined
let requestGeneration = 0

/** Removes empty category terms and normalizes separators for lookup validation and sending. */
function sanitizeLookup(value: string): string {
  const terms = value
    .split(':')
    .map((term) => term.trim().replace(/\s+/g, ' '))
    .filter((term) => term.length > 0)
  return terms.join(':')
}

/** Closes the popup and invalidates pending or in-flight lookups. */
function clearSuggestions() {
  if (debounceTimer) {
    clearTimeout(debounceTimer)
    debounceTimer = undefined
  }
  requestGeneration++
  suggestions.value = []
  activeIndex.value = -1
  open.value = false
}

watch(
  () => props.tenantKey,
  () => clearSuggestions(),
)

onBeforeUnmount(() => clearSuggestions())

async function lookup(term: string, generation: number) {
  if (!props.tenantKey) {
    clearSuggestions()
    return
  }

  try {
    const matches = await transactionsClient.getCategorySuggestions(term, props.tenantKey)
    if (generation !== requestGeneration || !props.tenantKey) return
    suggestions.value = matches
    activeIndex.value = matches.length > 0 ? 0 : -1
    open.value = matches.length > 0
  } catch {
    if (generation !== requestGeneration) return
    suggestions.value = []
    activeIndex.value = -1
    open.value = false
  }
}

/** Handles only actual input events, so v-model initialization and prop updates remain silent. */
function handleInput(event: Event) {
  const value = (event.target as HTMLInputElement).value
  emit('update:modelValue', value)

  if (debounceTimer) {
    clearTimeout(debounceTimer)
    debounceTimer = undefined
  }

  const term = sanitizeLookup(value)
  if (!term) {
    clearSuggestions()
    return
  }

  const generation = ++requestGeneration
  debounceTimer = setTimeout(() => {
    debounceTimer = undefined
    void lookup(term, generation)
  }, 200)
}

function chooseSuggestion(category: string) {
  emit('update:modelValue', category)
  clearSuggestions()
}

function handleKeydown(event: KeyboardEvent) {
  if (!open.value || suggestions.value.length === 0) {
    emit('keydown', event)
    return
  }

  if (event.key === 'ArrowDown') {
    event.preventDefault()
    activeIndex.value = (activeIndex.value + 1) % suggestions.value.length
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    activeIndex.value =
      activeIndex.value <= 0 ? suggestions.value.length - 1 : activeIndex.value - 1
  } else if (event.key === 'Escape') {
    event.preventDefault()
    event.stopPropagation()
    clearSuggestions()
  } else if (event.key === 'Enter') {
    event.preventDefault()
    event.stopPropagation()
    const index = activeIndex.value >= 0 ? activeIndex.value : 0
    const selected = suggestions.value[index]
    if (selected !== undefined) chooseSuggestion(selected)
  } else if (event.key === 'Tab') {
    const index = activeIndex.value >= 0 ? activeIndex.value : 0
    const selected = suggestions.value[index]
    if (selected !== undefined) chooseSuggestion(selected)
  }

  emit('keydown', event)
}

function handleKeyup(event: KeyboardEvent) {
  emit('keyup', event)
}

function handleBlur(event: FocusEvent) {
  emit('blur', event)
  const nextTarget = event.relatedTarget
  if (!(nextTarget instanceof Node) || !wrapper.value?.contains(nextTarget)) {
    clearSuggestions()
  }
}

function handleFocusOut(event: FocusEvent) {
  const nextTarget = event.relatedTarget
  if (!(nextTarget instanceof Node) || !wrapper.value?.contains(nextTarget)) {
    clearSuggestions()
  }
}
</script>

<template>
  <div
    ref="wrapper"
    class="category-picker position-relative"
    @focusout="handleFocusOut"
  >
    <input
      :id="inputId"
      :value="modelValue"
      type="text"
      class="form-control"
      :class="{ 'is-invalid': invalid }"
      :maxlength="maxlength"
      :disabled="disabled"
      :placeholder="placeholder"
      :aria-describedby="describedBy"
      :aria-invalid="invalid || undefined"
      :aria-expanded="open"
      aria-haspopup="listbox"
      role="combobox"
      :aria-controls="open ? `${inputId}-suggestions` : undefined"
      :aria-activedescendant="
        open && activeIndex >= 0 ? `${inputId}-suggestion-${activeIndex}` : undefined
      "
      :data-test-id="inputTestId"
      @input="handleInput"
      @keydown="handleKeydown"
      @keyup="handleKeyup"
      @blur="handleBlur"
    />

    <ul
      v-if="open && suggestions.length > 0"
      :id="`${inputId}-suggestions`"
      class="category-picker-suggestions list-group position-absolute w-100"
      role="listbox"
      data-test-id="suggestions"
    >
      <li
        v-for="(suggestion, index) in suggestions"
        :id="`${inputId}-suggestion-${index}`"
        :key="suggestion"
        class="list-group-item list-group-item-action"
        :class="{ active: activeIndex === index }"
        role="option"
        :aria-selected="activeIndex === index"
        @mousedown.prevent
        @mouseenter="activeIndex = index"
        @click="chooseSuggestion(suggestion)"
      >
        {{ suggestion }}
      </li>
    </ul>
  </div>
</template>

<style scoped>
.category-picker-suggestions {
  z-index: 1060;
  max-height: 240px;
  margin-top: 2px;
  overflow-y: auto;
  box-shadow: 0 0.25rem 0.5rem rgb(0 0 0 / 15%);
}

.category-picker-suggestions .list-group-item {
  cursor: pointer;
}
</style>
