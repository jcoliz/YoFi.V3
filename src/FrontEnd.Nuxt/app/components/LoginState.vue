<script setup lang="ts">
/**
 * Login sate control
 *
 * Shows current state of logged-in user, and allows login/logout,
 * and navigation to profile page.
 */

const { data, status, signOut } = useAuth()

const { clearPreferences } = useUserPreferencesStore()

const ready = ref(false)
onMounted(() => {
  ready.value = true
})

function systemLogin() {
  navigateTo('/login')
}

const systemLogout = async () => {
  try {
    clearPreferences()
    await signOut({ redirect: true, callbackUrl: '/' })
  } catch (error) {
    console.error('Logout error:', error)
  }
}
</script>

<template>
  <DropDownPortable
    class="ms-2 my-1 d-flex align-items-center"
    data-test-id="login-state"
  >
    <template #trigger>
      <a
        class="d-flex align-items-center link-body-emphasis text-decoration-none p-0 dropdown-toggle"
        data-bs-toggle="dropdown"
        aria-expanded="false"
      >
        <template v-if="data">
          <strong
            class="me-2"
            data-test-id="username"
            >{{ data.name }}</strong
          >
        </template>
        <FeatherIcon
          icon="user"
          size="24"
          class="rounded-circle me-2"
          :class="ready ? 'text-body' : 'text-primary'"
        />
        <!-- AB#1981: Adding some extra state indication via color
              for visual debugging. If the icon shows green in screen shots
              we know we didnt wait long enough for the page to hydrate. -->
      </a>
    </template>
    <template #default>
      <!-- Note that popper is handling absolute positioning of the drop-down -->
      <ul class="dropdown-menu dropdown-menu-end text-small shadow">
        <template v-if="status === 'authenticated'">
          <!-- Workspace slot - inject custom content -->
          <slot />

          <li v-if="$slots.default"><hr class="dropdown-divider" /></li>

          <!-- User Actions -->
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
              >Request Account</NuxtLink
            >
          </li>
        </template>
      </ul>
    </template>
  </DropDownPortable>
</template>
