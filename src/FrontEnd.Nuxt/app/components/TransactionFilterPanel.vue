<script setup lang="ts">
/**
 * Standalone advanced transaction filter panel.
 *
 * Maintains editable filter drafts, validates dates and amounts, and emits
 * complete criteria when they are committed. The parent owns applied state,
 * search coordination, API requests, and Clear All behavior; this component
 * has no store, workspace, or API-client dependency.
 */
import { transactionDatePreset, type TransactionDatePreset } from '~/utils/dateOnly'
import type { TransactionAdvancedFilters } from '~/types/transactionFilters'

/**
 * Inputs controlling the panel's visibility, availability, and applied criteria.
 */
interface Props {
  /** Whether the filter panel is expanded. */
  expanded: boolean
  /** Disables actions and prevents draft commits while a request is in progress. */
  disabled?: boolean
  /** Applied filters; changes replace the panel's local draft. */
  filters: TransactionAdvancedFilters
}

const props = withDefaults(defineProps<Props>(), { disabled: false })

/** Date presets displayed in the panel. */
const datePresets = [
  { id: 'last30Days', label: 'Last 30d' },
  { id: 'last3Months', label: 'Last 3mo' },
  { id: 'last12Months', label: 'Last 12mo' },
  { id: 'thisYear', label: 'This year' },
  { id: 'allTime', label: 'All time' },
] as const

const emit = defineEmits<{
  /** Emitted with the complete normalized criteria after a valid edit. */
  commit: [filters: TransactionAdvancedFilters]
  /** Emitted when the user requests clearing search and every advanced filter. */
  'clear-all': []
}>()

/** Editable values are kept local until validation succeeds. */
const draft = reactive({ ...props.filters })
/** Prevents duplicate commits before the parent updates the applied props. */
let lastCommitted = { ...props.filters }
const errors = reactive<Record<string, string>>({})
const amountInput = ref<HTMLInputElement | null>(null)
let blurTimer: ReturnType<typeof setTimeout> | undefined

/** Refresh drafts when the parent applies criteria or discards workspace edits. */
watch(
  () => props.filters,
  (filters) => {
    Object.assign(draft, filters)
    lastCommitted = { ...filters }
    clearErrors()
  },
  { deep: true },
)

onBeforeUnmount(() => {
  if (blurTimer) clearTimeout(blurTimer)
})

/** Clears validation messages after the draft or applied model changes. */
function clearErrors() {
  delete errors.fromDate
  delete errors.toDate
  delete errors.amount
}

/** Returns criteria trimmed for submission while preserving date and checkbox values. */
function normalizedDraft(): TransactionAdvancedFilters {
  return {
    fromDate: draft.fromDate,
    toDate: draft.toDate,
    payee: draft.payee.trim(),
    category: draft.category.trim(),
    memo: draft.memo.trim(),
    amount: draft.amount.trim(),
    uncategorizedOnly: draft.uncategorizedOnly,
  }
}

/**
 * Checks the panel's accepted currency/sign syntax and the .NET decimal range.
 * The server remains authoritative; this check provides immediate inline feedback.
 */
function isValidAmount(value: string): boolean {
  let number = value
  const hasLeadingSign = number.startsWith('+') || number.startsWith('-')
  if (hasLeadingSign) number = number.slice(1)

  const hasCurrencyPrefix = number.startsWith('$')
  if (hasCurrencyPrefix) number = number.slice(1)
  if (!hasLeadingSign && hasCurrencyPrefix && (number.startsWith('+') || number.startsWith('-'))) {
    number = number.slice(1)
  }

  if (!number || !/^(?:(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d*)?|\.\d+)$/.test(number)) {
    return false
  }

  const [integerPart = '0', fractionPart = ''] = number.replaceAll(',', '').split('.')
  const integer = BigInt(integerPart || '0')
  const maxDecimalInteger = 79228162514264337593543950335n
  if (integer > maxDecimalInteger) return false
  if (integer < maxDecimalInteger) return true
  return fractionPart.replace(/0+$/, '') === ''
}

/** Validates the complete draft and records field-specific errors. */
function validate(): TransactionAdvancedFilters | null {
  clearErrors()
  const result = normalizedDraft()

  if (result.fromDate && result.toDate && result.fromDate > result.toDate) {
    errors.toDate = 'To Date must be on or after From Date.'
  }
  if (result.amount && !isValidAmount(result.amount)) {
    errors.amount = 'Enter a valid amount, such as 42, +42, or $-42.'
  }

  return Object.keys(errors).length ? null : result
}

/** Moves focus to the first field with a validation error. */
function focusFirstError() {
  if (errors.fromDate) document.getElementById('fromDate')?.focus()
  else if (errors.toDate) document.getElementById('toDate')?.focus()
  else if (errors.amount) amountInput.value?.focus()
}

/** Compares complete criteria to suppress normalized no-op commits. */
function sameFilters(left: TransactionAdvancedFilters, right: TransactionAdvancedFilters): boolean {
  return (
    left.fromDate === right.fromDate &&
    left.toDate === right.toDate &&
    left.payee === right.payee &&
    left.category === right.category &&
    left.memo === right.memo &&
    left.amount === right.amount &&
    left.uncategorizedOnly === right.uncategorizedOnly
  )
}

/**
 * Validates the draft and optionally emits it to the parent.
 *
 * Exposed for parent actions such as search submission and panel collapse.
 * Setting `emit` to false lets the parent coordinate one atomic state update
 * and request with another action.
 */
function validateAndCommit(
  options: { emit?: boolean; focusInvalid?: boolean } = {},
): TransactionAdvancedFilters | null {
  if (props.disabled) return null

  const result = validate()
  if (!result) {
    if (options.focusInvalid !== false) focusFirstError()
    return null
  }

  if (options.emit !== false && !sameFilters(result, lastCommitted)) {
    lastCommitted = { ...result }
    emit('commit', result)
  }
  return result
}

/** Defers blur commits briefly so a following click can take precedence. */
function commitOnBlur() {
  if (props.disabled) return
  if (blurTimer) clearTimeout(blurTimer)
  blurTimer = setTimeout(() => {
    blurTimer = undefined
    validateAndCommit({ focusInvalid: false })
  }, 50)
}

/** Cancels a pending blur commit and commits the current draft once. */
function commitImmediately() {
  if (blurTimer) {
    clearTimeout(blurTimer)
    blurTimer = undefined
  }
  validateAndCommit()
}

/** Applies a date preset atomically while retaining all non-date draft fields. */
function selectPreset(preset: TransactionDatePreset) {
  if (props.disabled) return
  Object.assign(draft, transactionDatePreset(preset))
  commitImmediately()
}

/** Requests that the parent clear quick search and all applied filter criteria. */
function clearAll() {
  if (props.disabled) return
  if (blurTimer) {
    clearTimeout(blurTimer)
    blurTimer = undefined
  }
  clearErrors()
  emit('clear-all')
}

/** Discards uncommitted draft values and restores the current applied model. */
function resetDraft() {
  Object.assign(draft, props.filters)
  clearErrors()
  if (blurTimer) {
    clearTimeout(blurTimer)
    blurTimer = undefined
  }
}

defineExpose({ validateAndCommit, resetDraft })
</script>

<template>
  <Transition name="expand">
    <div
      v-if="expanded"
      class="expand-panel"
    >
      <div class="expand-panel-inner">
        <div
          id="date-range-filters"
          class="card mb-4"
          data-test-id="date-range-filters"
        >
          <div class="card-body">
            <section aria-label="Date range">
              <h2 class="h6">Date range</h2>
              <div class="d-flex flex-wrap gap-2 mb-3">
                <button
                  v-for="preset in datePresets"
                  :key="preset.id"
                  type="button"
                  class="btn btn-sm btn-outline-secondary"
                  :data-test-id="`date-preset-${preset.id}`"
                  :disabled="disabled"
                  @click="selectPreset(preset.id)"
                >
                  {{ preset.label }}
                </button>
              </div>
              <div class="row g-3">
                <div class="col-12 col-md-6">
                  <label
                    for="fromDate"
                    class="form-label"
                    >From Date</label
                  >
                  <input
                    id="fromDate"
                    v-model="draft.fromDate"
                    type="date"
                    class="form-control"
                    data-test-id="from-date-filter"
                    :disabled="disabled"
                    :aria-invalid="!!errors.fromDate"
                    :aria-describedby="errors.fromDate ? 'from-date-error' : undefined"
                    @change="commitImmediately"
                  />
                  <div
                    v-if="errors.fromDate"
                    id="from-date-error"
                    class="invalid-feedback d-block"
                  >
                    {{ errors.fromDate }}
                  </div>
                </div>
                <div class="col-12 col-md-6">
                  <label
                    for="toDate"
                    class="form-label"
                    >To Date</label
                  >
                  <input
                    id="toDate"
                    v-model="draft.toDate"
                    type="date"
                    class="form-control"
                    data-test-id="to-date-filter"
                    :disabled="disabled"
                    :aria-invalid="!!errors.toDate"
                    :aria-describedby="errors.toDate ? 'to-date-error' : undefined"
                    @change="commitImmediately"
                  />
                  <div
                    v-if="errors.toDate"
                    id="to-date-error"
                    class="invalid-feedback d-block"
                  >
                    {{ errors.toDate }}
                  </div>
                </div>
              </div>
            </section>

            <hr />

            <div class="row g-3">
              <div class="col-12 col-md-6">
                <label
                  for="payeeFilter"
                  class="form-label"
                  >Payee</label
                >
                <input
                  id="payeeFilter"
                  ref="payeeInput"
                  v-model="draft.payee"
                  type="text"
                  class="form-control"
                  data-test-id="payee-filter"
                  @keyup.enter.prevent="commitImmediately"
                  @blur="commitOnBlur"
                />
              </div>
              <div class="col-12 col-md-6">
                <label
                  for="categoryFilter"
                  class="form-label"
                  >Category (any split)</label
                >
                <input
                  id="categoryFilter"
                  ref="categoryInput"
                  v-model="draft.category"
                  type="text"
                  class="form-control"
                  data-test-id="category-filter"
                  @keyup.enter.prevent="commitImmediately"
                  @blur="commitOnBlur"
                />
              </div>
              <div class="col-12 col-md-6">
                <label
                  for="memoFilter"
                  class="form-label"
                  >Memo</label
                >
                <input
                  id="memoFilter"
                  ref="memoInput"
                  v-model="draft.memo"
                  type="text"
                  class="form-control"
                  data-test-id="memo-filter"
                  aria-describedby="memo-filter-help"
                  @keyup.enter.prevent="commitImmediately"
                  @blur="commitOnBlur"
                />
                <div
                  id="memo-filter-help"
                  class="form-text"
                >
                  Searches transaction and split memos.
                </div>
              </div>
              <div class="col-12 col-md-6">
                <label
                  for="amountFilter"
                  class="form-label"
                  >Amount</label
                >
                <input
                  id="amountFilter"
                  ref="amountInput"
                  v-model="draft.amount"
                  type="text"
                  inputmode="decimal"
                  class="form-control"
                  data-test-id="amount-filter"
                  :aria-invalid="!!errors.amount"
                  :aria-describedby="
                    errors.amount ? 'amount-filter-help amount-filter-error' : 'amount-filter-help'
                  "
                  @keyup.enter.prevent="commitImmediately"
                  @blur="commitOnBlur"
                />
                <div
                  id="amount-filter-help"
                  class="form-text"
                >
                  Unsigned amounts match either sign; use + or - to restrict the sign.
                </div>
                <div
                  v-if="errors.amount"
                  id="amount-filter-error"
                  class="invalid-feedback d-block"
                >
                  {{ errors.amount }}
                </div>
              </div>
              <div class="col-12">
                <div class="form-check">
                  <input
                    id="uncategorizedOnly"
                    v-model="draft.uncategorizedOnly"
                    type="checkbox"
                    class="form-check-input"
                    data-test-id="uncategorized-filter"
                    :disabled="disabled"
                    @change="commitImmediately"
                  />
                  <label
                    for="uncategorizedOnly"
                    class="form-check-label"
                  >
                    Uncategorized only
                  </label>
                  <div class="form-text">Includes transactions with any blank split category.</div>
                </div>
              </div>
            </div>

            <div class="d-flex justify-content-end mt-3">
              <button
                type="button"
                class="btn btn-secondary"
                data-test-id="clear-filters-button"
                :disabled="disabled"
                @click="clearAll"
              >
                Clear All
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.expand-panel {
  display: grid;
  grid-template-rows: 1fr;
}

.expand-panel-inner {
  min-height: 0;
  overflow: hidden;
}

.expand-enter-active,
.expand-leave-active {
  transition:
    grid-template-rows 250ms ease,
    opacity 250ms ease;
}

.expand-enter-from,
.expand-leave-to {
  grid-template-rows: 0fr;
  opacity: 0;
}

@media (prefers-reduced-motion: reduce) {
  .expand-enter-active,
  .expand-leave-active {
    transition: none;
  }
}
</style>
