<template>
  <div>
    <NuxtLayout>
      <NuxtPage/>
    </NuxtLayout>
  </div>
</template>
<script setup lang="ts">
useHead({
  htmlAttrs: {
    lang: "en"
  },
  titleTemplate: (titleChunk) => {
    return titleChunk ? `${titleChunk} - YoFi.V3` : 'YoFi.V3';
  },  
});
</script>