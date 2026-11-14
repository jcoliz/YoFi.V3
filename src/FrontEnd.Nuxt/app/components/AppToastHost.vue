<script setup lang="ts">
/**
 * App Toast Host Component
 *
 * This presentation-only host is mounted once at the app root. The toast
 * store lets unrelated pages publish notifications without coupling to this
 * host's parent layout or component hierarchy.
 */

const toastStore = useToastStore()
const timeouts = new Map<number, ReturnType<typeof setTimeout>>()

/**
 * Remove a toast and cancel its pending automatic dismissal.
 */
function dismiss(id: number) {
  const timeout = timeouts.get(id)
  if (timeout) {
    clearTimeout(timeout)
    timeouts.delete(id)
  }

  toastStore.dismiss(id)
}

/**
 * Start the automatic dismissal timer for a newly displayed toast.
 */
function scheduleDismissal(id: number, duration: number) {
  if (!import.meta.client || timeouts.has(id)) {
    return
  }

  timeouts.set(
    id,
    setTimeout(() => dismiss(id), duration),
  )
}

watch(
  () => toastStore.toasts,
  (toasts) => {
    const activeIds = new Set(toasts.map((toast) => toast.id))

    for (const [id, timeout] of timeouts) {
      if (!activeIds.has(id)) {
        clearTimeout(timeout)
        timeouts.delete(id)
      }
    }

    for (const toast of toasts) {
      scheduleDismissal(toast.id, toast.duration)
    }
  },
  { deep: true },
)

onBeforeUnmount(() => {
  for (const timeout of timeouts.values()) {
    clearTimeout(timeout)
  }
})
</script>

<template>
  <div
    class="toast-container position-fixed top-0 end-0 p-3"
    aria-live="polite"
    aria-atomic="true"
  >
    <TransitionGroup name="toast">
      <div
        v-for="toast in toastStore.toasts"
        :key="toast.id"
        class="toast show"
        :class="`text-bg-${toast.variant}`"
        role="status"
        data-test-id="app-toast"
      >
        <div class="toast-header">
          <strong class="me-auto">{{ toast.title }}</strong>
          <button
            type="button"
            class="btn-close"
            aria-label="Close notification"
            data-test-id="DismissToast"
            @click="dismiss(toast.id)"
          />
        </div>
        <div
          class="toast-body"
          :data-test-id="toast.testId"
        >
          {{ toast.message }}
        </div>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.2s ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateX(1rem);
}
</style>
