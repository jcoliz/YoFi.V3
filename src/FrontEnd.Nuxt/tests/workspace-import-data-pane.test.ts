import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { h, type Slots } from 'vue'
import ImportDataPane from '~/components/workspace/ImportDataPane.vue'
import { TenantDataAdminClient, type TenantRoleResultDto } from '~/utils/apiclient'

vi.mock('~/utils/errorHandler', () => ({
  handleApiError: (err: unknown, title = 'Error', detail?: string) => ({
    title,
    detail,
    status:
      err && typeof err === 'object' && 'status' in err ? (err as { status: number }).status : 400,
  }),
}))

const { toastSuccess } = vi.hoisted(() => ({ toastSuccess: vi.fn() }))
vi.mock('~/stores/toast', () => ({
  useToastStore: () => ({ success: toastSuccess }),
}))

const previewImport = vi.fn()
const importData = vi.fn()
const useApiClientMock = vi.fn(
  (Client: typeof TenantDataAdminClient) => new Client('', { fetch: vi.fn() }),
)
vi.stubGlobal('useApiClient', useApiClientMock)

const workspace = { key: 'workspace-key', name: 'Test Workspace' } as TenantRoleResultDto
const preview = {
  formatId: 'original-yofi',
  canImport: true,
  sha256: 'preview-digest',
  ignoredWorksheets: ['Transaction'],
  worksheets: [
    {
      worksheetName: 'Payee',
      dataType: 'PayeeMatchingRules',
      validRecordCount: 2,
      skippedRecordCount: 1,
      canImport: true,
      errors: [],
      skippedRows: [
        { rowNumber: 4, field: 'Category', code: 'blank', message: 'Category is blank' },
      ],
    },
  ],
}

const ModalDialogStub = {
  props: ['show', 'testId', 'primaryButtonTestId', 'secondaryButtonTestId', 'loading'],
  emits: ['primary', 'update:show'],
  setup(
    props: { show: boolean; testId: string; primaryButtonTestId: string },
    { slots, emit }: { slots: Slots; emit: (event: string) => void },
  ) {
    return () =>
      props.show
        ? h('div', { 'data-test-id': props.testId }, [
            slots.default?.(),
            h(
              'button',
              { 'data-test-id': props.primaryButtonTestId, onClick: () => emit('primary') },
              'Confirm',
            ),
          ])
        : null
  },
}

const globalConfig = {
  components: {
    CardTitle: { template: '<h2>{{ title }}</h2>', props: ['title', 'subtitle'] },
    ErrorDisplay: {
      template: '<div v-if="show" class="error-display">{{ problem?.detail }}</div>',
      props: ['show', 'problem'],
    },
    BaseSpinner: { template: '<span />' },
    HelpText: { template: '<span><slot /></span>' },
    ModalDialog: ModalDialogStub,
  },
}

async function selectWorkbook(wrapper: ReturnType<typeof mount>) {
  const file = new File(['workbook bytes'], 'legacy.xlsx')
  const input = wrapper.find<HTMLInputElement>('[data-test-id="import-file-input"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  await input.trigger('change')
  return file
}

describe('Workspace ImportDataPane', () => {
  beforeEach(() => {
    vi.restoreAllMocks()
    toastSuccess.mockReset()
    previewImport.mockReset().mockResolvedValue(preview)
    importData.mockReset().mockResolvedValue({
      worksheets: [
        {
          worksheetName: 'Payee',
          created: 2,
          skipped: 1,
          updated: 0,
          failed: 0,
        },
      ],
    })
    vi.spyOn(TenantDataAdminClient.prototype, 'previewImport').mockImplementation(previewImport)
    vi.spyOn(TenantDataAdminClient.prototype, 'importData').mockImplementation(importData)
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:import-errors'),
      revokeObjectURL: vi.fn(),
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  })

  it('previews the selected workbook and imports only after confirmation', async () => {
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    const file = await selectWorkbook(wrapper)

    expect(useApiClientMock).toHaveBeenLastCalledWith(TenantDataAdminClient)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(previewImport).toHaveBeenCalledWith('workspace-key', {
      data: file,
      fileName: 'legacy.xlsx',
    })
    expect(wrapper.text()).toContain('2 valid')
    expect(wrapper.text()).toContain('Other worksheets will not be imported: Transaction')

    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    expect(wrapper.find('[data-test-id="import-data-confirm-modal"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Original payee rules are create-only')

    await wrapper.find('[data-test-id="import-data-confirm-button"]').trigger('click')
    await flushPromises()

    expect(importData).toHaveBeenCalledWith(
      'workspace-key',
      { data: file, fileName: 'legacy.xlsx' },
      'preview-digest',
    )
    expect(wrapper.emitted('imported')).toHaveLength(1)
    expect(toastSuccess).toHaveBeenCalledWith(
      'Payee: 2 created, 1 skipped',
      'Import complete',
      'import-result',
    )
    expect(wrapper.find('[data-test-id="import-result"]').exists()).toBe(false)
  })

  it('requires a new preview when the selected workbook changes', async () => {
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    expect(
      wrapper.find('[data-test-id="import-data-button"]').attributes('disabled'),
    ).toBeUndefined()

    const replacement = new File(['another workbook'], 'replacement.xlsx')
    const input = wrapper.find<HTMLInputElement>('[data-test-id="import-file-input"]')
    Object.defineProperty(input.element, 'files', { value: [replacement], configurable: true })
    await input.trigger('change')

    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-data-button"]').exists()).toBe(false)
  })

  it('shows row validation issues and blocks importing invalid workbooks', async () => {
    previewImport.mockResolvedValue({
      ...preview,
      canImport: false,
      worksheets: [
        {
          ...preview.worksheets[0],
          canImport: false,
          errors: [{ rowNumber: 3, field: 'Name', code: 'invalid', message: 'Pattern is invalid' }],
        },
      ],
    })
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="import-row-issues"]').text()).toContain(
      'Pattern is invalid',
    )
    expect(wrapper.find('[data-test-id="download-import-errors-button"]').exists()).toBe(true)
    expect(wrapper.find('[data-test-id="import-data-button"]').attributes('disabled')).toBeDefined()
  })

  it('requires a new preview after the import endpoint rejects the workbook', async () => {
    importData.mockRejectedValue(new Error('Digest mismatch'))
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-data-confirm-button"]').trigger('click')
    await flushPromises()

    expect(importData).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-data-confirm-modal"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-data-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="preview-import-button"]').exists()).toBe(true)
  })

  it('reports only nonzero import counts in the success toast', async () => {
    importData.mockResolvedValue({
      worksheets: [
        { worksheetName: 'Transactions', created: 559, skipped: 0, updated: 0, failed: 0 },
        { worksheetName: 'Splits', created: 0, skipped: 0, updated: 0, failed: 0 },
      ],
    })
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-data-confirm-button"]').trigger('click')
    await flushPromises()

    expect(toastSuccess).toHaveBeenCalledWith(
      'Transactions: 559 created',
      'Import complete',
      'import-result',
    )
  })

  it('renders native and original format labels and payee warnings accurately', async () => {
    previewImport.mockResolvedValueOnce({
      formatId: 'yofi.v3',
      sha256: 'native-digest',
      canImport: true,
      worksheets: [
        {
          worksheetName: 'Transactions',
          dataType: 'Transactions',
          validRecordCount: 5,
          skippedRecordCount: 1,
          canImport: true,
          errors: [],
        },
        {
          worksheetName: 'Splits',
          dataType: 'Splits',
          validRecordCount: 2,
          skippedRecordCount: 0,
          canImport: true,
          errors: [],
        },
        {
          worksheetName: 'Payees',
          dataType: 'PayeeMatchingRules',
          validRecordCount: 3,
          skippedRecordCount: 0,
          canImport: true,
          errors: [],
        },
      ],
      ignoredWorksheets: [],
    })

    const nativeWrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(nativeWrapper)
    await nativeWrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(nativeWrapper.find('[data-test-id="import-format"]').text()).toBe('YoFi.V3')
    expect(
      nativeWrapper.find('[data-test-id="import-worksheet-preview-transactions"]').text(),
    ).toContain('5 valid')
    expect(nativeWrapper.find('[data-test-id="import-worksheet-preview-splits"]').text()).toContain(
      '2 valid',
    )
    expect(nativeWrapper.text()).toContain('existing matching Keys are updated')

    previewImport.mockResolvedValueOnce(preview)
    const originalWrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(originalWrapper)
    await originalWrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(originalWrapper.find('[data-test-id="import-format"]').text()).toBe('Original YoFi')
    expect(originalWrapper.text()).toContain('Original payees are added as create-only rules')
    expect(originalWrapper.text()).not.toContain('existing matching Keys are updated')
  })

  it('requires transactions when a native split preview is present', async () => {
    previewImport.mockResolvedValue({
      formatId: 'yofi.v3',
      sha256: 'split-digest',
      canImport: true,
      worksheets: [
        {
          worksheetName: 'Splits',
          dataType: 'Splits',
          validRecordCount: 2,
          skippedRecordCount: 0,
          canImport: true,
          errors: [],
        },
      ],
      ignoredWorksheets: [],
    })

    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="import-worksheet-preview-splits"]').text()).toContain(
      'Splits',
    )
    expect(wrapper.find('[data-test-id="import-data-button"]').attributes('disabled')).toBeDefined()
  })

  it('clears preview and confirmation state when the selected file changes', async () => {
    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    expect(wrapper.find('[data-test-id="import-data-confirm-modal"]').exists()).toBe(true)

    const replacement = new File(['another workbook'], 'replacement.xlsx')
    const input = wrapper.find<HTMLInputElement>('[data-test-id="import-file-input"]')
    Object.defineProperty(input.element, 'files', { value: [replacement], configurable: true })
    await input.trigger('change')

    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-data-confirm-modal"]').exists()).toBe(false)
  })

  it('enters the preview failure state and does not show a preview', async () => {
    previewImport.mockRejectedValue(new Error('Preview failed'))
    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="preview-import-button"]').text()).toContain(
      'Preview Import',
    )
    expect(wrapper.find('.error-display').text()).toContain('Failed to preview workbook')
  })

  it('reports ignored columns and ignored worksheets', async () => {
    previewImport.mockResolvedValue({
      ...preview,
      worksheets: [{ ...preview.worksheets[0], ignoredColumns: ['LegacyCategory', 'Unknown'] }],
    })

    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="import-ignored-columns"]').text()).toContain(
      'LegacyCategory, Unknown',
    )
    expect(wrapper.find('[data-test-id="import-ignored-worksheets"]').text()).toContain(
      'Transaction',
    )
  })

  it('downloads only preview issues and formula-sanitizes CSV values', async () => {
    previewImport.mockResolvedValue({
      ...preview,
      worksheets: [
        {
          ...preview.worksheets[0],
          skippedRows: [{ rowNumber: 4, field: '@Category', code: 'blank', message: '=SUM(1,2)' }],
          ignoredColumns: [],
        },
      ],
    })

    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-test-id="download-import-errors-button"]').trigger('click')

    expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:import-errors')
    const blob = (URL.createObjectURL as ReturnType<typeof vi.fn>).mock.calls[0]![0] as Blob
    const csv = await blob.text()
    expect(csv).toContain('"\'@Category"')
    expect(csv).toContain('"\'=SUM(1,2)"')
    expect(csv).not.toContain('Pattern is invalid')
  })

  it('keeps confirmation open and shows persistence failures without success', async () => {
    importData.mockRejectedValue({ status: 500 })
    const wrapper = mount(ImportDataPane, { props: { workspace }, global: globalConfig })
    await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-data-confirm-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test-id="import-data-confirm-modal"]').exists()).toBe(true)
    expect(
      wrapper.find('[data-test-id="import-data-confirm-modal"] .error-display').text(),
    ).toContain('Failed to import workbook')
    expect(toastSuccess).not.toHaveBeenCalled()
  })

  it('uses the generated client to send multipart preview and import requests', async () => {
    vi.restoreAllMocks()
    const fetch = vi
      .fn()
      .mockResolvedValueOnce(new Response(JSON.stringify(preview), { status: 200 }))
      .mockResolvedValueOnce(
        new Response(
          JSON.stringify({
            worksheets: [{ worksheetName: 'Payee', created: 2, skipped: 1, updated: 0, failed: 0 }],
          }),
          { status: 200 },
        ),
      )
    useApiClientMock.mockImplementationOnce((Client) => new Client('', { fetch }))
    const wrapper = mount(ImportDataPane, {
      props: { workspace },
      global: globalConfig,
    })
    const file = await selectWorkbook(wrapper)
    await wrapper.find('[data-test-id="preview-import-button"]').trigger('click')
    await flushPromises()

    expect(fetch).toHaveBeenNthCalledWith(
      1,
      '/api/tenant/workspace-key/admin/import/preview',
      expect.objectContaining({ method: 'POST', body: expect.any(FormData) }),
    )
    const previewForm = fetch.mock.calls[0]![1].body as FormData
    expect(previewForm.get('file')).toEqual(file)

    await wrapper.find('[data-test-id="import-data-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-data-confirm-button"]').trigger('click')
    await flushPromises()

    expect(fetch).toHaveBeenNthCalledWith(
      2,
      '/api/tenant/workspace-key/admin/import',
      expect.objectContaining({ method: 'POST', body: expect.any(FormData) }),
    )
    const importForm = fetch.mock.calls[1]![1].body as FormData
    expect(importForm.get('file')).toEqual(file)
    expect(importForm.get('expectedSha256')).toBe('preview-digest')
    expect(toastSuccess).toHaveBeenCalledWith(
      'Payee: 2 created, 1 skipped',
      'Import complete',
      'import-result',
    )
  })
})
