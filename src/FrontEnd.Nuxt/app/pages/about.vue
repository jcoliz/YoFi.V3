<script setup lang="ts">
useHead({
  title: 'About',
});
const { baseUrl } = useApiBaseUrl();
</script>
<template>
    <h1>About</h1>
    <p>Testing the ability for <strong>static API base URLs</strong></p>
    <p>apiBaseUrl:  {{ baseUrl }}</p>
</template>
