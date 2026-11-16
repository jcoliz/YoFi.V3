<script setup lang="ts">
/**
 * Compact Edit/Delete row actions shared by domain lists.
 * Owns menu presentation only; parents handle authorization, confirmation, and API calls.
 */
const props = defineProps<{
  /** Accessible trigger label identifying the row. */
  triggerLabel: string
  /** Disables the trigger and both actions while the parent is busy. */
  disabled?: boolean
}>()

const emit = defineEmits<{
  /** Requests editing the parent-owned row. */
  edit: []
  /** Requests deletion confirmation for the parent-owned row. */
  delete: []
}>()

const trigger = ref<HTMLButtonElement | null>(null)

function choose(action: 'edit' | 'delete') {
  if (props.disabled) return
  trigger.value?.focus()
  if (action === 'edit') emit('edit')
  else emit('delete')
}
</script>

<template>
  <div>
    <DropDownPortable fixed>
      <template #trigger>
        <button
          ref="trigger"
          type="button"
          class="btn btn-sm btn-outline-secondary border-0 row-actions-trigger"
          :aria-label="triggerLabel"
          :disabled="disabled"
          title="Actions"
          data-bs-toggle="dropdown"
          aria-expanded="false"
          data-test-id="row-actions-button"
        >
          <FeatherIcon
            icon="more-horizontal"
            size="18"
          />
        </button>
      </template>
      <ul
        class="dropdown-menu dropdown-menu-end"
        data-test-id="row-actions-menu"
      >
        <li>
          <button
            type="button"
            class="dropdown-item"
            :disabled="disabled"
            data-test-id="edit-button"
            @click="choose('edit')"
          >
            <FeatherIcon
              icon="edit"
              size="14"
              class="me-2"
            />
            Edit
          </button>
        </li>
        <li>
          <button
            type="button"
            class="dropdown-item text-danger"
            :disabled="disabled"
            data-test-id="delete-button"
            @click="choose('delete')"
          >
            <FeatherIcon
              icon="trash-2"
              size="14"
              class="me-2"
            />
            Delete
          </button>
        </li>
      </ul>
    </DropDownPortable>
  </div>
</template>

<style scoped>
.row-actions-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  padding: 0;
}
</style>
