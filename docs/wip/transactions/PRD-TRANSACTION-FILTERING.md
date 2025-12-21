# Product Requirements Document: Transaction Filtering

**Status**: Approved (Detailed Design Complete)
**Created**: 2025-12-21
**Owner**: James Coliz
**Target Release**: V3.0
**ADO**: [Feature 1983](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/1983): Transaction Filtering

---

## Problem Statement

Users need to find specific transactions quickly from potentially hundreds or thousands of records. Currently, only basic date range filtering exists. Users cannot search by payee, category, amount, or find uncategorized transactions. This makes transaction management tedious and time-consuming.

---

## Goals & Non-Goals

### Goals
- [ ] Enable fast multi-field text search (payee, category, memo, amount) - most common use case
- [ ] Support finding uncategorized transactions
- [ ] Provide date range filtering with smart defaults (last 12 months)
- [ ] Allow field-specific searches for precision filtering
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
- [ ] Searches across payee, category, memo, and amount fields
- [ ] Results update as user types (debounced)
- [ ] Clear button (×) appears when text is present

### Story 2: User - Find Uncategorized Transactions
**As a** user
**I want** to filter for uncategorized transactions
**So that** I can quickly categorize newly imported transactions

**Acceptance Criteria**:
- [ ] Checkbox filter for "Uncategorized only"
- [ ] Finds transactions with blank or whitespace category
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
- [ ] Field-specific inputs for payee, category, memo
- [ ] Exact amount search
- [ ] Date range quick-select buttons (Last 30d, 3mo, 12mo, This year, All time)
- [ ] Custom date range inputs (From/To)
- [ ] "Clear All" button resets all filters

---

## Technical Approach

Implement collapsible filter bar pattern with search-first UX (similar to Monarch Money, YNAB).

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - Filter bar component with collapsible panel
- [x] Controllers (API endpoints) - Add query parameters to GetTransactions
- [x] Application (Features) - Filter logic in TransactionsFeature
- [ ] Database (Indexes) - Add indexes on Payee, Category for performance

**Key Components**:
- **New**: `src/FrontEnd.Nuxt/app/components/TransactionFilterBar.vue` - Filter UI component
- **Modified**: `src/FrontEnd.Nuxt/app/pages/transactions.vue` - Integrate filter bar
- **Modified**: `src/Controllers/TransactionsController.cs` - Add filter query parameters
- **Modified**: `src/Application/Features/TransactionsFeature.cs` - Filter query logic

**API Design**:
```
GET /api/tenant/{key}/transactions?search=starbucks&fromDate=2024-01-01&toDate=2024-12-31&uncategorizedOnly=true
```

**Filter Parameters**:
- `search` - Multi-field substring search (OR logic)
- `payee`, `category`, `memo` - Field-specific substring search (AND logic)
- `amount` - Exact amount match
- `fromDate`, `toDate` - Date range
- `uncategorizedOnly` - Boolean for blank/whitespace categories

---

## Open Questions

- [x] **Q**: Should we support cryptic text syntax (e.g., "c=groceries,y=2024")?
  **A**: NO - Poor UX, not discoverable. Use explicit filter panel instead.

- [x] **Q**: Always-visible filters vs. collapsible panel?
  **A**: Collapsible panel. Aligns with minimal/modern preference and industry best practices.

- [x] **Q**: How to handle amount search in multi-field search?
  **A**: Parse search text as decimal first. If valid, include amount field with exact match. Otherwise skip amount field.

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
- Transaction splits implementation (category field)
- Existing transaction list infrastructure

**Constraints**:
- Must maintain fast performance with 10k+ transactions
- Mobile-friendly filter UI required
- Backend must use database-level filtering (not client-side)

---

## Notes & Context

**Design Documents**:
- UI design: [`docs/wip/transactions/TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md`](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) (comprehensive 651-line spec)
- Initial requirements: [`docs/wip/transactions/TRANSACTION-FILTERING.md`](TRANSACTION-FILTERING.md)

**Key Design Decisions**:
1. **Search-first UX** - Always-visible search bar, collapsible advanced filters (progressive disclosure)
2. **Multi-field OR search** - Primary search bar searches all fields with OR logic (broad, fast)
3. **Field-specific AND search** - Advanced panel provides field-specific inputs with AND logic (precise, narrow)
4. **Backend filtering** - All filtering at database level for performance (not client-side)
5. **Smart defaults** - 12-month date range by default, user can clear or adjust

**Industry Patterns**:
- Monarch Money, YNAB use collapsible filter pattern
- Gmail, Notion use similar progressive disclosure
- Traditional finance apps (QuickBooks, Mint) use always-visible filters (rejected as cluttered)

---

## Handoff Checklist

Implementation planning complete:
- [x] UI pattern selected (collapsible filter bar)
- [x] Component architecture designed
- [x] API contract defined
- [x] Filter logic specified
- [x] Performance considerations documented
- [x] Mobile responsive strategy defined
- [x] Accessibility requirements specified

**Reference**: See [`TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md`](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) for complete implementation details including component structure, API changes, and phase-by-phase rollout plan.
