import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { onMounted, nextTick } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import ReportsPage from '~/pages/reports.vue'
import ReportsTable from '~/components/reports/Table.vue'
import { ReportResultDto, TenantRoleResultDto } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

enableAutoUnmount(afterEach)
vi.stubGlobal('definePageMeta', vi.fn())
vi.stubGlobal('useAuthenticationGuard', () => ({
  requireAuthenticatedUser: (action: () => Promise<void>) => onMounted(action),
}))

const catalog = [
  { id: 'income', name: 'Income', defaultDepth: 1, hasMonthColumns: false },
  { id: 'expenses-detail', name: 'Expenses Detail', defaultDepth: 2, hasMonthColumns: true },
]
const getAvailableReports = vi.fn()
const getReport = vi.fn()
vi.stubGlobal('useApiClient', () => ({ getAvailableReports, getReport }))

function makeReport(id = 'income', detail = false) {
  return ReportResultDto.fromJS({
    id,
    name: detail ? 'Expenses Detail' : 'Income',
    year: 2040,
    hasMonthColumns: detail,
    rows: [
      {
        categoryPath: 'Home',
        categoryName: 'Home',
        depth: 1,
        isSubtotal: detail,
        annualTotal: 100,
        percentage: 1,
        monthlyAmounts: Array(12).fill(100 / 12),
      },
      ...(detail
        ? [
            {
              categoryPath: 'Home:Utilities',
              categoryName: 'Utilities',
              depth: 2,
              isSubtotal: false,
              annualTotal: 25,
              percentage: 0.25,
              monthlyAmounts: Array(12).fill(25 / 12),
            },
          ]
        : []),
    ],
    totals: { annualTotal: 100, monthlyAmounts: Array(12).fill(100 / 12) },
  })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: Error) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

function setupPage(key: string | undefined = 'first') {
  const store = useUserPreferencesStore()
  store.currentTenant = key ? new TenantRoleResultDto({ key, name: key }) : undefined
  vi.spyOn(store, 'loadFromStorage').mockImplementation(() => {})
  const wrapper = mount(ReportsPage, {
    global: {
      components: {
        ReportsTable,
        WorkspaceSelector: { template: '<div />' },
        BaseSpinner: { template: '<span />' },
        ErrorDisplay: {
          props: ['show', 'problem'],
          template:
            '<div v-if="show" role="alert">{{ problem?.title }} {{ problem?.detail }}</div>',
        },
      },
    },
  })
  return { wrapper, store }
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  getAvailableReports.mockReset().mockResolvedValue(catalog)
  getReport
    .mockReset()
    .mockImplementation(async (id: string) => makeReport(id, id.endsWith('detail')))
})

describe('Reports table', () => {
  it('renders annual-only columns, currency and signed annual percentages', () => {
    const report = makeReport()
    report.rows![0]!.annualTotal = -25
    report.rows![0]!.percentage = -0.25
    const wrapper = mount(ReportsTable, { props: { report } })

    expect(wrapper.findAll('thead th').map((header) => header.text())).toEqual([
      'Income',
      'Total',
      '% Total',
    ])
    expect(wrapper.find('tbody').text()).toContain('-$25.00')
    expect(wrapper.find('tbody').text()).toContain('-25%')
    expect(wrapper.findAll('tbody tr')[0]!.classes()).not.toContain('fw-bold')
  })

  it('renders twelve month columns, bold parent rows and indented normal child rows', () => {
    const wrapper = mount(ReportsTable, {
      props: { report: makeReport('expenses-detail', true), displayDepth: 2 },
    })
    const headers = wrapper.findAll('thead th').map((header) => header.text())
    expect(headers).toHaveLength(15)
    expect(headers[1]).toBe('January')
    expect(headers[12]).toBe('December')
    const rows = wrapper.findAll('[data-test-id="report-row"]')
    expect(rows[0]!.classes()).toContain('fw-bold')
    expect(rows[1]!.classes()).not.toContain('fw-bold')
    expect(rows[0]!.find('th').classes()).not.toContain('fw-normal')
    expect(rows[1]!.find('th').classes()).toContain('fw-normal')
    expect(rows[0]!.find('th').attributes('style')).toContain('0.5rem')
    expect(rows[1]!.find('th').attributes('style')).toContain('2rem')
    expect(rows[0]!.find('th').text()).toBe('Home')
    expect(rows[1]!.find('th').text()).toBe('Utilities')
  })

  it.each([false, true])(
    'bolds all level-one rows in a two-level report even without children (months: %s)',
    (hasMonthColumns) => {
      const report = makeReport()
      report.hasMonthColumns = hasMonthColumns
      const wrapper = mount(ReportsTable, { props: { report, displayDepth: 2 } })
      const row = wrapper.find('[data-test-id="report-row"]')

      expect(report.rows![0]!.isSubtotal).toBe(false)
      expect(row.classes()).toContain('fw-bold')
      expect(row.find('th').classes()).not.toContain('fw-normal')
      expect(row.find('th').attributes('style')).toContain('0.5rem')
    },
  )

  it('does not use subtotal status to style a one-level report', () => {
    const report = makeReport()
    report.rows![0]!.isSubtotal = true
    const wrapper = mount(ReportsTable, { props: { report, displayDepth: 1 } })
    const row = wrapper.find('[data-test-id="report-row"]')

    expect(row.classes()).not.toContain('fw-bold')
    expect(row.find('th').classes()).toContain('fw-normal')
  })

  it.each([
    ['Income:401k Match', '401k Match'],
    ['Income:Salary', 'Salary'],
    ['Income:Salary:Primary', 'Primary'],
    ['Uncategorized', 'Uncategorized'],
    ['Income:Salary', 'Backend-supplied label'],
  ])('renders the backend category name for %s without changing its full path', (path, label) => {
    const report = makeReport()
    report.rows![0]!.categoryPath = path
    report.rows![0]!.categoryName = label
    const wrapper = mount(ReportsTable, { props: { report } })

    expect(wrapper.find('[data-test-id="report-row"] th').text()).toBe(label)
    expect(report.rows![0]!.categoryPath).toBe(path)
  })

  it('uses explicit footer totals instead of adding overlapping parent and child rows', () => {
    const wrapper = mount(ReportsTable, { props: { report: makeReport('expenses-detail', true) } })
    const footer = wrapper.findAll('tfoot td')
    expect(footer[0]!.text()).toBe('$8.33')
    expect(footer[12]!.text()).toBe('$100.00')
    expect(footer[13]!.text()).toBe('100%')
  })

  it('renders undefined percentages as em dashes, including the zero-total footer', () => {
    const report = ReportResultDto.fromJS({
      ...makeReport().toJSON(),
      rows: [
        { categoryPath: 'Home', categoryName: 'Home', annualTotal: 0, percentage: null, depth: 1 },
      ],
      totals: { annualTotal: 0 },
    })
    const wrapper = mount(ReportsTable, { props: { report } })
    expect(wrapper.findAll('tbody td').at(-1)!.text()).toBe('\u2014')
    expect(wrapper.findAll('tfoot td').at(-1)!.text()).toBe('\u2014')
  })

  it.each([false, true])(
    'keeps headers but manufactures no category rows when empty (months: %s)',
    (detail) => {
      const report = makeReport('empty', detail)
      report.rows = []
      const wrapper = mount(ReportsTable, { props: { report } })
      expect(wrapper.findAll('thead th')).toHaveLength(detail ? 15 : 3)
      expect(wrapper.findAll('[data-test-id="report-row"]')).toHaveLength(0)
      expect(wrapper.find('tbody').text()).toContain('No data')
      expect(wrapper.find('tbody td').attributes('colspan')).toBe(detail ? '15' : '3')
    },
  )
})

describe('Reports page', () => {
  it('loads the catalog default, persists it and leaves the year unspecified', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getAvailableReports).toHaveBeenCalledOnce()
    expect(getAvailableReports).toHaveBeenCalledWith('first')
    expect(getReport).toHaveBeenCalledWith('income', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
    expect(wrapper.find('h2').text()).toBe('Income \u2014 2040')
  })

  it('saves selection changes and restores them on a return visit', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    await wrapper.find('select').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('expenses-detail')
    expect(wrapper.findComponent(ReportsTable).props('displayDepth')).toBe(2)
    wrapper.unmount()
    const returned = setupPage()
    await flushPromises()
    expect(returned.wrapper.find<HTMLSelectElement>('select').element.value).toBe('expenses-detail')
  })

  it('replaces an obsolete saved report ID with the catalog default', async () => {
    useUserPreferencesStore().setSelectedReportId('first', 'removed')
    const { store } = setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenCalledWith('income', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
  })

  it('restores independent workspace selections and clears data without a workspace', async () => {
    const { wrapper, store } = setupPage()
    store.setSelectedReportId('second', 'expenses-detail')
    await flushPromises()
    store.currentTenant = new TenantRoleResultDto({ key: 'second' })
    await flushPromises()
    expect(getAvailableReports).toHaveBeenLastCalledWith('second')
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', undefined, 'second')
    expect(store.getSelectedReportId('first')).toBe('income')
    store.currentTenant = undefined
    await flushPromises()
    expect(wrapper.find('[data-test-id="report-result"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="no-workspace-warning"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="loading-state"]').exists()).toBe(false)
  })

  it('does not request data without a selected workspace', async () => {
    const { wrapper } = setupPage('')
    await flushPromises()
    expect(getAvailableReports).not.toHaveBeenCalled()
    expect(getReport).not.toHaveBeenCalled()
    expect(wrapper.find('[data-test-id="no-workspace-warning"]').exists()).toBe(true)
  })

  it('shows loading until the report arrives', async () => {
    const pending = deferred<ReportResultDto>()
    getReport.mockReturnValue(pending.promise)
    const { wrapper } = setupPage()
    await flushPromises()
    expect(wrapper.find('[data-test-id="loading-state"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
    pending.resolve(makeReport())
    await flushPromises()
    expect(wrapper.find('[data-test-id="loading-state"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(true)
  })

  it.each(['catalog', 'report'])('shows %s errors and supports retry', async (source) => {
    const log = vi.spyOn(console, 'error').mockImplementation(() => {})
    const failing = source === 'catalog' ? getAvailableReports : getReport
    failing.mockRejectedValueOnce(new Error('Unavailable'))
    const { wrapper } = setupPage()
    await flushPromises()
    expect(wrapper.find('[role="alert"]').text()).toContain('Failed to load')
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
    await wrapper.find('button').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(true)
    log.mockRestore()
  })

  it('ignores a stale catalog response after switching workspaces', async () => {
    const pending = deferred<typeof catalog>()
    getAvailableReports.mockReturnValueOnce(pending.promise)
    const { wrapper, store } = setupPage()
    await flushPromises()
    store.currentTenant = new TenantRoleResultDto({ key: 'second' })
    await flushPromises()
    pending.resolve(catalog)
    await flushPromises()
    expect(getReport).toHaveBeenCalledOnce()
    expect(getReport).toHaveBeenCalledWith('income', undefined, 'second')
    expect(wrapper.find('h2').text()).toContain('Income')
  })

  it('ignores an older report response after selecting a different report', async () => {
    const pending = deferred<ReportResultDto>()
    getReport.mockReturnValueOnce(pending.promise)
    const { wrapper } = setupPage()
    await flushPromises()
    await wrapper.find('select').setValue('expenses-detail')
    await flushPromises()
    pending.resolve(makeReport())
    await flushPromises()
    expect(wrapper.find('h2').text()).toContain('Expenses Detail')
  })

  it('ignores a report response after the workspace has been cleared', async () => {
    const pending = deferred<ReportResultDto>()
    getReport.mockReturnValueOnce(pending.promise)
    const { wrapper, store } = setupPage()
    await flushPromises()
    store.currentTenant = undefined
    await nextTick()
    pending.resolve(makeReport())
    await flushPromises()
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
  })
})
