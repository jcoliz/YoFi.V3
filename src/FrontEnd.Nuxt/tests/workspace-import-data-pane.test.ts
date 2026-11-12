import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { h } from 'vue'
import ImportDataPane from '~/components/workspace/ImportDataPane.vue'
import { TenantDataAdminClient, type TenantRoleResultDto } from '~/utils/apiclient'

vi.mock('~/utils/errorHandler', () => ({
  handleApiError: (_err: unknown, title = 'Error', detail?: string) => ({
    title,
    detail,
    status: 400,
  }),
}))

const previewImport = vi.fn()
const importData = vi.fn()
const useApiClientMock = vi.fn(
  (Client: typeof TenantDataAdminClient) => new Client('', { fetch: vi.fn() }),
)
vi.stubGlobal('useApiClient', useApiClientMock)

const workspace = { key: 'workspace-key', name: 'Test Workspace' } as TenantRoleResultDto
const preview = {
  canImport: true,
  sha256: 'preview-digest',
  ignoredWorksheets: ['Transaction'],
  worksheets: [
    {
      worksheetName: 'Payee',
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
    { slots, emit }: { slots: { default?: () => unknown }; emit: (event: string) => void },
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
    ErrorDisplay: { template: '<div v-if="show" />', props: ['show', 'problem'] },
    BaseSpinner: { template: '<span />' },
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
    expect(wrapper.text()).toContain('2 valid rules')
    expect(wrapper.text()).toContain('Other worksheets will not be imported: Transaction')

    await wrapper.find('[data-test-id="import-payees-button"]').trigger('click')
    expect(wrapper.find('[data-test-id="import-payees-confirm-modal"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Importing this workbook again creates duplicate rules')

    await wrapper.find('[data-test-id="import-payees-confirm-button"]').trigger('click')
    await flushPromises()

    expect(importData).toHaveBeenCalledWith(
      'workspace-key',
      { data: file, fileName: 'legacy.xlsx' },
      'preview-digest',
    )
    expect(wrapper.emitted('imported')).toHaveLength(1)
    expect(wrapper.find('[data-test-id="import-result"]').text()).toContain('2 created')
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
      wrapper.find('[data-test-id="import-payees-button"]').attributes('disabled'),
    ).toBeUndefined()

    const replacement = new File(['another workbook'], 'replacement.xlsx')
    const input = wrapper.find<HTMLInputElement>('[data-test-id="import-file-input"]')
    Object.defineProperty(input.element, 'files', { value: [replacement], configurable: true })
    await input.trigger('change')

    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-payees-button"]').exists()).toBe(false)
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
    expect(
      wrapper.find('[data-test-id="import-payees-button"]').attributes('disabled'),
    ).toBeDefined()
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
    await wrapper.find('[data-test-id="import-payees-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-payees-confirm-button"]').trigger('click')
    await flushPromises()

    expect(importData).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-test-id="import-preview"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-payees-confirm-modal"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="import-payees-button"]').exists()).toBe(false)
    expect(wrapper.find('[data-test-id="preview-import-button"]').exists()).toBe(true)
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

    await wrapper.find('[data-test-id="import-payees-button"]').trigger('click')
    await wrapper.find('[data-test-id="import-payees-confirm-button"]').trigger('click')
    await flushPromises()

    expect(fetch).toHaveBeenNthCalledWith(
      2,
      '/api/tenant/workspace-key/admin/import',
      expect.objectContaining({ method: 'POST', body: expect.any(FormData) }),
    )
    const importForm = fetch.mock.calls[1]![1].body as FormData
    expect(importForm.get('file')).toEqual(file)
    expect(importForm.get('expectedSha256')).toBe('preview-digest')
    expect(wrapper.find('[data-test-id="import-result"]').text()).toContain('2 created')
  })
})
