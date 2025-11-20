<script setup lang="ts">
const appConfig = useAppConfig()
const route = useRoute()

// Debug: Log app initialization
console.log('[DEBUG] App.vue: Initializing app')
console.log('[DEBUG] App.vue: Route path:', route.path)
console.log('[DEBUG] App.vue: Route meta:', route.meta)

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
