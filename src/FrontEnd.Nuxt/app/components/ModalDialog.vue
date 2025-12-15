<script setup lang="ts">
/**
 * ModalDialog Component
 *
 * Reusable Bootstrap modal dialog with flexible content slots
 */

interface Props {
  show: boolean
  title: string
  size?: 'sm' | 'lg' | 'xl' | '' // Default is medium (no class)
  loading?: boolean
  primaryButtonText?: string
  primaryButtonVariant?: 'primary' | 'danger' | 'success' | 'warning'
  secondaryButtonText?: string
  hideFooter?: boolean
  hidePrimaryButton?: boolean
  hideSecondaryButton?: boolean
  primaryButtonTestId?: string
  secondaryButtonTestId?: string
}

const props = withDefaults(defineProps<Props>(), {
  size: '',
  loading: false,
  primaryButtonText: 'Save',
  primaryButtonVariant: 'primary',
  secondaryButtonText: 'Cancel',
  hideFooter: false,
  hidePrimaryButton: false,
  hideSecondaryButton: false,
  primaryButtonTestId: '',
  secondaryButtonTestId: '',
})

const emit = defineEmits<{
  'update:show': [value: boolean]
  primary: []
  secondary: []
}>()

function handleBackdropClick() {
  emit('update:show', false)
}

function handleClose() {
  emit('update:show', false)
}

function handlePrimary() {
  emit('primary')
}

function handleSecondary() {
  emit('secondary')
  emit('update:show', false)
}

const modalDialogClass = computed(() => {
  const classes = ['modal-dialog']
  if (props.size) {
    classes.push(`modal-dialog-${props.size}`)
  }
  return classes.join(' ')
})
</script>

<template>
  <Teleport to="body">
    <!-- Modal -->
    <div
      v-if="show"
      class="modal fade show d-block"
      tabindex="-1"
      @click.self="handleBackdropClick"
    >
      <div :class="modalDialogClass">
        <div class="modal-content">
          <!-- Header -->
          <div class="modal-header">
            <h5 class="modal-title">{{ title }}</h5>
            <button
              type="button"
              class="btn-close"
              :disabled="loading"
              @click="handleClose"
            />
          </div>

          <!-- Body -->
          <div class="modal-body">
            <slot />
          </div>

          <!-- Footer -->
          <div
            v-if="!hideFooter"
            class="modal-footer"
          >
            <slot name="footer">
              <button
                v-if="!hideSecondaryButton"
                type="button"
                class="btn btn-secondary"
                :data-test-id="secondaryButtonTestId"
                :disabled="loading"
                @click="handleSecondary"
              >
                {{ secondaryButtonText }}
              </button>
              <button
                v-if="!hidePrimaryButton"
                type="button"
                class="btn"
                :class="`btn-${primaryButtonVariant}`"
                :data-test-id="primaryButtonTestId"
                :disabled="loading"
                @click="handlePrimary"
              >
                <BaseSpinner
                  v-if="loading"
                  size="sm"
                  class="me-1"
                />
                {{ primaryButtonText }}
              </button>
            </slot>
          </div>
        </div>
      </div>
    </div>

    <!-- Backdrop -->
    <div
      v-if="show"
      class="modal-backdrop fade show"
    />
  </Teleport>
</template>

<style scoped>
.modal {
  background-color: rgba(0, 0, 0, 0.5);
}
</style>
