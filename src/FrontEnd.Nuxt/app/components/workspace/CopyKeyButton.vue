<script setup lang="ts">
withDefaults(
  defineProps<{
    workspaceKey: string
    compact?: boolean
  }>(),
  { compact: false },
)
</script>

<template>
  <CopyTextButton
    :value="workspaceKey"
    label="workspace key"
    :compact="compact"
    test-id-prefix="copy-workspace-key"
  />
</template>
