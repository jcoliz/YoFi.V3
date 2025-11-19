<script setup lang="ts">
definePageMeta({
    title: 'Profile'
})

// TODO: Replace with actual user data from authentication context
const user = ref({
  email: 'testuser@example.com',
  username: 'testuser',
  workspaceName: 'Personal Finance',
  memberSince: '2024-01-15',
  lastLogin: '2024-11-17'
})

// Edit mode state
const isEditing = ref(false)
const isLoading = ref(false)
const errors = ref<string[]>([])

// Form data for editing
const editForm = ref({
  email: user.value.email,
  username: user.value.username
})

// Toggle edit mode
const startEditing = () => {
  editForm.value = {
    email: user.value.email,
    username: user.value.username
  }
  isEditing.value = true
  errors.value = []
}

const cancelEditing = () => {
  isEditing.value = false
  errors.value = []
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
    await new Promise(resolve => setTimeout(resolve, 1000))
    
    // Update local user data
    user.value.email = editForm.value.email
    user.value.username = editForm.value.username
    
    isEditing.value = false
    
  } catch (error) {
    errors.value.push('Profile update failed. Please try again.')
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
            @click="startEditing"
            class="btn btn-outline-primary btn-sm"
            data-test-id="EditProfile"
          >
            <i class="bi bi-pencil me-1"></i>
            Edit Profile
          </button>
        </div>
        <div class="card-body">
          
          <!-- Error Display -->
          <div v-if="errors.length > 0" class="alert alert-danger" data-test-id="ProfileErrors">
            <ul class="mb-0">
              <li v-for="error in errors" :key="error">{{ error }}</li>
            </ul>
          </div>

          <!-- View Mode -->
          <div v-if="!isEditing" data-test-id="AccountInfo">
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Email:</strong>
              </div>
              <div class="col-sm-9" data-test-id="Email">
                {{ user.email }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Username:</strong>
              </div>
              <div class="col-sm-9" data-test-id="Username">
                {{ user.username }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Member Since:</strong>
              </div>
              <div class="col-sm-9">
                {{ new Date(user.memberSince).toLocaleDateString() }}
              </div>
            </div>
            <div class="row mb-3">
              <div class="col-sm-3">
                <strong>Last Login:</strong>
              </div>
              <div class="col-sm-9">
                {{ new Date(user.lastLogin).toLocaleDateString() }}
              </div>
            </div>
          </div>

          <!-- Edit Mode -->
          <form v-else @submit.prevent="handleUpdate" data-test-id="EditProfileForm">
            <div class="mb-3">
              <label for="edit-email" class="form-label">Email Address</label>
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
              <label for="edit-username" class="form-label">Username</label>
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
                <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                {{ isLoading ? 'Saving...' : 'Save Changes' }}
              </button>
              <button
                type="button"
                @click="cancelEditing"
                class="btn btn-secondary"
                data-test-id="CancelEdit"
                :disabled="isLoading"
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
        <div class="card-body" data-test-id="WorkspaceInfo">
          <h6 class="text-primary">{{ user.workspaceName }}</h6>
          <p class="text-muted mb-2">
            <small>Your default workspace for managing financial data</small>
          </p>
          <div class="d-grid">
            <NuxtLink to="/" class="btn btn-outline-primary btn-sm">
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
            <button class="btn btn-outline-warning btn-sm" data-test-id="ChangePassword">
              <i class="bi bi-lock me-1"></i>
              Change Password
            </button>
            <button class="btn btn-outline-info btn-sm" data-test-id="ManageWorkspaces">
              <i class="bi bi-building me-1"></i>
              Manage Workspaces
            </button>
            <hr>
            <button class="btn btn-outline-danger btn-sm" data-test-id="Logout">
              <i class="bi bi-box-arrow-right me-1"></i>
              Sign Out
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
