<script setup lang="ts">
/**
 * Root Application Component
 *
 * The main application wrapper that sets up the layout system and page titles.
 * Dynamically updates the document title based on the current route's meta.title.
 */

const appConfig = useAppConfig()
const route = useRoute()

// Create a reactive computed title
const pageTitle = computed(() => {
  return route.meta.title ? `${route.meta.title} - ${appConfig.name}` : appConfig.name
})

useHead({
  htmlAttrs: {
    lang: 'en',
  },
  title: pageTitle,
})
</script>
<template>
  <div>
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
