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
    categoryRoot: detail ? null : 'Income',
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
  it.each([
    ['Taxes', 'Taxes'],
    ['Transfer', 'Transfer'],
    [null, 'Category'],
    [undefined, 'Category'],
  ])('uses category root %s as the header with a fallback for no root', (root, header) => {
    const report = makeReport()
    report.categoryRoot = root ?? undefined
    const wrapper = mount(ReportsTable, { props: { report } })

    expect(wrapper.find('thead th').text()).toBe(header)
    expect(wrapper.find('caption').text()).toContain('Income')
  })

  it('shows annual report content without month columns', () => {
    const report = makeReport()
    report.rows![0]!.annualTotal = -25
    report.rows![0]!.percentage = -0.25
    const wrapper = mount(ReportsTable, { props: { report } })

    const table = wrapper.find('[data-test-id="report-table"]')
    expect(table.text()).toContain('Income')
    expect(table.text()).not.toContain('January')
    expect(wrapper.find('[data-test-id="report-row"]').text()).toContain('Home')
    expect(wrapper.find('[data-test-id="report-row"]').text()).toContain('25')
  })

  it('shows monthly report content and preserves parent-child row order', () => {
    const wrapper = mount(ReportsTable, {
      props: { report: makeReport('expenses-detail', true), displayDepth: 2 },
    })
    const headers = wrapper.findAll('thead th').map((header) => header.text())
    expect(headers).toContain('January')
    expect(headers).toContain('December')
    const rows = wrapper.findAll('[data-test-id="report-row"]')
    expect(rows).toHaveLength(2)
    expect(rows[0]!.text()).toContain('Home')
    expect(rows[1]!.text()).toContain('Utilities')
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
    const footer = wrapper.find('tfoot')
    expect(footer.text()).toContain('Grand total')
    expect(footer.text()).toContain('100')
    expect(footer.text()).not.toContain('125')
  })

  it.each([false, true])(
    'shows the empty state without manufacturing category rows (months: %s)',
    (detail) => {
      const report = makeReport('empty', detail)
      report.rows = []
      const wrapper = mount(ReportsTable, { props: { report } })
      expect(wrapper.findAll('[data-test-id="report-row"]')).toHaveLength(0)
      expect(wrapper.find('[data-test-id="report-table"]').text()).toContain('No data')
    },
  )
})

describe('Reports page', () => {
  it('uses Expenses when the backend lists it first, without overriding a saved selection', async () => {
    getAvailableReports.mockResolvedValue([
      { id: 'expenses', name: 'Expenses', defaultDepth: 1, hasMonthColumns: false },
      ...catalog,
    ])
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('expenses')
    wrapper.unmount()

    store.setSelectedReportId('first', 'income')
    setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
  })

  it('loads the catalog default, persists it and leaves the year unspecified', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getAvailableReports).toHaveBeenCalledOnce()
    expect(getAvailableReports).toHaveBeenCalledWith('first')
    expect(getReport).toHaveBeenCalledWith('income', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('Income')
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('2040')
  })

  it('saves selection changes and restores them on a return visit', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    await wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', undefined, 'first')
    expect(store.getSelectedReportId('first')).toBe('expenses-detail')
    expect(wrapper.findComponent(ReportsTable).props('displayDepth')).toBe(2)
    wrapper.unmount()
    const returned = setupPage()
    await flushPromises()
    expect(
      returned.wrapper.find<HTMLSelectElement>('[data-test-id="report-selector"]').element.value,
    ).toBe('expenses-detail')
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
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('Income')
  })

  it('ignores an older report response after selecting a different report', async () => {
    const pending = deferred<ReportResultDto>()
    getReport.mockReturnValueOnce(pending.promise)
    const { wrapper } = setupPage()
    await flushPromises()
    await wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    pending.resolve(makeReport())
    await flushPromises()
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('Expenses Detail')
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
