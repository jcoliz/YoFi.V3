<script setup lang="ts">
/**
 * Page Breadcrumb Component
 *
 * Displays a Bootstrap-styled breadcrumb navigation bar for child pages.
 * Breadcrumb items are driven by the current route's `meta.breadcrumbs` array.
 * The current page title (from `meta.title`) is shown as the active (last) item.
 *
 * Each breadcrumb entry in `meta.breadcrumbs` should have:
 *   - title: string — display text
 *   - to: string — route path (used as NuxtLink target)
 */

const route = useRoute()

interface BreadcrumbItem {
  title: string
  to: string
}

const breadcrumbs = computed(() => (route.meta.breadcrumbs as BreadcrumbItem[]) ?? [])
const currentTitle = computed(() => (route.meta.title as string) ?? '')
</script>
<template>
  <nav
    v-if="breadcrumbs.length"
    aria-label="breadcrumb"
  >
    <ol class="breadcrumb">
      <li
        v-for="item in breadcrumbs"
        :key="item.to"
        class="breadcrumb-item"
      >
        <NuxtLink :to="item.to">
          {{ item.title }}
        </NuxtLink>
      </li>
      <li
        class="breadcrumb-item active"
        aria-current="page"
      >
        {{ currentTitle }}
      </li>
    </ol>
  </nav>
</template>
