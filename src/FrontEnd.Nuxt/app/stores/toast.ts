import { defineStore } from 'pinia'

/**
 * App-level notifications are published by unrelated pages and components.
 * A transient store avoids passing notification events through layouts while
 * keeping the reusable host presentation-only. Toasts are deliberately not
 * persisted because they should not reappear after a reload.
 */

/**
 * The supported visual styles for app-level toast notifications.
 */
export type ToastVariant = 'success' | 'info' | 'warning' | 'danger'

/**
 * A notification displayed by the app-level toast host.
 */
export interface AppToast {
  id: number
  title: string
  message: string
  variant: ToastVariant
  duration: number
}

/**
 * Options used to add an app-level toast notification.
 */
export interface ToastOptions {
  title: string
  message: string
  variant?: ToastVariant
  duration?: number
}

/**
 * Holds transient notifications for the reusable app-level toast host.
 */
export const useToastStore = defineStore('toast', () => {
  const toasts = ref<AppToast[]>([])
  let nextId = 0

  /**
   * Add a notification to the toast host.
   *
   * @param options The notification content and display options.
   * @returns The identifier of the added notification.
   */
  function add(options: ToastOptions): number {
    const id = nextId++
    toasts.value.push({
      id,
      title: options.title,
      message: options.message,
      variant: options.variant ?? 'info',
      duration: options.duration ?? 5000,
    })

    return id
  }

  /**
   * Add a success notification to the toast host.
   *
   * @param message The notification body.
   * @param title The notification title.
   * @returns The identifier of the added notification.
   */
  function success(message: string, title = 'Success'): number {
    return add({ title, message, variant: 'success' })
  }

  /**
   * Remove a notification from the toast host.
   *
   * @param id The notification identifier.
   */
  function dismiss(id: number) {
    toasts.value = toasts.value.filter((toast) => toast.id !== id)
  }

  return { toasts, add, success, dismiss }
})
