<script setup lang="ts">
definePageMeta({
  title: 'Profile',
  middleware: 'sidebase-auth'
})

const { data, status, signOut } = useAuth()

const workspace = ref({
  name: 'Default Workspace',
})

// Edit mode state
const isEditing = ref(false)
const isLoading = ref(false)
const errors = ref<string[]>([])

// Form data for editing
const editForm = ref({
  email: data.value?.email,
  username: data.value?.name,
})

// Toggle edit mode
const startEditing = () => {
  editForm.value = {
    email: data.value?.email,
    username: data.value?.name,
  }
  isEditing.value = true
  errors.value = []
}

const cancelEditing = () => {
  isEditing.value = false
  errors.value = []
}

const systemLogout = async () => {
  try {
    await signOut( { redirect: true, callbackUrl: '/' } )
  } catch (error) {
    console.error('Logout error:', error)
  }
}

// Handle profile update
const handleUpdate = async () => {
  errors.value = []

  // Client-side validation
  if (!editForm.value.email) {
    errors.value.push('Email is required')
  }
  if (!editForm.value.username) {
    errors.value.push('Username is required')
  }

  if (errors.value.length > 0) {
    return
  }

  isLoading.value = true

  try {
    // TODO: Implement actual profile update API call
    console.log('Profile update attempt:', editForm.value)

    // Simulate API delay
    await new Promise((resolve) => setTimeout(resolve, 1000))

    // TODO: Implement editing Update local user data
    //user.value.email = editForm.value.email
    //user.value.username = editForm.value.username

    isEditing.value = false
  } catch (error) {
    errors.value.push(
      `Profile update failed: ${error instanceof Error ? error.message : 'Please try again.'}`,
    )
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="row">
    <div class="col-lg-8">
      <div class="card">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h4 class="card-title mb-0">Account Information</h4>
          <button
            v-if="!isEditing"
            class="btn btn-outline-primary btn-sm"
            data-test-id="EditProfile"
            @click="startEditing"
          >
            <i class="bi bi-pencil me-1" />
            Edit Profile
          </button>
        </div>
        <div class="card-body">
          <!-- Error Display -->
          <div
            v-if="errors.length > 0"
            class="alert alert-danger"
            data-test-id="ProfileErrors"
          >
            <ul class="mb-0">
              <li
                v-for="error in errors"
                :key="error"
              >
                {{ error }}
              </li>
            </ul>
          </div>

          <!-- View Mode -->
          <div
            v-if="!isEditing"
            data-test-id="AccountInfo"
          >
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Email:</strong>
              </div>
              <div
                class="col-sm-9"
                data-test-id="Email"
              >
                {{ data?.email }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Username:</strong>
              </div>
              <div
                class="col-sm-9"
                data-test-id="Username"
              >
                {{ data?.name }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>ID:</strong>
              </div>
              <div class="col-sm-9">
                {{ data?.id }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Roles:</strong>
              </div>
              <div class="col-sm-9">
                {{ data?.roles?.length ? data.roles.join(', ') : 'None' }}
              </div>
            </div>
          </div>

          <!-- Edit Mode -->
          <form
            v-else
            data-test-id="EditProfileForm"
            @submit.prevent="handleUpdate"
          >
            <div class="mb-3">
              <label
                for="edit-email"
                class="form-label"
                >Email Address</label
              >
              <input
                id="edit-email"
                v-model="editForm.email"
                type="email"
                class="form-control"
                data-test-id="EditEmail"
                :disabled="isLoading"
                required
              />
            </div>
            <div class="mb-3">
              <label
                for="edit-username"
                class="form-label"
                >Username</label
              >
              <input
                id="edit-username"
                v-model="editForm.username"
                type="text"
                class="form-control"
                data-test-id="EditUsername"
                :disabled="isLoading"
                required
              />
            </div>
            <div class="d-flex gap-2">
              <button
                type="submit"
                class="btn btn-primary"
                data-test-id="SaveProfile"
                :disabled="isLoading"
              >
                <span
                  v-if="isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                />
                {{ isLoading ? 'Saving...' : 'Save Changes' }}
              </button>
              <button
                type="button"
                class="btn btn-secondary"
                data-test-id="CancelEdit"
                :disabled="isLoading"
                @click="cancelEditing"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <div class="col-lg-4">
      <!-- Workspace Information -->
      <div class="card mb-4">
        <div class="card-header">
          <h5 class="card-title mb-0">Current Workspace</h5>
        </div>
        <div
          class="card-body"
          data-test-id="WorkspaceInfo"
        >
          <h6 class="text-primary">{{ workspace.name }}</h6>
          <p class="text-muted mb-2">
            <small>Your default workspace for managing financial data</small>
          </p>
          <div class="d-grid">
            <NuxtLink
              to="/"
              class="btn btn-outline-primary btn-sm"
            >
              Go to Workspace
            </NuxtLink>
          </div>
        </div>
      </div>

      <!-- Account Actions -->
      <div class="card">
        <div class="card-header">
          <h5 class="card-title mb-0">Account Actions</h5>
        </div>
        <div class="card-body">
          <div class="d-grid gap-2">
            <button
              class="btn btn-outline-warning btn-sm"
              data-test-id="ChangePassword"
            >
              <i class="bi bi-lock me-1" />
              Change Password
            </button>
            <button
              class="btn btn-outline-info btn-sm"
              data-test-id="ManageWorkspaces"
            >
              <i class="bi bi-building me-1" />
              Manage Workspaces
            </button>
            <hr />
            <button
              class="btn btn-outline-danger btn-sm"
              data-test-id="Logout"
              @click="systemLogout"
            >
              <i class="bi bi-box-arrow-right me-1" />
              Sign Out
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
