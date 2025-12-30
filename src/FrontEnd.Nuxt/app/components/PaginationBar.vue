<script setup lang="ts">
/**
 * Page picker for navigating multi-page results lists
 *
 * It will display 3 numbered page links, including the current page, plus
 * icon links to first and last pages.
 *
 * If first or last page is contained in the numbered pages, the icon
 * link is not shown
 *
 * Typically, the three numbered pages are current, previous, and
 * next. However, if current or next pages don't exist, we'll also
 * display an extra page on the other end, so we always display 3
 * numbered pages if possible.
 */

import { computed } from 'vue'
import type { IPaginatedResultBaseDto } from '~/utils/apiclient'

const props = defineProps<{
  /**
   * Pagination metadata from the API
   */
  pageInfo: IPaginatedResultBaseDto
}>()

const emit = defineEmits<{
  /**
   * User has selected a new page
   */
  pageUpdated: [page: number]
}>()

/**
 * Handle user desire to navigate to a new page
 *
 * @param page Desired new page
 */
function newPage(page: number) {
  if (page !== props.pageInfo.pageNumber) {
    emit('pageUpdated', page)
  }
}

/**
 * Convenience property for current page
 */
const page = computed(() => props.pageInfo.pageNumber ?? 0)

/**
 * Convenience property for last page
 */
const last = computed(() => props.pageInfo.totalPages ?? 0)

/**
 * First item number on the current page
 */
const firstItem = computed(() => {
  const pageNum = props.pageInfo.pageNumber ?? 0
  const pageSize = props.pageInfo.pageSize ?? 0
  return pageNum > 0 && pageSize > 0 ? (pageNum - 1) * pageSize + 1 : 0
})

/**
 * Last item number on the current page
 */
const lastItem = computed(() => {
  const pageNum = props.pageInfo.pageNumber ?? 0
  const pageSize = props.pageInfo.pageSize ?? 0
  const totalCount = props.pageInfo.totalCount ?? 0
  const calculatedLast = pageNum * pageSize
  return Math.min(calculatedLast, totalCount)
})

/**
 * Pages which should be displayed as numbers
 */
const numberedPages = computed((): Array<number> => {
  const p = page.value

  // In typical case, we display current page plus the ones before
  // and after it
  let result = [p - 1, p, p + 1]

  // However, if we're at the first page, there is no "page before",
  // so need to display page 3 as well to round out the triplet
  if (page.value === 1) result = [1, 2, 3]

  // Likewise, if we're at the last page, there is no "page after",
  // so need to display page-2 also
  if (page.value === last.value) result = [p - 2, p - 1, p]

  // Finally, make sure the pages we want to display actually exist
  return result.filter((x) => x >= 1 && x <= last.value)
})
</script>

<template>
  <div
    v-if="pageInfo"
    data-test-id="PaginationBar"
    class="mt-2 row"
  >
    <div class="col-sm-7">
      <p class="fs-6">
        Displaying
        <span data-test-id="firstitem">{{ firstItem }}</span> through
        <span data-test-id="lastitem">{{ lastItem }}</span> of
        <span data-test-id="totalitems">{{ pageInfo.totalCount }}</span
        >.
      </p>
    </div>
    <nav
      v-if="page && last"
      class="col-sm-5"
      aria-label="Pagination control"
    >
      <ul class="pagination justify-content-end">
        <PaginationBarLink
          v-if="!numberedPages.includes(1)"
          :page="1"
          icon="chevrons-left"
          aria-label="First Page"
          data-test-id="page-first"
          @page-updated="newPage"
        />
        <PaginationBarLink
          v-for="each in numberedPages"
          :key="each"
          :page="each"
          :current="page"
          @page-updated="newPage"
        />
        <PaginationBarLink
          v-if="!numberedPages.includes(last)"
          :page="last"
          icon="chevrons-right"
          aria-label="Last Page"
          data-test-id="page-last"
          @page-updated="newPage"
        />
      </ul>
    </nav>
  </div>
</template>
