<script setup lang="ts">
definePageMeta({
    title: 'Login',
    layout: 'blank'
})

// Reactive form data
const form = ref({
  email: '',
  password: ''
})

// Form validation and error handling
const errors = ref<string[]>([])
const isLoading = ref(false)

// Form submission handler
const handleSubmit = async () => {
  errors.value = []
  
  // Client-side validation
  if (!form.value.email) {
    errors.value.push('Email is required')
  }
  if (!form.value.password) {
    errors.value.push('Password is required')
  }
  
  if (errors.value.length > 0) {
    return
  }
  
  isLoading.value = true
  
  try {
    // TODO: Implement actual login API call
    console.log('Login attempt:', {
      email: form.value.email,
      password: form.value.password
    })
    
    // Simulate API delay
    await new Promise(resolve => setTimeout(resolve, 1000))
    
    // TODO: Handle successful login
    // Should validate credentials, set authentication state, and redirect to workspace
    // For now, simulate different responses based on email
    if (form.value.email === 'baduser@example.com') {
      errors.value.push('Invalid email or password')
      return
    }
    
    // TODO: Navigate to workspace dashboard after successful login
    // await navigateTo('/workspace/dashboard')
    
  } catch (error) {
    errors.value.push('Login failed. Please try again.')
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
          <form @submit.prevent="handleSubmit" data-test-id="LoginForm">
            
            <!-- Error Display -->
            <div v-if="errors.length > 0" class="alert alert-danger" data-test-id="Errors">
              <ul class="mb-0">
                <li v-for="error in errors" :key="error">{{ error }}</li>
              </ul>
            </div>

            <!-- Email Field -->
            <div class="mb-3">
              <label for="email" class="form-label">Email Address</label>
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

            <!-- Password Field -->
            <div class="mb-3">
              <label for="password" class="form-label">Password</label>
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
                <span v-if="isLoading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                {{ isLoading ? 'Signing In...' : 'Sign In' }}
              </button>
            </div>

            <!-- Registration Link -->
            <div class="text-center">
              <p class="mb-0">
                Don't have an account? 
                <NuxtLink to="/register" class="text-decoration-none">Create one here</NuxtLink>
              </p>
            </div>

          </form>
        </div>
      </div>
    </div>
  </div>
</template>
