import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { onMounted, nextTick } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import ReportsPage from '~/pages/reports.vue'
import ReportsTable from '~/components/reports/Table.vue'
import ReportDisplayControls from '~/components/reports/DisplayControls.vue'
import { ReportResultDto, TenantRoleResultDto } from '~/utils/apiclient'
import { useUserPreferencesStore } from '~/stores/userPreferences'

enableAutoUnmount(afterEach)
afterEach(() => vi.restoreAllMocks())
vi.stubGlobal('definePageMeta', vi.fn())
vi.stubGlobal('useAuthenticationGuard', () => ({
  requireAuthenticatedUser: (action: () => Promise<void>) => onMounted(action),
}))

const catalog = [
  { id: 'income', name: 'Income', defaultDepth: 1, hasMonthColumns: false },
  { id: 'expenses-detail', name: 'Expenses Detail', defaultDepth: 2, hasMonthColumns: true },
]
const getAvailableReports = vi.fn()
const availability = { years: [2042, 2040, 2035], defaultYear: 2040 }
const getAvailableYears = vi.fn()
const getReport = vi.fn()
vi.stubGlobal('useApiClient', () => ({ getAvailableReports, getAvailableYears, getReport }))

function makeReport(id = 'income', detail = false, year = 2040) {
  return ReportResultDto.fromJS({
    id,
    name: detail ? 'Expenses Detail' : 'Income',
    categoryRoot: detail ? null : 'Income',
    year,
    displayDepth: detail ? 2 : 1,
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
        ReportsDisplayControls: ReportDisplayControls,
        DisplayControls: ReportDisplayControls,
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
  getAvailableYears.mockReset().mockResolvedValue(availability)
  getReport
    .mockReset()
    .mockImplementation(
      async (id: string, year: number, displayDepth: number, showMonths: boolean) => {
        const report = makeReport(id, id.endsWith('detail'), year)
        report.displayDepth = displayDepth
        report.hasMonthColumns = showMonths
        return report
      },
    )
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
      props: { report: makeReport('expenses-detail', true) },
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
  it('shares year changes across reports and restores the workspace year after remount', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2035')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2035, 1, false, 'first')
    expect(wrapper.find('#report-heading').text()).toContain('2035')
    expect(store.getReportSettings('first')).toEqual({ year: 2035 })

    await wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', 2035, 2, true, 'first')
    expect(wrapper.find('#report-heading').text()).toContain('2035')
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2042')
    await flushPromises()
    await wrapper.find('[data-test-id="report-selector"]').setValue('income')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2042, 1, false, 'first')
    expect(getAvailableYears).toHaveBeenCalledOnce()
    expect(getAvailableReports).toHaveBeenCalledOnce()

    wrapper.unmount()
    setActivePinia(createPinia())
    const returned = setupPage()
    await flushPromises()
    expect(
      returned.wrapper.find<HTMLSelectElement>('[data-test-id="report-year-selector"]').element
        .value,
    ).toBe('2042')
    await returned.wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', 2042, 2, true, 'first')
  })

  it('restores independent workspace years and resets availability on workspace changes', async () => {
    const { wrapper, store } = setupPage()
    store.updateReportSettings('first', { year: 2035 })
    store.updateReportSettings('second', { year: 2042 })
    await flushPromises()
    store.currentTenant = new TenantRoleResultDto({ key: 'second' })
    await nextTick()
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
    await flushPromises()
    expect(getAvailableYears).toHaveBeenLastCalledWith('second')
    expect(getReport).toHaveBeenLastCalledWith('income', 2042, 1, false, 'second')
    store.currentTenant = new TenantRoleResultDto({ key: 'first' })
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2035, 1, false, 'first')
  })

  it('replaces an unavailable saved year with the backend default', async () => {
    useUserPreferencesStore().updateReportSettings('first', { year: 2020 })
    const { store } = setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'first')
    expect(store.getReportSettings('first')).toEqual({ year: 2040 })
  })

  it('does not restore conflicting legacy per-report years when switching reports', async () => {
    localStorage.setItem(
      'userPreferences:tenant:first:reports:income:settings',
      JSON.stringify({ year: 2035 }),
    )
    localStorage.setItem(
      'userPreferences:tenant:first:reports:expenses-detail:settings',
      JSON.stringify({ year: 2042 }),
    )
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'first')
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2035')
    await flushPromises()
    await wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', 2035, 2, true, 'first')
    expect(store.getReportSettings('first')).toEqual({ year: 2035 })
  })

  it('keeps the year selector for an empty workspace and displays the empty report', async () => {
    getAvailableYears.mockResolvedValue({ years: [2040], defaultYear: 2040 })
    const empty = makeReport()
    empty.rows = []
    getReport.mockResolvedValue(empty)
    const { wrapper } = setupPage()
    await flushPromises()
    expect(wrapper.findAll('[data-test-id="report-year-selector"] option')).toHaveLength(1)
    expect(wrapper.find('[data-test-id="report-table"]').text()).toContain('No data')
  })

  it.each([
    undefined,
    {},
    { years: [], defaultYear: 2040 },
    { years: [2042], defaultYear: 2040 },
    { years: [2040, 2040], defaultYear: 2040 },
    { years: [2035, 2040], defaultYear: 2040 },
    { years: ['2040'], defaultYear: '2040' },
    { years: [10000], defaultYear: 10000 },
    { years: [0], defaultYear: 0 },
    { years: [2040.5], defaultYear: 2040.5 },
  ])('surfaces invalid year metadata without requesting a report: %j', async (value) => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    getAvailableYears.mockResolvedValue(value)
    const { wrapper } = setupPage()
    await flushPromises()
    expect(wrapper.find('[role="alert"]').text()).toContain('Failed to load reports')
    expect(getReport).not.toHaveBeenCalled()
  })

  it('retries a failed historical report without rediscovery or losing the persisted year', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    const { wrapper, store } = setupPage()
    await flushPromises()
    getReport.mockRejectedValueOnce(new Error('Unavailable'))
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2035')
    await flushPromises()
    expect(store.getReportSettings('first')).toEqual({ year: 2035 })
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
    await wrapper.find('button').trigger('click')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2035, 1, false, 'first')
    expect(getAvailableYears).toHaveBeenCalledOnce()
    expect(getAvailableReports).toHaveBeenCalledOnce()
    expect(wrapper.find('#report-heading').text()).toContain('2035')
  })

  it.each(['read', 'write'])(
    'surfaces preference %s failures and permits retry',
    async (operation) => {
      vi.spyOn(console, 'error').mockImplementation(() => {})
      const store = useUserPreferencesStore()
      const method = operation === 'read' ? 'getReportSettings' : 'updateReportSettings'
      const failure = vi.spyOn(store, method).mockImplementationOnce(() => {
        throw new Error('Storage unavailable')
      })
      const { wrapper } = setupPage()
      await flushPromises()
      expect(wrapper.find('[role="alert"]').text()).toContain('Persistence Failed')
      expect(getReport).not.toHaveBeenCalled()
      failure.mockRestore()
      await wrapper.find('button').trigger('click')
      await flushPromises()
      expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'first')
    },
  )

  it.each(['resolve', 'reject'])('ignores stale year requests that %s', async (outcome) => {
    const pending = deferred<ReportResultDto>()
    const { wrapper, store } = setupPage()
    await flushPromises()
    getReport.mockReturnValueOnce(pending.promise)
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2035')
    expect(wrapper.find('[data-test-id="report-table"]').exists()).toBe(false)
    await wrapper.find('[data-test-id="report-year-selector"]').setValue('2042')
    await flushPromises()
    if (outcome === 'resolve') pending.resolve(makeReport('income', false, 2035))
    else pending.reject(new Error('Old failure'))
    await flushPromises()
    expect(wrapper.find('#report-heading').text()).toContain('2042')
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(store.getReportSettings('first')).toEqual({ year: 2042 })
  })

  it.each(['workspace', 'unmount'])('ignores pending year discovery after %s', async (action) => {
    const pending = deferred<typeof availability>()
    getAvailableYears.mockReturnValueOnce(pending.promise)
    const { wrapper, store } = setupPage()
    await flushPromises()
    if (action === 'workspace') {
      store.currentTenant = new TenantRoleResultDto({ key: 'second' })
      await flushPromises()
    } else wrapper.unmount()
    pending.resolve({ years: [2000], defaultYear: 2000 })
    await flushPromises()
    expect(store.getSelectedReportId('first')).toBeNull()
    expect(store.getReportSettings('first')).toEqual({})
    expect(getReport).toHaveBeenCalledTimes(action === 'workspace' ? 1 : 0)
    if (action === 'workspace')
      expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'second')
  })

  it('uses Expenses when the backend lists it first, without overriding a saved selection', async () => {
    getAvailableReports.mockResolvedValue([
      { id: 'expenses', name: 'Expenses', defaultDepth: 1, hasMonthColumns: false },
      ...catalog,
    ])
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses', 2040, 1, false, 'first')
    expect(store.getSelectedReportId('first')).toBe('expenses')
    wrapper.unmount()

    store.setSelectedReportId('first', 'income')
    setupPage()
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
  })

  describe('Report settings preferences', () => {
    const key = 'userPreferences:tenant:first:reports:settings'

    it('merges year changes without discarding other report settings', () => {
      localStorage.setItem(key, JSON.stringify({ year: 2035, depth: 2 }))
      const store = useUserPreferencesStore()
      store.updateReportSettings('first', { year: 2040 })
      expect(JSON.parse(localStorage.getItem(key)!)).toEqual({ year: 2040, depth: 2 })
    })

    it.each(['{', 'null', '[]', '"2040"', '{"year":"2040"}', '{"year":2040.5}', '{"year":0}'])(
      'diagnoses malformed settings and recovers to the backend default: %s',
      async (stored) => {
        const log = vi.spyOn(console, 'error').mockImplementation(() => {})
        localStorage.setItem(key, stored)
        setupPage()
        await flushPromises()
        expect(log).toHaveBeenCalledWith(
          'Failed to parse stored report settings:',
          expect.any(Error),
        )
        expect(getReport).toHaveBeenLastCalledWith('income', 2040, 1, false, 'first')
        expect(JSON.parse(localStorage.getItem(key)!)).toEqual({ year: 2040 })
      },
    )

    it.each(['getItem', 'setItem'] as const)('propagates localStorage %s errors', (method) => {
      const store = useUserPreferencesStore()
      vi.spyOn(localStorage, method).mockImplementation(() => {
        throw new Error('Storage unavailable')
      })
      expect(() => store.updateReportSettings('first', { year: 2040 })).toThrow(
        'Storage unavailable',
      )
    })
  })

  it('loads and persists the backend default year rather than the first available year', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    expect(getAvailableReports).toHaveBeenCalledOnce()
    expect(getAvailableReports).toHaveBeenCalledWith('first')
    expect(getAvailableYears).toHaveBeenCalledWith('first')
    expect(getReport).toHaveBeenCalledWith('income', 2040, 1, false, 'first')
    expect(store.getReportSettings('first')).toEqual({ year: 2040 })
    expect(
      wrapper
        .findAll('[data-test-id="report-year-selector"] option')
        .map((option) => option.text()),
    ).toEqual(['2042', '2040', '2035'])
    expect(store.getSelectedReportId('first')).toBe('income')
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('Income')
    expect(wrapper.find('[data-test-id="report-result"]').text()).toContain('2040')
  })

  it('saves selection changes and restores them on a return visit', async () => {
    const { wrapper, store } = setupPage()
    await flushPromises()
    await wrapper.find('[data-test-id="report-selector"]').setValue('expenses-detail')
    await flushPromises()
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', 2040, 2, true, 'first')
    expect(store.getSelectedReportId('first')).toBe('expenses-detail')
    expect(wrapper.findComponent(ReportsTable).props('report').displayDepth).toBe(2)
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
    expect(getReport).toHaveBeenCalledWith('income', 2040, 1, false, 'first')
    expect(store.getSelectedReportId('first')).toBe('income')
  })

  it('restores independent workspace selections and clears data without a workspace', async () => {
    const { wrapper, store } = setupPage()
    store.setSelectedReportId('second', 'expenses-detail')
    await flushPromises()
    store.currentTenant = new TenantRoleResultDto({ key: 'second' })
    await flushPromises()
    expect(getAvailableReports).toHaveBeenLastCalledWith('second')
    expect(getReport).toHaveBeenLastCalledWith('expenses-detail', 2040, 2, true, 'second')
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
    expect(getAvailableYears).not.toHaveBeenCalled()
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

  it.each(['catalog', 'years', 'report'])('shows %s errors and supports retry', async (source) => {
    const log = vi.spyOn(console, 'error').mockImplementation(() => {})
    const failing =
      source === 'catalog'
        ? getAvailableReports
        : source === 'years'
          ? getAvailableYears
          : getReport
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
    expect(getReport).toHaveBeenCalledWith('income', 2040, 1, false, 'second')
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
