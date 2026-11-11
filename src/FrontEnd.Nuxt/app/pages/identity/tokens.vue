<script setup lang="ts">
import { jwtDecode, type JwtHeader, type JwtPayload } from 'jwt-decode'

definePageMeta({
  title: 'Tokens',
  breadcrumbs: [{ title: 'Profile', to: '/profile' }],
})

useAuthenticationGuard().requireAuthenticatedUser()

const { token, refreshToken, refresh, status } = useAuth()
const errors = useProblemDetails()
const isRefreshing = ref(false)

const cleanToken = computed(() => token.value?.replace(/^Bearer /i, '') || null)
const decodedToken = computed(() => {
  if (!cleanToken.value) return null
  try {
    return jwtDecode<JwtPayload & Record<string, unknown>>(cleanToken.value)
  } catch {
    return null
  }
})
const decodedHeader = computed(() => {
  if (!cleanToken.value) return null
  try {
    return jwtDecode<JwtHeader>(cleanToken.value, { header: true })
  } catch {
    return null
  }
})
const tokenStatus = computed(() => {
  const exp = decodedToken.value?.exp
  if (typeof exp !== 'number' || !Number.isFinite(exp)) return 'No expiration'
  return Date.now() >= exp * 1000 ? 'Expired' : 'Not expired'
})

const claimDescriptions: Record<string, string> = {
  iss: 'Issuer - who issued the token',
  sub: 'Subject - who the token is about',
  aud: 'Audience - who the token is intended for',
  exp: 'Expiration time - when the token expires',
  iat: 'Issued at - when the token was issued',
  nbf: 'Not before - when the token becomes valid',
  jti: 'JWT ID - unique identifier for the token',
  name: 'Full name of the user',
  email: 'Email address of the user',
  role: "User's account roles",
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': "User's account roles",
  scope: 'Access scopes granted to the token',
}

function formatTimestamp(value: unknown) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'N/A'
  const date = new Date(value * 1000)
  return Number.isNaN(date.getTime()) ? 'N/A' : date.toLocaleString()
}

async function copyToClipboard(value: string) {
  errors.clearError()
  try {
    if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable')
    await navigator.clipboard.writeText(value)
  } catch {
    errors.setError({
      title: 'Copy failed',
      detail: 'Could not copy the token to the clipboard.',
    })
  }
}

async function handleRefresh() {
  isRefreshing.value = true
  errors.clearError()
  try {
    await refresh()
  } catch (error) {
    await errors.handleApiError(error, 'Refresh Token Failed', 'Failed to refresh token')
  } finally {
    isRefreshing.value = false
  }
}
</script>

<template>
  <ProblemDetailsViewer />

  <div
    v-if="!cleanToken"
    class="alert alert-warning"
    role="alert"
  >
    No JWT token found.
  </div>
  <div
    v-else-if="!decodedToken"
    class="alert alert-danger"
    role="alert"
  >
    Failed to decode JWT token. Token may be malformed.
  </div>
  <template v-else>
    <InfoCard
      title="Token Information"
      class="mb-4 mt-3"
    >
      <div class="row">
        <div class="col-md-6"><strong>Status:</strong> {{ tokenStatus }}</div>
        <div class="col-md-6"><strong>Algorithm:</strong> {{ decodedHeader?.alg || 'N/A' }}</div>
        <div class="col-md-6">
          <strong>Issued At:</strong> {{ formatTimestamp(decodedToken.iat) }}
        </div>
        <div class="col-md-6">
          <strong>Expires:</strong> {{ formatTimestamp(decodedToken.exp) }}
        </div>
      </div>
      <small class="text-muted">Decoded locally; the token signature is not verified.</small>
    </InfoCard>

    <InfoCard
      title="JWT Claims"
      class="mb-4"
    >
      <div class="table-responsive">
        <table class="table table-striped">
          <thead>
            <tr>
              <th scope="col">Claim</th>
              <th scope="col">Value</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="[key, value] in Object.entries(decodedToken)"
              :key="key"
            >
              <td>
                <code>{{ key }}</code>
                <small
                  v-if="claimDescriptions[key]"
                  class="text-muted d-block"
                  >{{ claimDescriptions[key] }}</small
                >
              </td>
              <td>
                <ul
                  v-if="Array.isArray(value)"
                  class="mb-0 ps-3"
                >
                  <li
                    v-for="(item, index) in value"
                    :key="index"
                  >
                    {{ typeof item === 'object' ? JSON.stringify(item) : item }}
                  </li>
                </ul>
                <pre
                  v-else-if="typeof value === 'object'"
                  class="mb-0"
                  >{{ JSON.stringify(value, null, 2) }}</pre>
                <span v-else-if="key === 'exp' || key === 'iat' || key === 'nbf'">
                  <code>{{ value }}</code>
                  <small class="text-muted d-block">{{ formatTimestamp(value) }}</small>
                </span>
                <span v-else>{{ value }}</span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </InfoCard>

    <InfoCard
      title="Raw JWT Token"
      class="mb-4"
    >
      <div class="input-group">
        <textarea
          aria-label="Raw JWT token"
          class="form-control font-monospace"
          :value="cleanToken"
          readonly
          rows="4"
        />
        <button
          class="btn btn-outline-secondary"
          type="button"
          title="Copy token"
          @click="copyToClipboard(cleanToken!)"
        >
          Copy
        </button>
      </div>
    </InfoCard>
  </template>

  <InfoCard class="mb-4">
    <template #header>
      <div class="d-flex align-items-center justify-content-between">
        <h5 class="card-title mb-0">Refresh Token</h5>
        <button
          class="btn btn-outline-primary btn-sm"
          type="button"
          data-test-id="refresh-tokens"
          :disabled="isRefreshing || status === 'loading'"
          @click="handleRefresh"
        >
          <span
            v-if="isRefreshing"
            class="spinner-border spinner-border-sm me-1"
            role="status"
            aria-hidden="true"
          />
          {{ isRefreshing ? 'Refreshing...' : 'Refresh Tokens' }}
        </button>
      </div>
    </template>
    <div
      v-if="refreshToken"
      class="input-group"
    >
      <textarea
        aria-label="Refresh token"
        class="form-control font-monospace"
        :value="refreshToken"
        readonly
        rows="3"
      />
      <button
        class="btn btn-outline-secondary"
        type="button"
        title="Copy refresh token"
        @click="copyToClipboard(refreshToken)"
      >
        Copy
      </button>
    </div>
    <div
      v-else
      class="text-muted"
    >
      No refresh token available
    </div>
  </InfoCard>
</template>

<style scoped>
.font-monospace,
pre {
  font-size: 0.875rem;
}

textarea {
  resize: none;
}

pre {
  max-height: 200px;
  overflow: auto;
}
</style>
