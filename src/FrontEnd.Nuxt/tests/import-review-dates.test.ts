import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { DuplicateStatus, ImportReviewTransactionDto } from '~/utils/apiclient'
import ReviewTable from '~/components/import/ReviewTable.vue'

const dates: [string, string][] = [
  ['2026-10-01', 'Oct 1, 2026'],
  ['2026-11-03', 'Nov 3, 2026'],
  ['2026-01-01', 'Jan 1, 2026'],
  ['2024-02-29', 'Feb 29, 2024'],
  ['2026-03-08', 'Mar 8, 2026'],
  ['2026-11-01', 'Nov 1, 2026'],
]

for (const timezone of ['America/Los_Angeles', 'UTC', 'Pacific/Kiritimati']) {
  describe(`import review preserves date-only API values in ${timezone}`, () => {
    let originalTimezone: string | undefined

    beforeEach(() => {
      originalTimezone = process.env.TZ
      process.env.TZ = timezone
    })

    afterEach(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    it('renders all dates without timezone shift', async () => {
      const transactions = dates.map(([date]) =>
        ImportReviewTransactionDto.fromJS({
          key: date,
          date,
          payee: 'Maple Grove Apartments',
          amount: -1500,
          duplicateStatus: DuplicateStatus.New,
          isSelected: true,
        }),
      )

      const wrapper = mount(ReviewTable, {
        props: { transactions },
        global: {
          stubs: { BaseSpinner: true },
        },
      })

      const html = wrapper.html()
      for (const [, expected] of dates) {
        expect(html).toContain(expected)
      }
      expect(html).not.toContain('Sep 30, 2026')
    })
  })
}
