<template>
    <h1>About</h1>
    <p>Testing the ability for <strong>static API base URLs</strong></p>
    <p>Using backend API at {{ config.public.apiBaseUrl }}</p>
</template>
<script setup lang="ts">
useHead({
  title: 'About',
});
const config = useRuntimeConfig()
console.log('API Base URL:', config.public.apiBaseUrl)
</script>
