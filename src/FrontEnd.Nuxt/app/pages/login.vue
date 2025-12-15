<script setup lang="ts">
/**
 * Login Page
 *
 * User authentication page with username/password login form.
 * Handles client-side validation, displays error messages using RFC 7807 Problem Details,
 * and redirects authenticated users to their profile.
 */

import type { IProblemDetails } from '~/utils/apiclient'

definePageMeta({
  title: 'Login',
  layout: 'blank',
  auth: false, // Using login-redirect middleware instead
})

const { signIn } = useAuth()
const route = useRoute()

// Reactive form data
const form = ref({
  username: '',
  password: '',
})

// Form validation and error handling
const errorProblem = ref<IProblemDetails | undefined>()
const showErrors = ref(false)
const isLoading = ref(false)

// Form submission handler
const handleSubmit = async () => {
  errorProblem.value = undefined
  showErrors.value = false

  // Client-side validation
  const validationErrors: string[] = []
  if (!form.value.username) {
    validationErrors.push('Username is required')
  }
  if (!form.value.password) {
    validationErrors.push('Password is required')
  }

  if (validationErrors.length > 0) {
    // Create ad-hoc problem details for validation errors
    errorProblem.value = {
      title: 'Please fix the following errors:',
      detail: validationErrors.join(', '),
    }
    showErrors.value = true
    return
  }

  isLoading.value = true

  try {
    isLoading.value = true

    // Use the redirect query param if available, otherwise default to '/'
    const callbackUrl = (route.query.redirect as string) || '/'

    await signIn(
      {
        username: form.value.username,
        password: form.value.password,
      },
      {
        redirect: true,
        callbackUrl: callbackUrl,
      },
    )
  } catch (error: any) {
    console.error('*** Login error:')
    console.log('- Status:', error.status)
    console.log('- Message:', error.message)
    console.log('- Data:', error.data)
    console.log('- Full error object:', error)

    // TODO: Now that we are directly displaying the error details, need to soften the
    // language a bit for end users.
    // If the API returned a problem details object in error.data, use it
    if (error.data && typeof error.data === 'object' && 'title' in error.data) {
      errorProblem.value = error.data as IProblemDetails
    } else {
      // Otherwise, compose an ad-hoc problem details object
      errorProblem.value = {
        title: 'Login failed',
        detail: error.message ?? 'Please check your credentials',
        status: error.status,
      }
    }
    showErrors.value = true
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
            <ErrorDisplay
              v-model:show="showErrors"
              :problem="errorProblem"
            />

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
                  data-test-id="create-account-link"
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
