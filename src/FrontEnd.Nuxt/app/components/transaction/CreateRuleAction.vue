<script setup lang="ts">
/**
 * Owns the dialog and rule creation independently of the transaction rows, which
 * can disappear during a refresh while the dialog is still open.
 */
import {
  PayeeMatchingRulesClient,
  PayeeMatchingRuleCreateDto,
  TransactionQuickEditDto,
  TransactionsClient,
  type IProblemDetails,
  type PayeeMatchingRuleEditDto,
  type TransactionResultDto,
} from '~/utils/apiclient'
import { handleApiError } from '~/utils/errorHandler'
import { useToastStore } from '~/stores/toast'

const props = defineProps<{
  /** The current workspace identifier. */
  tenantKey: string
}>()

const emit = defineEmits<{
  refresh: [tenantKey: string, failureDetail: string, problem?: IProblemDetails]
}>()

const transactionsClient = useApiClient(TransactionsClient)
const payeeRulesClient = useApiClient(PayeeMatchingRulesClient)
const toastStore = useToastStore()

const showDialog = ref(false)
const saving = ref(false)
const ruleError = ref<IProblemDetails | undefined>(undefined)
const transaction = ref<TransactionResultDto | null>(null)
let active = true

onBeforeUnmount(() => {
  active = false
})

watch(
  () => props.tenantKey,
  () => {
    showDialog.value = false
    transaction.value = null
    ruleError.value = undefined
  },
)

function openDialog(source: TransactionResultDto) {
  if (saving.value) return
  transaction.value = source
  ruleError.value = undefined
  showDialog.value = true
}

async function createRule(
  rule: PayeeMatchingRuleEditDto,
  applyToUncategorizedTransactions: boolean,
) {
  if (saving.value) return

  const tenantKey = props.tenantKey
  const source = transaction.value
  if (!tenantKey || !source?.key) {
    ruleError.value = handleApiError(
      new Error('No transaction selected'),
      'Create Rule Failed',
      'Select a transaction in the current workspace before creating a rule',
    )
    return
  }

  saving.value = true
  ruleError.value = undefined
  let sourceUpdated = false

  try {
    if (!source.hasMultipleSplits) {
      try {
        await transactionsClient.quickEditTransaction(
          source.key,
          tenantKey,
          new TransactionQuickEditDto({
            payee: source.payee,
            memo: source.memo,
            category: rule.category?.trim(),
          }),
        )
        sourceUpdated = true
      } catch (err) {
        if (!active || props.tenantKey !== tenantKey) return
        ruleError.value = handleApiError(
          err,
          'Source Update Failed',
          'The selected transaction could not be updated. No rule was created',
        )
        return
      }
    }

    try {
      const created = await payeeRulesClient.createRule(
        tenantKey,
        PayeeMatchingRuleCreateDto.fromJS({
          payeePattern: rule.payeePattern,
          payeeIsRegex: rule.payeeIsRegex,
          category: rule.category,
          applyToUncategorizedTransactions,
        }),
      )
      if (!active || props.tenantKey !== tenantKey) return

      showDialog.value = false

      const count = created.transactionsCategorized
      if (
        applyToUncategorizedTransactions &&
        (!Number.isInteger(count) || count === undefined || count < 0)
      ) {
        emit(
          'refresh',
          tenantKey,
          'The rule was created, but refreshing the transaction list failed. Do not create the rule again',
          handleApiError(
            new Error('Invalid categorized transaction count'),
            'Rule Created',
            'The rule was created, but the server did not return a valid categorized transaction count. Do not create the rule again',
          ),
        )
        return
      }

      const message = applyToUncategorizedTransactions
        ? count === 0
          ? 'No additional uncategorized transactions matched.'
          : `Categorized ${count} additional transaction${count === 1 ? '' : 's'}.`
        : 'The payee matching rule was created.'
      toastStore.success(
        message,
        'Rule created',
        applyToUncategorizedTransactions ? `rule-categorized-${count}` : 'rule-created',
      )
      emit(
        'refresh',
        tenantKey,
        'The rule was created, but refreshing the transaction list failed. Do not create the rule again',
      )
    } catch (err) {
      if (!active || props.tenantKey !== tenantKey) return
      ruleError.value = handleApiError(
        err,
        'Create Rule Failed',
        sourceUpdated
          ? 'The source transaction was updated, but the rule could not be created'
          : 'Failed to create payee matching rule',
      )
      if (sourceUpdated) {
        emit(
          'refresh',
          tenantKey,
          'The source transaction was updated, but refreshing the transaction list failed',
        )
      }
    }
  } finally {
    saving.value = false
  }
}

function cancelDialog() {
  if (!saving.value) showDialog.value = false
}

defineExpose({ open: openDialog, saving })
</script>

<template>
  <PayeeRulesDialog
    v-model:show="showDialog"
    :tenant-key="tenantKey"
    mode="create"
    :loading="saving"
    :from-transaction="true"
    :problem="ruleError"
    :initial-payee-pattern="transaction?.payee || ''"
    :initial-category="transaction?.category || ''"
    :initial-is-regex="false"
    @save="createRule"
    @cancel="cancelDialog"
  />
</template>
