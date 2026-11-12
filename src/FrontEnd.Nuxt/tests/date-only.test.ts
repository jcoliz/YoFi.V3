import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import * as api from '~/utils/apiclient'
import {
  formatDateOnly,
  fromDateInputValue,
  todayDateInputValue,
  toDateInputValue,
  transactionDatePreset,
} from '~/utils/dateOnly'

const dates = ['2026-01-01', '2025-12-31', '2024-02-29', '2026-03-08', '2026-11-01', '2026-10-01']

for (const timezone of ['America/Los_Angeles', 'UTC', 'Pacific/Kiritimati']) {
  describe(`date-only transaction values keep their calendar day in ${timezone}`, () => {
    let originalTimezone: string | undefined

    beforeEach(() => {
      originalTimezone = process.env.TZ
      process.env.TZ = timezone
    })

    describe('transaction date presets use inclusive local calendar dates', () => {
      it('uses 30 inclusive days', () => {
        expect(transactionDatePreset('last30Days', '2026-03-08')).toEqual({
          fromDate: '2026-02-07',
          toDate: '2026-03-08',
        })
      })

      it('clamps month subtraction before adding the inclusive day', () => {
        expect(transactionDatePreset('last3Months', '2026-01-31')).toEqual({
          fromDate: '2025-11-01',
          toDate: '2026-01-31',
        })
        expect(transactionDatePreset('last3Months', '2024-05-31')).toEqual({
          fromDate: '2024-03-01',
          toDate: '2024-05-31',
        })
      })

      it('handles leap days, calendar years, and all time', () => {
        expect(transactionDatePreset('last12Months', '2024-02-29')).toEqual({
          fromDate: '2023-03-01',
          toDate: '2024-02-29',
        })
        expect(transactionDatePreset('thisYear', '2026-12-31')).toEqual({
          fromDate: '2026-01-01',
          toDate: '2026-12-31',
        })
        expect(transactionDatePreset('allTime', '2026-12-31')).toEqual({
          fromDate: '',
          toDate: '',
        })
      })
    })

    afterEach(() => {
      if (originalTimezone === undefined) delete process.env.TZ
      else process.env.TZ = originalTimezone
    })

    for (const value of dates) {
      it(`preserves ${value}`, () => {
        const [year, month, day] = value.split('-').map(Number)
        const expectedDisplay = new Date(year, month - 1, day).toLocaleDateString()
        const parsed = api.TransactionResultDto.fromJS({ key: value, date: value }).date

        expect(formatDateOnly(parsed)).toBe(expectedDisplay)
        expect(toDateInputValue(parsed)).toBe(value)

        const dto = new api.TransactionEditDto({
          date: fromDateInputValue(value),
          payee: 'x',
        })
        expect(dto.toJSON().date).toBe(value)
      })
    }

    it('handles undefined gracefully', () => {
      expect(formatDateOnly(undefined)).toBe('')
      expect(toDateInputValue(undefined)).toBe('')
    })

    it('todayDateInputValue returns a valid local date string', () => {
      const now = new Date()
      const today = todayDateInputValue()
      expect(today).toMatch(/^\d{4}-\d{2}-\d{2}$/)
      expect(fromDateInputValue(today).getDate()).toBe(now.getDate())
    })
  })
}
