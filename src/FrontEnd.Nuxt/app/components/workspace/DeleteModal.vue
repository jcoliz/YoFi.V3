<script setup lang="ts">
/**
 * Confirms and performs deletion of a workspace, or of all its data, after the
 * user types the workspace's exact name.
 *
 * - `workspace` scope deletes the workspace itself. The name is checked here only;
 *   the workspace delete API does not take a confirmation name.
 * - `data` scope deletes all workspace data, preserving the workspace and its
 *   memberships. The backend validates the name against the route workspace.
 */
import {
  TenantClient,
  TenantDataAdminClient,
  TenantDataDeletionRequest,
  type TenantDataDeletionResultDto,
  type TenantRoleResultDto,
  type IProblemDetails,
} from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'
import { handleApiError } from '~/utils/errorHandler'

const props = withDefaults(
  defineProps<{
    show: boolean
    workspace: TenantRoleResultDto | undefined
    scope?: 'workspace' | 'data'
  }>(),
  { scope: 'workspace' },
)

const emit = defineEmits<{
  'update:show': [value: boolean]
  /** Emitted after a committed deletion; data scope includes the deleted counts. */
  deleted: [result?: TenantDataDeletionResultDto]
  'update:busy': [value: boolean]
}>()

const userPreferencesStore = useUserPreferencesStore()
const tenantClient = useApiClient(TenantClient)
const tenantDataAdminClient = useApiClient(TenantDataAdminClient)

const loading = ref(false)
const error = ref<IProblemDetails | undefined>(undefined)
const showError = ref(false)
const confirmationName = ref('')

const isDataScope = computed(() => props.scope === 'data')
const workspaceName = computed(() => props.workspace?.name ?? '')
const nameMatches = computed(
  () => workspaceName.value !== '' && confirmationName.value === workspaceName.value,
)
const actionText = computed(() =>
  isDataScope.value ? 'Delete All Workspace Data' : 'Delete Workspace',
)

watch(
  () => [props.show, props.workspace, props.scope] as const,
  ([show]) => {
    if (!show) return

    confirmationName.value = ''
    error.value = undefined
    showError.value = false
  },
  { immediate: true },
)

function close(value: boolean) {
  if (loading.value) return
  emit('update:show', value)
}

async function confirmDelete() {
  if (loading.value || !nameMatches.value) return

  const workspaceKey = props.workspace?.key
  if (!workspaceKey) {
    error.value = {
      title: 'No workspace selected',
      detail: 'Please select a workspace to delete',
    }
    showError.value = true
    return
  }

  const scope = props.scope
  const name = confirmationName.value
  loading.value = true
  emit('update:busy', true)
  error.value = undefined
  showError.value = false

  try {
    if (scope === 'data') {
      const result = await tenantDataAdminClient.deleteAllData(
        workspaceKey,
        new TenantDataDeletionRequest({ confirmationWorkspaceName: name }),
      )
      emit('update:show', false)
      emit('deleted', result)
    } else {
      await tenantClient.deleteTenant(workspaceKey)

      if (workspaceKey === userPreferencesStore.getCurrentTenantKey) {
        userPreferencesStore.clearPreferences()
      }

      emit('update:show', false)
      emit('deleted')
    }
  } catch (err) {
    error.value =
      scope === 'data'
        ? handleApiError(
            err,
            'Delete Failed',
            'Failed to delete workspace data. Your workspace data may not have been deleted',
          )
        : handleApiError(err, 'Delete Failed', 'Failed to delete workspace')
    showError.value = true
  } finally {
    loading.value = false
    emit('update:busy', false)
  }
}
</script>

<template>
  <ModalDialog
    :show="show"
    :title="actionText"
    :loading="loading"
    primary-button-variant="danger"
    :primary-button-text="loading ? 'Deleting...' : actionText"
    :primary-button-disabled="!nameMatches"
    primary-button-test-id="delete-submit-button"
    secondary-button-test-id="delete-cancel-button"
    :test-id="isDataScope ? 'delete-data-modal' : 'delete-modal'"
    @primary="confirmDelete"
    @update:show="close"
  >
    <ErrorDisplay
      v-model:show="showError"
      :problem="error"
    />

    <template v-if="isDataScope">
      <p>
        This permanently deletes all data in the workspace
        <strong data-test-id="deleting-workspace-name">{{ workspaceName }}</strong
        >, including transactions, payee matching rules, and pending bank imports.
      </p>
      <p>The workspace itself, its settings, and all of its members remain.</p>
      <p class="text-danger">
        <strong>Warning:</strong> This action cannot be undone. Consider keeping your own copy of
        any data you need before deleting.
      </p>
    </template>
    <template v-else>
      <p>
        This permanently deletes the workspace
        <strong data-test-id="deleting-workspace-name">{{ workspaceName }}</strong>
        itself, along with all of its data and members' access to it.
      </p>
      <p class="text-danger"><strong>Warning:</strong> This action cannot be undone.</p>
    </template>

    <form @submit.prevent="confirmDelete">
      <label
        for="delete-confirmation-name"
        class="form-label"
      >
        To confirm, type the workspace name
        <span class="d-inline-flex align-items-center">
          <strong data-test-id="confirmation-workspace-name">{{ workspaceName }}</strong>
          <CopyTextButton
            :value="workspaceName"
            label="workspace name"
            compact
            test-id-prefix="copy-workspace-name"
            class="ms-1"
          />
        </span>
      </label>
      <input
        id="delete-confirmation-name"
        v-model="confirmationName"
        type="text"
        class="form-control"
        autocomplete="off"
        spellcheck="false"
        :disabled="loading"
        data-test-id="confirmation-name-input"
      />
    </form>
  </ModalDialog>
</template>
