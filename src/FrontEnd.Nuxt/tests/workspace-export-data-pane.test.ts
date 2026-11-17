import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import ExportDataPane from '~/components/workspace/ExportDataPane.vue'
import { TenantDataAdminClient, type TenantRoleResultDto } from '~/utils/apiclient'
import { fromDateInputValue } from '~/utils/dateOnly'

const { toastSuccess } = vi.hoisted(() => ({ toastSuccess: vi.fn() }))

vi.mock('~/utils/errorHandler', () => ({
  handleApiError: (_err: unknown, title = 'Error', detail?: string) => ({
    title,
    detail,
    status: 400,
  }),
}))
vi.mock('~/stores/toast', () => ({
  useToastStore: () => ({ success: toastSuccess }),
}))

const getExportOptions = vi.fn()
const exportData = vi.fn()
const useApiClientMock = vi.fn(
  (Client: typeof TenantDataAdminClient) => new Client('', { fetch: vi.fn() }),
)
vi.stubGlobal('useApiClient', useApiClientMock)

const workspace = { key: 'workspace-key', name: 'Test Workspace' } as TenantRoleResultDto
const dataTypes = [
  { dataType: 'Transactions', worksheetName: 'Transactions', supportsDateFilter: true },
  {
    dataType: 'PayeeMatchingRules',
    worksheetName: 'PayeeMatchingRules',
    supportsDateFilter: false,
  },
]

const globalConfig = {
  components: {
    BaseSpinner: { template: '<span />' },
    ErrorDisplay: {
      props: ['show', 'problem'],
      template:
        '<div v-if="show" data-test-id="export-error">{{ problem?.title }} {{ problem?.detail }}</div>',
    },
  },
}

function exportResponse(headers?: Record<string, unknown>) {
  return {
    data: new Blob(['workbook']),
    fileName: 'yofi-export.xlsx',
    headers,
    status: 200,
  }
}

async function mountPane() {
  const wrapper = mount(ExportDataPane, {
    props: { workspace },
    global: globalConfig,
  })
  await flushPromises()
  return wrapper
}

describe('Workspace ExportDataPane', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    toastSuccess.mockReset()
    getExportOptions.mockReset().mockResolvedValue({
      dataTypes,
      years: [2024, 2023],
    })
    exportData.mockReset().mockResolvedValue(
      exportResponse({
        'X-YoFi-Export-Counts': JSON.stringify([
          { DataType: 'Transactions', RecordCount: 2 },
          { DataType: 'PayeeMatchingRules', RecordCount: 4 },
        ]),
      }),
    )
    vi.spyOn(TenantDataAdminClient.prototype, 'getExportOptions').mockImplementation(
      getExportOptions,
    )
    vi.spyOn(TenantDataAdminClient.prototype, 'exportData').mockImplementation(exportData)
    vi.stubGlobal('useApiClient', useApiClientMock)
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:export'),
      revokeObjectURL: vi.fn(),
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  })

  it('loads exportable types, defaults to all selected, and provides selection controls', async () => {
    const wrapper = await mountPane()

    expect(useApiClientMock).toHaveBeenLastCalledWith(TenantDataAdminClient)
    expect(getExportOptions).toHaveBeenCalledWith('workspace-key')
    expect(wrapper.findAll('[data-test-id="export-type"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Payee Matching Rules')
    expect(wrapper.text()).toContain('(includes splits)')
    expect(wrapper.find('[data-test-id="export-selected-button"]').text()).toContain(
      'Export Data (2 types selected)',
    )

    await wrapper.find('[data-test-id="clear-export-types"]').trigger('click')
    expect(
      wrapper.find('[data-test-id="export-selected-button"]').attributes('disabled'),
    ).toBeDefined()
    await wrapper.find('[data-test-id="select-all-export-types"]').trigger('click')
    expect(
      wrapper.find('[data-test-id="export-selected-button"]').attributes('disabled'),
    ).toBeUndefined()
  })

  it('sends the selected types and lets the backend decide whether this is a complete export', async () => {
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="clear-export-types"]').trigger('click')
    for (const checkbox of wrapper.findAll<HTMLInputElement>('[data-test-id="export-type"]')) {
      await checkbox.setValue(true)
    }
    await wrapper.find('[data-test-id="export-selected-button"]').trigger('click')
    await flushPromises()

    expect(exportData).toHaveBeenCalledWith(
      'workspace-key',
      ['Transactions', 'PayeeMatchingRules'],
      undefined,
      undefined,
      false,
    )
  })

  it('converts a selected year to inclusive start and end dates', async () => {
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="export-date-selection"]').setValue('year:2023')
    await wrapper.find('[data-test-id="export-selected-button"]').trigger('click')
    await flushPromises()

    expect(exportData).toHaveBeenCalledWith(
      'workspace-key',
      ['Transactions', 'PayeeMatchingRules'],
      fromDateInputValue('2023-01-01'),
      fromDateInputValue('2023-12-31'),
      false,
    )
  })

  it('supports an open-ended custom date range', async () => {
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="export-date-selection"]').setValue('custom')
    await wrapper.find('[data-test-id="export-start-date"]').setValue('2024-03-15')
    await wrapper.find('[data-test-id="export-selected-button"]').trigger('click')
    await flushPromises()

    expect(exportData).toHaveBeenCalledWith(
      'workspace-key',
      ['Transactions', 'PayeeMatchingRules'],
      fromDateInputValue('2024-03-15'),
      undefined,
      false,
    )
  })

  it('prevents a reversed custom range', async () => {
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="export-date-selection"]').setValue('custom')
    await wrapper.find('[data-test-id="export-start-date"]').setValue('2024-03-16')
    await wrapper.find('[data-test-id="export-end-date"]').setValue('2024-03-15')

    expect(wrapper.find('[data-test-id="export-range-error"]').text()).toContain(
      'The start date must be on or before the end date.',
    )
    expect(
      wrapper.find('[data-test-id="export-selected-button"]').attributes('disabled'),
    ).toBeDefined()
    expect(exportData).not.toHaveBeenCalled()
  })

  it('downloads all data without type or date filters and displays worksheet counts', async () => {
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="export-date-selection"]').setValue('year:2023')
    await wrapper.find('[data-test-id="export-all-data-button"]').trigger('click')
    await flushPromises()

    expect(exportData).toHaveBeenCalledWith('workspace-key', [], undefined, undefined, true)
    expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:export')
    expect(toastSuccess).toHaveBeenCalledWith(
      'Downloaded yofi-export.xlsx. Transactions: 2; Payee Matching Rules: 4',
      'Export complete',
      'workspace-export-complete',
    )
    expect(wrapper.emitted('update:busy')).toEqual([[true], [false]])
  })

  it('shows an error when export fails', async () => {
    exportData.mockRejectedValue(new Error('Network unavailable'))
    const wrapper = await mountPane()
    await wrapper.find('[data-test-id="export-selected-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="export-error"]').text()).toContain('Network unavailable')
    expect(wrapper.emitted('update:busy')).toEqual([[true], [false]])
  })
})
