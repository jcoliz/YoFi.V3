<script setup lang="ts">
/**
 * A Page Header using the current route title for its text
 *
 * If no title is defined, will not display
 */

const route = useRoute()
console.log('[DEBUG] RoutePageHeader: Mounted')
console.log('[DEBUG] RoutePageHeader: Route meta title:', route.meta.title)
console.log('[DEBUG] RoutePageHeader: Will render BasePageHeader:', !!route.meta.title)
</script>
<template>
  <BasePageHeader v-if="$route.meta.title">{{ $route.meta.title }}</BasePageHeader>
</template>
