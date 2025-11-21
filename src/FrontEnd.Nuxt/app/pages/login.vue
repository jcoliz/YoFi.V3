<script setup lang="ts">
definePageMeta({
  title: 'Login',
  layout: 'blank',
})

const { signIn } = useAuth()

// Reactive form data
const form = ref({
  username: '',
  password: '',
})

// Form validation and error handling
const errors = ref<string[]>([])
const isLoading = ref(false)

// Form submission handler
const handleSubmit = async () => {
  errors.value = []

  // Client-side validation
  if (!form.value.username) {
    errors.value.push('Username is required')
  }
  if (!form.value.password) {
    errors.value.push('Password is required')
  }

  if (errors.value.length > 0) {
    return
  }

  isLoading.value = true

  try {
    isLoading.value = true
    await signIn(
      {
        username: form.value.username,
        password: form.value.password,
      },
      {
        redirect: true,
        callbackUrl: '/',
      },
    )
  } catch (error: any) {
    console.error('*** Login error:')
    console.log('- Status:', error.status)
    console.log('- Message:', error.message)
    console.log('- Data:', error.data)
    console.log('- Full error object:', error)

    // Handle ProblemDetails format
    const title = error.data?.title ?? 'Login failed'
    const detail = error.data?.detail ?? error.message ?? 'Please check your credentials'
    errors.value = [`${title}: ${detail}`]
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="row justify-content-center">
    <div class="col-md-6 col-lg-4">
      <div class="card shadow">
        <div class="card-header text-center">
          <h3 class="card-title mb-0">Sign In</h3>
        </div>
        <div class="card-body">
          <form
            data-test-id="LoginForm"
            @submit.prevent="handleSubmit"
          >
            <!-- Error Display -->
            <div
              v-if="errors.length > 0"
              class="alert alert-danger alert-dismissible fade show"
              role="alert"
              data-test-id="error-display"
            >
              <strong>Please fix the following errors:</strong><br />
              <span
                v-for="error in errors"
                :key="error"
              >
                {{ error }}
              </span>
              <button
                type="button"
                class="btn-close"
                data-bs-dismiss="alert"
                aria-label="Close"
                @click="errors = []"
              ></button>
            </div>

            <!-- Username Field -->
            <div class="mb-3">
              <label
                for="username"
                class="form-label"
                >Username</label
              >
              <input
                id="username"
                v-model="form.username"
                type="text"
                class="form-control"
                data-test-id="username"
                placeholder="Enter your username"
                :disabled="isLoading"
                required
              />
            </div>

            <!-- Password Field -->
            <div class="mb-3">
              <label
                for="password"
                class="form-label"
                >Password</label
              >
              <input
                id="password"
                v-model="form.password"
                type="password"
                class="form-control"
                data-test-id="password"
                placeholder="Enter your password"
                :disabled="isLoading"
                required
              />
            </div>

            <!-- Submit Button -->
            <div class="d-grid mb-3">
              <button
                type="submit"
                class="btn btn-primary"
                data-test-id="Login"
                :disabled="isLoading"
              >
                <span
                  v-if="isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                />
                {{ isLoading ? 'Signing In...' : 'Sign In' }}
              </button>
            </div>

            <!-- Registration Link -->
            <div class="text-center">
              <p class="mb-0">
                Don't have an account?
                <NuxtLink
                  to="/register"
                  class="text-decoration-none"
                  >Request one here</NuxtLink
                >
              </p>
            </div>
          </form>
        </div>
      </div>
    </div>
  </div>
</template>
