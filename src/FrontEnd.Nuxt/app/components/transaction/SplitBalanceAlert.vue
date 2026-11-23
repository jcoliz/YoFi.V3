<script setup lang="ts">
/**
 * Renders the authoritative transaction and split totals for an unbalanced transaction.
 * Balance status and totals are supplied by the detail API response; this component does not calculate them.
 */
const props = defineProps<{
  transactionAmount: number
  splitsTotal: number
}>()

/** Formats an authoritative currency value consistently with the transaction detail page. */
function formatCurrency(amount: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(amount)
}

const difference = computed(() => props.transactionAmount - props.splitsTotal)
const formattedDifference = computed(() => {
  const formatted = formatCurrency(Math.abs(difference.value))
  if (difference.value > 0) return `+${formatted}`
  if (difference.value < 0) return `-${formatted}`
  return formatted
})
</script>

<template>
  <BaseAlert
    variant="danger"
    data-test-id="transaction-split-balance-alert"
  >
    Splits do not match the transaction amount.
    <template #detail>
      Transaction amount:
      <span data-test-id="transaction-amount">{{ formatCurrency(transactionAmount) }}</span>. Split
      total: <span data-test-id="transaction-split-total">{{ formatCurrency(splitsTotal) }}</span>.
      Difference:
      <span data-test-id="transaction-split-difference">{{ formattedDifference }}</span>.
    </template>
  </BaseAlert>
</template>
