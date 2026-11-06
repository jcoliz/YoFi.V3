/**
 * Vitest global setup for frontend component tests.
 *
 * Provides Vue reactivity primitives as globals so that Vue SFCs that rely on
 * Nuxt auto-imports (ref, computed, watch, etc.) work without modification.
 *
 * Nuxt-specific composables (useApiClient, useUserPreferencesStore, navigateTo,
 * useProblemDetails, etc.) are NOT provided here — each test file that needs
 * them must mock them with vi.mock().
 */
import { vi } from 'vitest'
import * as vue from 'vue'

// Vue reactivity and lifecycle — Nuxt auto-imports these in every component
vi.stubGlobal('ref', vue.ref)
vi.stubGlobal('computed', vue.computed)
vi.stubGlobal('watch', vue.watch)
vi.stubGlobal('watchEffect', vue.watchEffect)
vi.stubGlobal('reactive', vue.reactive)
vi.stubGlobal('readonly', vue.readonly)
vi.stubGlobal('shallowRef', vue.shallowRef)
vi.stubGlobal('shallowReactive', vue.shallowReactive)
vi.stubGlobal('toRef', vue.toRef)
vi.stubGlobal('toRefs', vue.toRefs)
vi.stubGlobal('isRef', vue.isRef)
vi.stubGlobal('unref', vue.unref)
vi.stubGlobal('nextTick', vue.nextTick)

// Vue lifecycle hooks — Nuxt auto-imports these too
vi.stubGlobal('onMounted', vue.onMounted)
vi.stubGlobal('onUnmounted', vue.onUnmounted)
vi.stubGlobal('onBeforeMount', vue.onBeforeMount)
vi.stubGlobal('onBeforeUnmount', vue.onBeforeUnmount)
vi.stubGlobal('onUpdated', vue.onUpdated)
vi.stubGlobal('onBeforeUpdate', vue.onBeforeUpdate)

// Vue component utilities
vi.stubGlobal('defineProps', vue.defineProps)
vi.stubGlobal('defineEmits', vue.defineEmits)
vi.stubGlobal('defineExpose', vue.defineExpose)
vi.stubGlobal('withDefaults', vue.withDefaults)
vi.stubGlobal('useSlots', vue.useSlots)
vi.stubGlobal('useAttrs', vue.useAttrs)
vi.stubGlobal('provide', vue.provide)
vi.stubGlobal('inject', vue.inject)
vi.stubGlobal('h', vue.h)
