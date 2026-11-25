<script setup lang="ts">
/**
 * Owns the quick-edit dialog and keeps it outside transaction rows.
 */
import EditModal from '~/components/transaction/EditModal.vue'
import type { TransactionResultDto } from '~/utils/apiclient'

const props = defineProps<{
  tenantKey: string
}>()

const emit = defineEmits<{
  updated: [tenantKey: string]
}>()

const showDialog = ref(false)
const transaction = ref<TransactionResultDto | null>(null)

watch(
  () => props.tenantKey,
  () => {
    showDialog.value = false
    transaction.value = null
  },
)

function openDialog(source: TransactionResultDto) {
  transaction.value = source
  showDialog.value = true
}

function handleUpdated() {
  emit('updated', props.tenantKey)
}

defineExpose({ open: openDialog })
</script>

<template>
  <EditModal
    v-model:show="showDialog"
    :transaction="transaction"
    @updated="handleUpdated"
  />
</template>
