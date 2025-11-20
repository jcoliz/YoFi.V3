<script setup lang="ts">
/**
 * Login sate control
 *
 * Shows current state of logged-in user, and allows login/logout,
 * and navigation to profile page.
 */

console.log('[DEBUG] LoginState: Component loading')

// Fake login state for demo purposes

const account = ref(true)
const name = ref('__TEST__0001')
const photo = ref('')

console.log('[DEBUG] LoginState: Account state:', account.value)
console.log('[DEBUG] LoginState: Will render FeatherIcon:', !account.value)

function systemLogin() {
  navigateTo('/login')
}

function systemLogout() {
  account.value = false
  name.value = ''

  navigateTo('/')
}
</script>

<template>
  <DropDownPortable
    class="ms-2 my-1 d-flex align-items-middle"
    data-test-id="login-state"
  >
    <template #trigger>
      <a
        class="d-flex align-items-center link-body-emphasis text-decoration-none p-0 dropdown-toggle"
        data-bs-toggle="dropdown"
        aria-expanded="false"
      >
        <template v-if="account">
          <img
            v-if="photo"
            :src="photo"
            alt=""
            width="32"
            height="32"
            class="rounded-circle me-2"
          />
          <strong
            class="me-2"
            data-test-id="username"
            >{{ name }}</strong
          >
        </template>
        <FeatherIcon
          icon="user"
          size="24"
          class="rounded-circle me-2"
        />
      </a>
    </template>
    <template #default>
      <!-- Note that popper is handling absolute positioning of the drop-down -->
      <ul class="dropdown-menu dropdown-menu-end text-small shadow">
        <template v-if="account">
          <li>
            <NuxtLink
              class="dropdown-item"
              to="/profile"
              data-test-id="Profile"
              >Profile</NuxtLink
            >
          </li>
          <li><hr class="dropdown-divider" /></li>
          <li>
            <a
              class="dropdown-item"
              data-test-id="SignOut"
              @click="systemLogout"
              >Sign Out</a
            >
          </li>
        </template>
        <template v-else>
          <li>
            <a
              class="dropdown-item"
              data-test-id="SignIn"
              @click="systemLogin"
              >Sign In</a
            >
          </li>
          <li><hr class="dropdown-divider" /></li>
          <li>
            <NuxtLink
              class="dropdown-item"
              to="/register"
              data-test-id="CreateAccount"
              >Create Account</NuxtLink
            >
          </li>
          <li><hr class="dropdown-divider" /></li>
          <!-- Just for now! -->
          <li>
            <NuxtLink
              class="dropdown-item"
              to="/profile"
              data-test-id="ProfileTemp"
              >Profile</NuxtLink
            >
          </li>
        </template>
      </ul>
    </template>
  </DropDownPortable>
</template>
