import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath } from 'node:url'

/**
 * Vitest configuration for frontend component and utility tests.
 *
 * Uses @vitejs/plugin-vue for Vue SFC support and happy-dom as the DOM
 * environment. Path aliases (~/ and @/) resolve to the app/ directory,
 * matching the Nuxt convention.
 *
 * Vue reactivity primitives (ref, computed, watch, etc.) are provided as
 * globals so that components using Nuxt auto-imports work without changes.
 * Nuxt-specific composables (useApiClient, navigateTo, etc.) must be mocked
 * with vi.mock() in individual test files.
 */
export default defineConfig({
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  plugins: [vue() as any],
  test: {
    environment: 'happy-dom',
    include: ['tests/**/*.test.ts'],
    globals: false,
    setupFiles: ['tests/setup.ts'],
  },
  resolve: {
    alias: {
      '~': fileURLToPath(new URL('./app', import.meta.url)),
      '@': fileURLToPath(new URL('./app', import.meta.url)),
    },
  },
})
