<script setup lang="ts">
/**
 * PayeeRuleDialog Component
 *
 * Reusable dialog for creating and editing payee matching rules.
 * Used by both the Payee Rules management page and the Transactions page.
 */

import { PayeeMatchingRuleEditDto } from '~/utils/apiclient'

interface Props {
  show: boolean
  mode: 'create' | 'edit'
  loading?: boolean
  initialPayeePattern?: string
  initialCategory?: string
  initialIsRegex?: boolean
  ruleKey?: string
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  initialPayeePattern: '',
  initialCategory: '',
  initialIsRegex: false,
  ruleKey: undefined,
})

const emit = defineEmits<{
  'update:show': [value: boolean]
  save: [rule: PayeeMatchingRuleEditDto]
  cancel: []
}>()

// Form data
const formData = ref({
  payeePattern: '',
  payeeIsRegex: false,
  category: '',
})

// Form errors
const formErrors = ref({
  payeePattern: '',
  category: '',
  regex: '',
})

// Watch for prop changes to reset form
watch(
  () => props.show,
  (newShow) => {
    if (newShow) {
      // Reset form when dialog opens
      formData.value = {
        payeePattern: props.initialPayeePattern || '',
        payeeIsRegex: props.initialIsRegex || false,
        category: props.initialCategory || '',
      }
      formErrors.value = {
        payeePattern: '',
        category: '',
        regex: '',
      }
    }
  },
)

function validateForm(): boolean {
  formErrors.value = {
    payeePattern: '',
    category: '',
    regex: '',
  }
  let isValid = true

  if (!formData.value.payeePattern || !formData.value.payeePattern.trim()) {
    formErrors.value.payeePattern = 'Payee pattern is required'
    isValid = false
  } else if (formData.value.payeePattern.length > 200) {
    formErrors.value.payeePattern = 'Payee pattern cannot exceed 200 characters'
    isValid = false
  }

  if (!formData.value.category || !formData.value.category.trim()) {
    formErrors.value.category = 'Category is required'
    isValid = false
  } else if (formData.value.category.length > 200) {
    formErrors.value.category = 'Category cannot exceed 200 characters'
    isValid = false
  }

  return isValid
}

function handleSave() {
  if (!validateForm()) return

  const rule = new PayeeMatchingRuleEditDto({
    payeePattern: formData.value.payeePattern.trim(),
    payeeIsRegex: formData.value.payeeIsRegex,
    category: formData.value.category.trim(),
  })

  emit('save', rule)
}

function handleCancel() {
  emit('cancel')
  emit('update:show', false)
}

const dialogTitle = computed(() => {
  return props.mode === 'create' ? 'Create Payee Matching Rule' : 'Edit Payee Matching Rule'
})

const primaryButtonText = computed(() => {
  if (props.loading) {
    return props.mode === 'create' ? 'Creating...' : 'Updating...'
  }
  return props.mode === 'create' ? 'Create' : 'Update'
})
</script>

<template>
  <ModalDialog
    :show="show"
    :title="dialogTitle"
    :loading="loading"
    :primary-button-text="primaryButtonText"
    primary-button-test-id="save-button"
    secondary-button-test-id="cancel-button"
    :test-id="mode === 'create' ? 'rule-create-dialog' : 'rule-edit-dialog'"
    @primary="handleSave"
    @secondary="handleCancel"
    @update:show="$emit('update:show', $event)"
  >
    <!-- Validation Error Summary -->
    <div
      v-if="formErrors.payeePattern || formErrors.category || formErrors.regex"
      class="alert alert-danger"
      data-test-id="validation-error"
    >
      <ul class="mb-0">
        <li v-if="formErrors.payeePattern">{{ formErrors.payeePattern }}</li>
        <li v-if="formErrors.category">{{ formErrors.category }}</li>
        <li v-if="formErrors.regex">{{ formErrors.regex }}</li>
      </ul>
    </div>

    <!-- Payee Pattern Field -->
    <div class="mb-3">
      <label
        for="payeePattern"
        class="form-label"
        >Payee Pattern</label
      >
      <input
        id="payeePattern"
        v-model="formData.payeePattern"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.payeePattern }"
        placeholder="e.g., Amazon or ^AMZN.*"
        maxlength="200"
        data-test-id="payee-pattern-input"
      />
      <small class="form-text text-muted">
        {{ formData.payeePattern.length }} / 200 characters
      </small>
      <div
        v-if="formErrors.payeePattern"
        class="invalid-feedback"
      >
        {{ formErrors.payeePattern }}
      </div>
    </div>

    <!-- Regex Checkbox -->
    <div class="mb-3 form-check">
      <input
        id="payeeIsRegex"
        v-model="formData.payeeIsRegex"
        type="checkbox"
        class="form-check-input"
        data-test-id="regex-checkbox"
      />
      <label
        class="form-check-label"
        for="payeeIsRegex"
      >
        Use regex pattern (case-insensitive)
      </label>
      <small class="form-text text-muted d-block">
        If checked, the pattern will be treated as a regular expression.
        If unchecked, it will be treated as a simple substring match.
      </small>
    </div>

    <!-- Category Field -->
    <div class="mb-3">
      <label
        for="category"
        class="form-label"
        >Category</label
      >
      <input
        id="category"
        v-model="formData.category"
        type="text"
        class="form-control"
        :class="{ 'is-invalid': formErrors.category }"
        placeholder="e.g., Shopping, Groceries, Utilities"
        maxlength="200"
        data-test-id="category-input"
      />
      <small class="form-text text-muted">
        {{ formData.category.length }} / 200 characters
      </small>
      <div
        v-if="formErrors.category"
        class="invalid-feedback"
      >
        {{ formErrors.category }}
      </div>
    </div>
  </ModalDialog>
</template>

<style scoped>
.form-text {
  font-size: 0.875rem;
  color: #6c757d;
}
</style>
