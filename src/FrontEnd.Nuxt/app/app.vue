<script setup lang="ts">
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
