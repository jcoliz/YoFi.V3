<script setup lang="ts">
/**
 * Registration Page
 *
 * User account registration page with email, username, and password validation.
 * Displays success message after registration and prevents already-authenticated users
 * from accessing. Includes password strength validation and confirmation matching.
 */

import type { IProblemDetails } from '~/utils/apiclient'

definePageMeta({
  title: 'Register',
  layout: 'blank',
  auth: {
    unauthenticatedOnly: true,
    navigateAuthenticatedTo: '/profile',
  },
})

const { signUp } = useAuth()

// Reactive form data
const form = ref({
  email: '',
  username: '',
  password: '',
  passwordAgain: '',
})
const response = ref()

// Form validation and error handling
const errorProblem = ref<IProblemDetails | undefined>()
const showError = ref(false)
const isLoading = ref(false)
const isRegistered = ref(false)

// Form submission handler
const handleSubmit = async () => {
  errorProblem.value = undefined
  showError.value = false
  const validationErrors: string[] = []

  // Client-side validation
  if (!form.value.email) {
    validationErrors.push('Email is required')
  }
  if (!form.value.username) {
    validationErrors.push('Username is required')
  }
  if (!form.value.password) {
    validationErrors.push('Password is required')
  }
  if (form.value.password !== form.value.passwordAgain) {
    validationErrors.push('Passwords do not match')
  }

  if (validationErrors.length > 0) {
    // Create ad-hoc problem details for validation errors
    errorProblem.value = {
      title: 'Please fix the following errors:',
      detail: validationErrors.join(', '),
    }
    showError.value = true
    return
  }

  isLoading.value = true

  try {
    isLoading.value = true
    response.value = await signUp(
      {
        username: form.value.username,
        email: form.value.email,
        password: form.value.password,
      },
      { preventLoginFlow: true },
    )
  } catch (error: any) {
    console.error('*** Registration error:')
    console.log('- Status:', error.status)
    console.log('- Message:', error.message)
    console.log('- Data:', error.data)
    console.log('- Full error object:', error)

    // If the API returned a problem details object in error.data, use it
    if (error.data && typeof error.data === 'object' && 'title' in error.data) {
      errorProblem.value = error.data as IProblemDetails
    } else {
      // Otherwise, compose an ad-hoc problem details object
      errorProblem.value = {
        title: 'Registration failed',
        detail: error.message ?? 'Please try again',
        status: error.status,
      }
    }
    showError.value = true
  } finally {
    isLoading.value = false
  }
}

// Check password strength
const isWeakPassword = computed(() => {
  return form.value.password.length > 0 && form.value.password.length < 8
})
</script>

<template>
  <div class="row justify-content-center">
    <div class="col-md-6 col-lg-4">
      <div class="card shadow">
        <div class="card-header text-center">
          <h3 class="card-title mb-0">
            {{ isRegistered ? 'Thanks!' : 'Request Account' }}
          </h3>
        </div>
        <div class="card-body">
          <!-- Success Message -->
          <div
            v-if="response && !response.error"
            class="text-center"
            data-test-id="SuccessMessage"
          >
            <div class="mb-4">
              <div class="text-success mb-3">
                <i class="bi bi-check-circle-fill fs-1"></i>
              </div>
              <h5
                data-test-id="display-username"
                class="mb-3"
              >
                {{ form.username }}
              </h5>
              <p
                data-test-id="display-email"
                class="text-muted mb-4"
              >
                {{ form.email }}
              </p>
              <p class="text-muted mb-4">
                Thanks for requesting an account. A member of our team will review your request and
                get back to you shortly.
              </p>
            </div>

            <div class="d-grid mb-3">
              <NuxtLink
                to="/login"
                class="btn btn-primary"
                data-test-id="ContinueButton"
              >
                Continue
              </NuxtLink>
            </div>
          </div>

          <!-- Registration Form -->
          <form
            v-else
            data-test-id="RegisterForm"
            @submit.prevent="handleSubmit"
          >
            <!-- Error Display -->
            <ErrorDisplay
              v-model:show="showError"
              :problem="errorProblem"
              data-test-id="Errors"
            />

            <!-- Email Field -->
            <div class="mb-3">
              <label
                for="email"
                class="form-label"
                >Email Address</label
              >
              <input
                id="email"
                v-model="form.email"
                type="email"
                class="form-control"
                data-test-id="email"
                placeholder="Enter your email"
                :disabled="isLoading"
                required
              />
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
                placeholder="Choose a username"
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
                :class="{ 'is-invalid': isWeakPassword }"
                data-test-id="password"
                placeholder="Enter a secure password"
                :disabled="isLoading"
                required
              />
              <div
                v-if="isWeakPassword"
                class="invalid-feedback"
              >
                Password must be at least 8 characters long
              </div>
            </div>

            <!-- Confirm Password Field -->
            <div class="mb-3">
              <label
                for="password-again"
                class="form-label"
                >Confirm Password</label
              >
              <input
                id="password-again"
                v-model="form.passwordAgain"
                type="password"
                class="form-control"
                :class="{
                  'is-invalid': form.passwordAgain && form.password !== form.passwordAgain,
                }"
                data-test-id="password-again"
                placeholder="Confirm your password"
                :disabled="isLoading"
                required
              />
              <div
                v-if="form.passwordAgain && form.password !== form.passwordAgain"
                class="invalid-feedback"
              >
                Passwords do not match
              </div>
            </div>

            <!-- Submit Button -->
            <div class="d-grid mb-3">
              <button
                type="submit"
                class="btn btn-primary"
                data-test-id="Register"
                :disabled="isLoading"
              >
                <output
                  v-if="isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  aria-hidden="true"
                />
                {{ isLoading ? 'Requesting Account...' : 'Request Account' }}
              </button>
            </div>

            <!-- Login Link -->
            <div class="text-center">
              <p class="mb-0">
                Already have an account?
                <NuxtLink
                  to="/login"
                  class="text-decoration-none"
                  data-test-id="sign-in-link"
                  >Sign in here</NuxtLink
                >
              </p>
            </div>
          </form>
        </div>
      </div>
    </div>
  </div>
</template>
