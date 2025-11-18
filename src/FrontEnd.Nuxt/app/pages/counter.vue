<script setup lang="ts">
import { ref } from "vue";
definePageMeta({
    title: 'Counter',
    order: 3
})

const increaseCount = () => {
    count.value++;
};

const count = ref(0);

</script>

<template>
    <p><output>Current count: {{ count }}</output></p>
    <button class="btn btn-primary" @click="increaseCount" label="Click me">Click me</button>
</template>