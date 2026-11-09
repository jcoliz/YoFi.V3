<script setup lang="ts">
import type { ReportResultDto } from '~/utils/apiclient'

defineProps<{ report: ReportResultDto; displayDepth?: number }>()

const months = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
]
const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })
const percent = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 2 })

function formatAmount(amount: number | undefined) {
  return amount == null ? '\u2014' : currency.format(amount)
}

function formatPercentage(value: number | null | undefined) {
  return value == null ? '\u2014' : percent.format(value)
}
</script>

<template>
  <div class="table-responsive">
    <table
      class="table table-hover mb-0"
      data-test-id="report-table"
    >
      <caption class="visually-hidden">
        {{
          report.name
        }}
        for
        {{
          report.year
        }}
      </caption>
      <thead>
        <tr>
          <th scope="col">{{ report.categoryRoot ?? 'Category' }}</th>
          <template v-if="report.hasMonthColumns">
            <th
              v-for="month in months"
              :key="month"
              scope="col"
              class="text-end"
            >
              {{ month }}
            </th>
          </template>
          <th
            scope="col"
            class="text-end"
          >
            Total
          </th>
          <th
            scope="col"
            class="text-end"
            :class="{ 'annual-percentage': !report.hasMonthColumns }"
          >
            % Total
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="!report.rows?.length">
          <td
            :colspan="report.hasMonthColumns ? 15 : 3"
            class="text-center py-4"
          >
            No data for this report and year.
          </td>
        </tr>
        <tr
          v-for="(row, index) in report.rows"
          :key="index"
          :class="{ 'fw-bold': displayDepth === 2 && row.depth === 1 }"
          data-test-id="report-row"
        >
          <th
            scope="row"
            :class="{ 'fw-normal': !(displayDepth === 2 && row.depth === 1) }"
            :style="{ paddingLeft: `${0.5 + Math.max(0, (row.depth ?? 1) - 1) * 1.5}rem` }"
          >
            {{ row.categoryName }}
          </th>
          <template v-if="report.hasMonthColumns">
            <td
              v-for="(_, month) in months"
              :key="month"
              class="text-end text-nowrap"
            >
              {{ formatAmount(row.monthlyAmounts?.[month]) }}
            </td>
          </template>

          <td class="text-end text-nowrap">{{ formatAmount(row.annualTotal) }}</td>
          <td class="text-end text-nowrap">{{ formatPercentage(row.percentage) }}</td>
        </tr>
      </tbody>
      <tfoot class="fw-bold">
        <tr>
          <th scope="row">Grand total</th>
          <template v-if="report.hasMonthColumns">
            <td
              v-for="(_, month) in months"
              :key="month"
              class="text-end text-nowrap"
            >
              {{ formatAmount(report.totals?.monthlyAmounts?.[month]) }}
            </td>
          </template>
          <td class="text-end text-nowrap">{{ formatAmount(report.totals?.annualTotal) }}</td>
          <td class="text-end text-nowrap">
            {{ formatPercentage(report.totals?.annualTotal ? 1 : null) }}
          </td>
        </tr>
      </tfoot>
    </table>
  </div>
</template>

<style scoped>
.annual-percentage {
  width: 1%;
  white-space: nowrap;
  padding-left: 2rem;
}
</style>
