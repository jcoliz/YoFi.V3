<template>
    <h1>About</h1>
    <p>Testing the ability for <strong>static API base URLs</strong></p>
    <p>apiBaseUrl:  {{ config.public.apiBaseUrl }}</p>
    <p>services__backend__http__0: {{ config.public.services__backend__http__0 }}</p>
    <p>finalApiBaseUrl: {{ config.public.finalApiBaseUrl }}</p>
</template>
<script setup lang="ts">
useHead({
  title: 'About',
});
const config = useRuntimeConfig()
console.log('API Base URL:', config.public.apiBaseUrl)
</script>
