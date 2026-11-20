<script setup lang="ts">
import type { ReportDefinitionDto } from '~/utils/apiclient'
import { MAX_REPORT_DISPLAY_DEPTH } from '~/utils/reportDisplay'

/** Selects a report and edits its year and display settings. */
const props = withDefaults(
  defineProps<{
    definitions: ReportDefinitionDto[]
    years: number[]
    selectedReportId: string
    selectedYear: number | undefined
    displayDepth: number
    showMonths: boolean
    disabled?: boolean
  }>(),
  { disabled: false },
)

const emit = defineEmits<{
  'update:selectedReportId': [string]
  'update:selectedYear': [number]
  'update:displayDepth': [number]
  'update:showMonths': [boolean]
  selectionChange: []
  reset: []
}>()

const depthOptions = Array.from({ length: MAX_REPORT_DISPLAY_DEPTH }, (_, index) => index + 1)

function updateReport(event: Event) {
  emit('update:selectedReportId', (event.target as HTMLSelectElement).value)
  emit('selectionChange')
}

function updateYear(event: Event) {
  emit('update:selectedYear', Number((event.target as HTMLSelectElement).value))
  emit('selectionChange')
}

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
    data-test-id="report-selection-controls"
  >
    <div class="col-md-9">
      <label
        for="report-selection"
        class="form-label"
        >Report</label
      >
      <select
        id="report-selection"
        class="form-select"
        :value="props.selectedReportId"
        data-test-id="report-selector"
        @change="updateReport"
      >
        <option
          v-for="definition in props.definitions"
          :key="definition.id"
          :value="definition.id"
        >
          {{ definition.name }}
        </option>
      </select>
    </div>
    <div class="col-md-3">
      <label
        for="report-year-selection"
        class="form-label"
        >Year</label
      >
      <select
        id="report-year-selection"
        class="form-select"
        :value="props.selectedYear"
        data-test-id="report-year-selector"
        @change="updateYear"
      >
        <option
          v-for="year in props.years"
          :key="year"
          :value="year"
        >
          {{ year }}
        </option>
      </select>
    </div>
  </div>
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
