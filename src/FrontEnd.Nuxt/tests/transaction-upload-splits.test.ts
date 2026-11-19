import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, type VNodeChild } from 'vue'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import UploadSplitsDialog from '~/components/transaction/UploadSplitsDialog.vue'
import ModalDialog from '~/components/ModalDialog.vue'
import ErrorDisplay from '~/components/ErrorDisplay.vue'
import { ProblemDetails, TransactionDetailDto } from '~/utils/apiclient'

const uploadSplits = vi.fn()
const getSplitUploadTemplate = vi.fn()
vi.stubGlobal('useApiClient', (Client: { name: string }) =>
  Client.name === 'TransactionsClient' ? { uploadSplits } : { getSplitUploadTemplate },
)
enableAutoUnmount(afterEach)

const updated = new TransactionDetailDto({
  key: 'transaction',
  amount: -100,
  splits: [],
})

const ModalDialogStub = defineComponent({
  props: [
    'show',
    'title',
    'loading',
    'primaryButtonDisabled',
    'primaryButtonTestId',
    'secondaryButtonTestId',
    'testId',
  ],
  emits: ['primary', 'update:show'],
  setup(props, { slots, emit }) {
    return () =>
      props.show
        ? h('div', { 'data-test-id': props.testId }, [
            slots.default?.() as VNodeChild,
            h(
              'button',
              {
                type: 'button',
                'data-test-id': props.secondaryButtonTestId,
                disabled: props.loading ? true : undefined,
                onClick: () => emit('update:show', false),
              },
              'Cancel',
            ),
            h(
              'button',
              {
                type: 'button',
                'data-test-id': props.primaryButtonTestId,
                disabled: props.loading || props.primaryButtonDisabled ? true : undefined,
                onClick: () => emit('primary'),
              },
              props.loading ? 'Loading...' : 'Upload',
            ),
          ])
        : null
  },
})

function mountDialog() {
  return mount(UploadSplitsDialog, {
    attachTo: document.body,
    props: { show: true, tenantKey: 'workspace', transactionKey: 'transaction' },
    global: {
      components: { ModalDialog, ErrorDisplay },
      stubs: {
        ModalDialog: ModalDialogStub,
        teleport: true,
        BaseSpinner: true,
        FeatherIcon: true,
      },
    },
  })
}

async function selectWorkbook(
  wrapper: ReturnType<typeof mountDialog>,
  fileName = 'splits.xlsx',
  size = 100,
) {
  const file =
    size === 100
      ? new File(['workbook bytes'], fileName)
      : new File([new Uint8Array(size)], fileName)
  const input = wrapper.get<HTMLInputElement>('[data-test-id="upload-splits-file"]')
  Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
  await input.trigger('change')
  return file
}

function deferredUpload() {
  let resolve!: (value: TransactionDetailDto) => void
  const promise = new Promise<TransactionDetailDto>((done) => {
    resolve = done
  })
  uploadSplits.mockReturnValue(promise)
  return resolve
}

describe('transaction split upload dialog', () => {
  beforeEach(() => {
    uploadSplits.mockReset().mockResolvedValue(updated)
    getSplitUploadTemplate.mockReset().mockResolvedValue({
      fileName: 'yofi-transaction-splits-template.xlsx',
      data: new Blob(['xlsx']),
      status: 200,
      headers: {},
    })
    vi.stubGlobal('URL', {
      createObjectURL: vi.fn(() => 'blob:split-template'),
      revokeObjectURL: vi.fn(),
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('uploads the selected workbook and emits the returned transaction', async () => {
    const wrapper = mountDialog()
    const file = await selectWorkbook(wrapper)

    await wrapper.get('[data-test-id="upload-splits-submit-button"]').trigger('click')
    await flushPromises()

    expect(uploadSplits).toHaveBeenCalledExactlyOnceWith(
      'transaction',
      'workspace',
      expect.objectContaining({ fileName: 'splits.xlsx', data: file }),
    )
    expect(wrapper.emitted('uploaded')).toEqual([[updated]])
    expect(wrapper.emitted('update:show')).toEqual([[false]])
  })

  it('downloads the generated template through the authenticated client', async () => {
    const wrapper = mountDialog()

    await wrapper.get('[data-test-id="download-splits-template-button"]').trigger('click')
    await flushPromises()

    expect(getSplitUploadTemplate).toHaveBeenCalledOnce()
    expect(uploadSplits).not.toHaveBeenCalled()
    expect(URL.createObjectURL).toHaveBeenCalledWith(expect.any(Blob))
    expect(HTMLAnchorElement.prototype.click).toHaveBeenCalledOnce()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:split-template')
  })

  it('rejects unsupported names and files larger than 1 MiB locally', async () => {
    const wrapper = mountDialog()

    await selectWorkbook(wrapper, 'splits.csv')
    expect(wrapper.text()).toContain('Unsupported workbook')
    expect(wrapper.text()).toContain('.xlsx')
    expect(uploadSplits).not.toHaveBeenCalled()

    await selectWorkbook(wrapper, 'large.xlsx', 1024 * 1024 + 1)
    expect(wrapper.text()).toContain('1 MB')
    expect(uploadSplits).not.toHaveBeenCalled()
  })

  it('keeps the selection open and renders structured 400 row errors', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    uploadSplits.mockRejectedValueOnce(
      new ProblemDetails({
        status: 400,
        title: 'The split workbook failed validation.',
        detail: 'Correct the workbook errors and upload it again. No splits were changed.',
        errors: {
          'Row 2: Category': ['Category cannot exceed 200 characters'],
          'Row 3: Memo': ['Memo cannot exceed 500 characters'],
        },
      }),
    )
    const wrapper = mountDialog()
    await selectWorkbook(wrapper)

    await wrapper.get('[data-test-id="upload-splits-submit-button"]').trigger('click')
    await flushPromises()

    expect(wrapper.get('[data-test-id="upload-splits-error"]').text()).toContain('Row 2: Category')
    expect(wrapper.get('[data-test-id="upload-splits-error"]').text()).toContain('Row 3: Memo')
    expect(wrapper.get('[data-test-id="upload-splits-selected-file"]').text()).toContain(
      'splits.xlsx',
    )
    expect(wrapper.emitted('update:show')).toBeUndefined()
    expect(
      wrapper
        .get<HTMLButtonElement>('[data-test-id="upload-splits-submit-button"]')
        .attributes('disabled'),
    ).toBeUndefined()

    uploadSplits.mockResolvedValueOnce(updated)
    await wrapper.get('[data-test-id="upload-splits-submit-button"]').trigger('click')
    await flushPromises()
    expect(wrapper.emitted('uploaded')).toEqual([[updated]])
  })

  it('guards duplicate submissions and dismissal while an upload is pending', async () => {
    const resolve = deferredUpload()
    const wrapper = mountDialog()
    await selectWorkbook(wrapper)

    await wrapper.get('[data-test-id="upload-splits-submit-button"]').trigger('click')
    await wrapper.get('[data-test-id="upload-splits-submit-button"]').trigger('click')
    await wrapper.get('[data-test-id="upload-splits-cancel-button"]').trigger('click')
    wrapper.getComponent(ModalDialog).vm.$emit('update:show', false)
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

    expect(uploadSplits).toHaveBeenCalledOnce()
    expect(wrapper.emitted('update:show')).toBeUndefined()
    expect(wrapper.get('[data-test-id="upload-splits-pending"]').text()).toContain('Uploading')
    expect(wrapper.emitted('update:busy')).toContainEqual([true])

    resolve(updated)
    await flushPromises()
    expect(wrapper.emitted('uploaded')).toEqual([[updated]])
    expect(wrapper.emitted('update:busy')).toContainEqual([false])
  })
})
