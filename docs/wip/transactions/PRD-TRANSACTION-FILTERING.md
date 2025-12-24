---
status: Approved (Detailed Design Complete)
owner: James Coliz
target_release: Beta 1
ado: "[Feature 1983](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/1983): Transaction Filtering"
---

# Product Requirements Document: Transaction Filtering

## Problem Statement

Users need to find specific transactions quickly from potentially hundreds or thousands of records. Currently, only basic date range filtering exists. Users cannot search by payee, category, amount, or find uncategorized transactions. This makes transaction management tedious and time-consuming.

---

## Goals & Non-Goals

### Goals
- [ ] Enable fast multi-field text search (payee, split categories, split memos, memo, amount) - most common use case
- [ ] Support finding uncategorized transactions (transactions with any splits having empty categories)
- [ ] Provide date range filtering with smart defaults (last 12 months)
- [ ] Allow field-specific searches for precision filtering
- [ ] Support filtering by balance status (balanced vs. unbalanced transactions)
- [ ] Maintain minimal, uncluttered UI (progressive disclosure)

### Non-Goals
- Complex boolean query syntax (AND/OR/NOT operators)
- Saved filter presets (future enhancement)
- Advanced analytics on filtered results (future enhancement)

---

## User Stories

### Story 1: User - Quick Text Search
**As a** user
**I want** to search all fields with a single text input
**So that** I can quickly find transactions without specifying which field to search

**Acceptance Criteria**:
- [ ] Single search bar always visible at top of transactions page
- [ ] Searches across payee, split categories, split memos, transaction memo, and amount fields
- [ ] When a transaction has multiple splits, match if ANY split matches the search criteria
- [ ] Results update as user types (debounced)
- [ ] Clear button (×) appears when text is present

### Story 2: User - Find Uncategorized Transactions
**As a** user
**I want** to filter for uncategorized transactions
**So that** I can quickly categorize newly imported transactions

**Acceptance Criteria**:
- [ ] Checkbox filter for "Uncategorized only"
- [ ] Finds transactions where ANY split has blank or whitespace category
- [ ] Transactions with mixed categorized/uncategorized splits ARE included (they need attention)
- [ ] Works in combination with other filters (date range, text search)

### Story 3: User - Default Date Range
**As a** user
**I want** transactions filtered to last 12 months by default
**So that** I see recent data without manual filtering on every visit

**Acceptance Criteria**:
- [ ] Transactions default to prior 12 months on initial load
- [ ] Visual indicator shows active 12-month filter (dismissible chip)
- [ ] User can clear default filter to see all transactions
- [ ] Preference persists in browser (localStorage)

### Story 4: User - Advanced Filtering
**As a** power user
**I want** to filter by specific fields and date ranges
**So that** I can create precise filtered views (e.g., "Starbucks in 2024")

**Acceptance Criteria**:
- [ ] Collapsible filter panel hidden by default
- [ ] Field-specific inputs for payee, split category, split memo, transaction memo
- [ ] Exact amount search (matches transaction amount, not split amounts)
- [ ] Balance status filter: All / Balanced only / Unbalanced only
- [ ] Date range quick-select buttons (Last 30d, 3mo, 12mo, This year, All time)
- [ ] Custom date range inputs (From/To)
- [ ] "Clear All" button resets all filters

### Story 5: User - Filter by Balance Status
**As a** user
**I want** to find transactions where splits don't balance
**So that** I can quickly fix data entry errors

**Acceptance Criteria**:
- [ ] Filter option for "Unbalanced only" (splits sum ≠ transaction amount)
- [ ] Filter option for "Balanced only" (splits sum = transaction amount)
- [ ] Balance status visible in transaction list (indicator or badge)
- [ ] Works in combination with other filters (date range, text search, uncategorized)

### Story 6: Reports User - Investigates underlying transactions [NEW]
**As a** User
**I want** discover which transactions exactly comprise one of the numbers shown
**So that** I can understand what underlying actions caused the result I'm seeing

See [`PRD-REPORTS`](../reports/PRD-REPORTS.md).

**Acceptance Criteria**:
- [ ] When viewing a report, user can select any number to understand what transactions comprise that total.
- [ ] User cannot construct a filter by hand which matches a report filter. Reports can give private search query which we will need to implement.

---

## Technical Approach

Implement collapsible filter bar pattern with search-first UX (similar to Monarch Money, YNAB).

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - Filter bar component with collapsible panel
- [x] Controllers (API endpoints) - Add query parameters to GetTransactions
- [x] Application (Features) - Filter logic in TransactionsFeature with split-aware queries
- [ ] Database (Indexes) - Add indexes on Payee, Split.Category for performance

**Key Components**:
- **New**: `src/FrontEnd.Nuxt/app/components/TransactionFilterBar.vue` - Filter UI component
- **Modified**: `src/FrontEnd.Nuxt/app/pages/transactions.vue` - Integrate filter bar
- **Modified**: `src/Controllers/TransactionsController.cs` - Add filter query parameters
- **Modified**: `src/Application/Features/TransactionsFeature.cs` - Filter query logic

**API Design**:
```
GET /api/tenant/{key}/transactions?search=starbucks&fromDate=2024-01-01&toDate=2024-12-31&uncategorizedOnly=true&balanceStatus=unbalanced
```

**Filter Parameters**:
- `search` - Multi-field substring search across payee, split categories, split memos, transaction memo, amount (OR logic within transaction/splits)
- `payee` - Field-specific substring search on transaction payee (AND logic)
- `category` - Field-specific substring search on split categories (matches if ANY split matches)
- `memo` - Field-specific substring search on transaction memo OR split memos (matches if ANY matches)
- `amount` - Exact amount match on transaction amount (not split amounts)
- `fromDate`, `toDate` - Date range on transaction date
- `uncategorizedOnly` - Boolean: if true, only transactions where ANY split has empty category
- `balanceStatus` - Enum: `all` (default), `balanced`, `unbalanced` (compares sum of split amounts to transaction amount)

---

## Open Questions

- [x] **Q**: Should we support cryptic text syntax (e.g., "c=groceries,y=2024")?
  **A**: NO - Poor UX, not discoverable. Use explicit filter panel instead.

- [x] **Q**: Always-visible filters vs. collapsible panel?
  **A**: Collapsible panel. Aligns with minimal/modern preference and industry best practices.

- [x] **Q**: How to handle amount search in multi-field search?
  **A**: Parse search text as decimal first. If valid, include transaction amount field with exact match. Otherwise skip amount field. Do NOT search split amounts.

- [x] **Q**: How to search split data efficiently?
  **A**: Use EF Core's `.Any()` on the Splits navigation property. Example: `query.Where(t => t.Splits.Any(s => s.Category.Contains(searchTerm)))`. EF Core will generate efficient SQL with EXISTS clauses.

- [x] **Q**: Should category filter match partial split categories or require all splits to match?
  **A**: Category filter matches if ANY split matches (OR logic across splits). This is intuitive - user searching for "Food" wants transactions that include food, even if they also include other categories.

- [x] **Q**: Should uncategorizedOnly require ALL splits to be uncategorized or just ANY split uncategorized?
  **A**: ANY split uncategorized. Transactions with mixed categorized/uncategorized splits need attention just as much as fully uncategorized transactions.

- [x] **Q**: How to handle balance status filtering performance?
  **A**: Add computed property `IsBalanced` to `TransactionResultDto` (calculated in Application layer during query). Frontend filters based on DTO property. Backend doesn't need SQL-level filtering for balance status (calculation requires loading splits anyway).

- [ ] **Q**: Should filters be shareable via URL parameters?
  **A**: YES (future enhancement) - Enable bookmarking and sharing filtered views.

---

## Success Metrics

**Usage**:
- % of users using text search vs. advanced filters
- Most common filter combinations
- Average time to find specific transaction

**Performance**:
- Search response time (target: <200ms for 10k transactions)
- Filter panel open/close interaction rate

---

## Dependencies & Constraints

**Dependencies**:
- ✅ Transaction Record implementation (memo, source, externalId fields) - COMPLETED
- ✅ Transaction Splits implementation (Split entity with category, memo, amount) - COMPLETED
- Existing transaction list infrastructure

**Constraints**:
- Must maintain fast performance with 10k+ transactions (including splits)
- Mobile-friendly filter UI required
- Backend must use database-level filtering (not client-side)
- Split searches must use efficient SQL (EF Core `.Any()` generates EXISTS clauses)

---

## Notes & Context

**Design Documents**:
- UI design: [`TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md`](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) (comprehensive 651-line spec - NEEDS REVIEW for splits compatibility)
- Initial requirements: [`TRANSACTION-FILTERING.md`](TRANSACTION-FILTERING.md)

**Key Design Decisions**:
1. **Search-first UX** - Always-visible search bar, collapsible advanced filters (progressive disclosure)
2. **Multi-field OR search** - Primary search bar searches all transaction and split fields with OR logic (broad, fast)
3. **Field-specific AND search** - Advanced panel provides field-specific inputs with AND logic (precise, narrow)
4. **Split-aware filtering** - Category and memo searches check both transaction-level AND all splits (matches if ANY split matches)
5. **Backend filtering** - All filtering at database level for performance (not client-side)
6. **Smart defaults** - 12-month date range by default, user can clear or adjust
7. **Balance status awareness** - Expose balance status as filterable property (leverages splits feature)

**Industry Patterns**:
- Monarch Money, YNAB use collapsible filter pattern
- Gmail, Notion use similar progressive disclosure
- Traditional finance apps (QuickBooks, Mint) use always-visible filters (rejected as cluttered)

---

## Handoff Checklist

Implementation planning complete:
- [x] UI pattern selected (collapsible filter bar)
- [x] Component architecture designed
- [x] API contract defined (UPDATED for splits)
- [x] Filter logic specified (UPDATED for splits)
- [x] Performance considerations documented (UPDATED for split queries)
- [x] Mobile responsive strategy defined
- [x] Accessibility requirements specified

**Action Required**: Review [`TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md`](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) to ensure it reflects split-aware filtering patterns. The 651-line spec was written before splits were implemented and may need updates for:
- Split category filtering UI/UX
- Split memo searching
- Balance status indicators in list view
- Filter panel terminology (distinguish transaction memo vs split memos)
