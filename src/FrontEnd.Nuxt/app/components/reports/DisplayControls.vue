<script setup lang="ts">
import type { ReportDefinitionDto } from '~/utils/apiclient'
import { MAX_REPORT_DISPLAY_DEPTH } from '~/utils/reportDisplay'

/** Edits the category-depth and month-display settings for one report. */
const props = withDefaults(
  defineProps<{
    definition: ReportDefinitionDto
    displayDepth: number
    showMonths: boolean
    disabled?: boolean
  }>(),
  { disabled: false },
)

const emit = defineEmits<{
  'update:displayDepth': [number]
  'update:showMonths': [boolean]
  reset: []
}>()

const depthOptions = Array.from({ length: MAX_REPORT_DISPLAY_DEPTH }, (_, index) => index + 1)

function updateDepth(event: Event) {
  emit('update:displayDepth', Number((event.target as HTMLSelectElement).value))
}

function updatePeriod(event: Event) {
  emit('update:showMonths', (event.target as HTMLSelectElement).value === 'true')
}
</script>

<template>
  <div
    class="row g-3 mb-4"
    data-test-id="report-display-controls"
  >
    <div class="col-md-4">
      <label
        for="report-depth-selection"
        class="form-label"
        >Depth</label
      >
      <select
        id="report-depth-selection"
        class="form-select"
        :value="props.displayDepth"
        :disabled="props.disabled"
        data-test-id="report-depth-selector"
        @change="updateDepth"
      >
        <option
          v-for="depth in depthOptions"
          :key="depth"
          :value="depth"
        >
          {{ depth }}
        </option>
      </select>
    </div>
    <div class="col-md-5">
      <label
        for="report-period-selection"
        class="form-label"
        >Display</label
      >
      <select
        id="report-period-selection"
        class="form-select"
        :value="String(props.showMonths)"
        :disabled="props.disabled"
        data-test-id="report-period-selector"
        @change="updatePeriod"
      >
        <option :value="false">Annual only</option>
        <option :value="true">Months and total</option>
      </select>
    </div>
    <div class="col-md-3 d-flex align-items-end">
      <button
        type="button"
        class="btn btn-secondary"
        :disabled="props.disabled"
        data-test-id="report-reset"
        @click="emit('reset')"
      >
        Reset
      </button>
    </div>
  </div>
</template>
