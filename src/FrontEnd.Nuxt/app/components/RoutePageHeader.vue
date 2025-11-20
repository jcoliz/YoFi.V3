<script setup lang="ts">
/**
 * A Page Header using the current route title for its text
 *
 * If no title is defined, will not display
 */
</script>
<template>
  <BasePageHeader v-if="$route.meta.title">{{ $route.meta.title }}</BasePageHeader>
</template>
