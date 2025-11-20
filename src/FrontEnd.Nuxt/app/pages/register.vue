<script setup lang="ts">
definePageMeta({
  title: 'Register',
  layout: 'blank',
})

// Reactive form data
const form = ref({
  email: '',
  username: '',
  password: '',
  passwordAgain: '',
})

// Form validation and error handling
const errors = ref<string[]>([])
const isLoading = ref(false)
const isRegistered = ref(false)

// Form submission handler
const handleSubmit = async () => {
  errors.value = []

  // Client-side validation
  if (!form.value.email) {
    errors.value.push('Email is required')
  }
  if (!form.value.username) {
    errors.value.push('Username is required')
  }
  if (!form.value.password) {
    errors.value.push('Password is required')
  }
  if (form.value.password !== form.value.passwordAgain) {
    errors.value.push('Passwords do not match')
  }

  if (errors.value.length > 0) {
    return
  }

  isLoading.value = true

  try {
    // TODO: Implement actual registration API call
    console.log('Registration attempt:', {
      email: form.value.email,
      username: form.value.username,
      password: form.value.password,
    })

    // Simulate API delay
    await new Promise((resolve) => setTimeout(resolve, 1000))

    // Set success state
    isRegistered.value = true
  } catch (error) {
    errors.value.push(
      `Registration failed: ${error instanceof Error ? error.message : 'Please try again.'}`,
    )
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
            {{ isRegistered ? 'Welcome!' : 'Create Account' }}
          </h3>
        </div>
        <div class="card-body">
          <!-- Success Message -->
          <div
            v-if="isRegistered"
            class="text-center"
            data-test-id="SuccessMessage"
          >
            <div class="mb-4">
              <div class="text-success mb-3">
                <i class="bi bi-check-circle-fill fs-1"></i>
              </div>
              <h5 data-test-id="display-username" class="mb-3">{{  form.username }}</h5>
              <p data-test-id="display-email" class="text-muted mb-4">
                {{ form.email }}
              </p>
              <p class="text-muted mb-4">
                Welcome to YoFi! Your account has been created and you're ready to get started.
              </p>
            </div>
            
            <div class="d-grid mb-3">
              <NuxtLink
                to="/profile"
                class="btn btn-primary"
                data-test-id="GoToProfile"
              >
                Continue to Profile
              </NuxtLink>
            </div>
            
            <div class="text-center">
              <p class="mb-0">
                <NuxtLink
                  to="/login"
                  class="text-decoration-none"
                  >Or sign in to existing account</NuxtLink
                >
              </p>
            </div>
          </div>

          <!-- Registration Form -->
          <form
            v-else
            data-test-id="RegisterForm"
            @submit.prevent="handleSubmit"
          >
            <!-- Error Display -->
            <div
              v-if="errors.length > 0"
              class="alert alert-danger"
              data-test-id="Errors"
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
                <span
                  v-if="isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                />
                {{ isLoading ? 'Creating Account...' : 'Create Account' }}
              </button>
            </div>

            <!-- Login Link -->
            <div class="text-center">
              <p class="mb-0">
                Already have an account?
                <NuxtLink
                  to="/login"
                  class="text-decoration-none"
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
