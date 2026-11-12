export interface TransactionAdvancedFilters {
  fromDate: string
  toDate: string
  payee: string
  category: string
  memo: string
  amount: string
  uncategorizedOnly: boolean
}

export function emptyTransactionAdvancedFilters(): TransactionAdvancedFilters {
  return {
    fromDate: '',
    toDate: '',
    payee: '',
    category: '',
    memo: '',
    amount: '',
    uncategorizedOnly: false,
  }
}
