---
status: Approved
owner: James Coliz
target_release: V3.0
ado: "[Link to ADO Item]"
---

# Product Requirements Document: Reports

## Problem Statement

The primary user purpose of a financial tracking app is to get insight and big-picture understanding of their financial picture. Reports are the
mechanism to deliver this insight and understanding

---

## Goals & Non-Goals

### Goals
- [ ] Enable users to understand their spending patterns through category-based income/expense reports
- [ ] Provide flexible report views (monthly detail vs. annual summary, configurable category depth)
- [ ] Support data verification through drill-down from report values to underlying transactions
- [ ] Deliver fast report generation (<500ms for single year, <2s for multi-year aggregation)
- [ ] Present data visually through charts for better comprehension
- [ ] Show hierarchical category rollups (e.g., "Home:Utilities" rolls up to "Home")

### Non-Goals
- Custom report creation by users (deferred to future - Story 8)
- Budget variance reporting (deferred pending Budget PRD - Story 5)
- Multi-currency support (out of scope for V3)
- Report scheduling or automated email delivery
- Export reports to PDF or Excel (separate feature)
- Retrieve report data for API (separate feature)
- Machine learning or predictive analytics

---

## User Stories

### Story 1: User - Views a built-in income/expense report
**As a** user
**I want** to review a summary of my income and/or expenses over a period of time
**So that** I can understand where my money is going, and what changes I might want to make in my financial life

**Acceptance Criteria**:
- [ ] User can select from a set of pre-defined income and/or expense reports
- [ ] User's choice of report filters which categories are included or excluded
- [ ] A grand total is shown at the end of each row if there are month columns
- [ ] A percentage is shown at the far end of each row, after the grand total, giving the percentage of that row out of the whole report grand total
- [ ] A grand total is shown at the bottom of each column, including all included categories
- [ ] Subtotals are shown at each category level, e.g. there would be a subtotal for "Home:Utilities", and also one for "Home".
- [ ] Report definition includes a choice of whether uncategorized transactions are included. If the report includes them, they are included as a top-level report item "Uncategorized".

### Category

Reports expect certain category conventions. The following top-level case-insensitive categories (e.g "Income") category and all subcategories (e.g., "Income:Salary") are treated as described below.

- Income: All sources of income
- Taxes: Tax withheld or paid, typically income tax, but any tax payments are expected here.
- Savings: Explicit transfers to savings accounts, which are not expenses
- Transfer: Moving money around, which is not expected to show up in any category

All other categories are expenses. No special handling for unexpected signs (negative income is shown as negative).

### Reports

Following are the standard income/expense reports

| Name | Categories | Default Level | Shows months by default
| --- | --- | --- | --- |
| Income | Income:* | 1 | No |
| Taxes | Taxes:* | 1 | No |
| Expenses | (All others):* | 1 | No |
| Savings | Savings:* | 1 | No |
| Transfers | Transfer:* | 1 | No |
| Income Detail | Income:* | 2 | Yes |
| Taxes Detail | Taxes:* | 2 | Yes |
| Expenses Detail | (All others):* | 2 | Yes |
| Savings Detail | Savings:* | 2 | No |
| All | * | 1 | No |
| All Detail | * | 2 | Yes |

**Notes:**
- **Category Filtering**: "Expenses" and "Expenses Detail" include all categories EXCEPT Income, Taxes, Savings, and Transfer. They also include uncategorized splits.
- **Category Depth Levels**: "Default Level: N" means show N levels below the report filter root. For example:
  - Income report at Level 1 shows categories like `Income:Salary`, `Income:Bonus` (one level below Income)
  - Income report at Level 2 shows categories like `Income:Salary:Primary`, `Income:Bonus:Annual` (two levels below Income)
  - Expenses report at Level 1 shows top-level expense categories like `Home`, `Transportation` (treated as if there's an implicit "Expenses" root)
  - Expenses report at Level 2 shows `Home:Utilities`, `Transportation:Fuel` (two levels of expense hierarchy)

### Story 2: User - Configures report display
**As a** user
**I want** customize how the report is shown to me
**So that** I can drill into a certain area to understand more **or** zoom out to view the data at a higher conceptual level

**Acceptance Criteria**:
- [ ] User can choose which year to display. Unless otherwise described later, reports are always in the context of a single calendar year
- [ ] User can choose whether to show independent month values with a row total, or show whole year only. The default is an aspect of report definition.
- [ ] User can choose how many levels deep of categories to show. The default level is an aspect of the report definition.
- [ ] Settings are persisted per-report in browser localStorage (each report remembers its own year, depth, and chart/table view)
- [ ] Settings auto-save whenever changed
- [ ] User can click "Reset" to clear localStorage for that specific report and restore definition defaults

### Story 3: User - Views a report in chart form
**As a** user
**I want** to visualize the report
**So that** I can process the information better as a visual learner

**Acceptance Criteria**:
- [ ] For any report I can view the data in an easy-to-understand chart using Chart.js library
- [ ] Month-by-month reports display as line charts showing top-level categories only
- [ ] Category breakdown reports display as donut charts
- [ ] Charts are not interactive (no drill-down from charts)
- [ ] User can choose between chart, table, or both (defaults to both)
- [ ] No accessibility requirements for charts

### Story 4: User - Views summary report
**As a** user
**I want** to view a top-level summary of my entire income/expense picture
**So that** I can get a high-level sense of where we are

This is an "income statement" made easier to use for lay users. The sections are as follows. Unless otherwise noted these are the reports described in Story 1

- Income
- Taxes
- Calculated Section: "Net Income". Rows: "Income", "Taxes" (both taken from grand total of above), "Total". Columns: Category, "$ Total", "% of Income"
- Expenses
- Savings
- Calculated Section: "Net Savings". Rows: "Net Income" (taken from previous calculated section), "Expenses", "Total". Columns: "Category", "$ Total", "% of Net Income"
  - **Note**: Total = Net Income - Expenses, representing actual savings rate (explicit savings in "Savings" section + implicit savings remaining in bank account)

**Acceptance Criteria**:
- [ ] Summary report includes a collection of high-level report sections
- [ ] From each section which is backed by an underlying report, there is an affordance for me to drill into that and directly view a dedicated report at a lower level of detail.

### Story 5: User - Views budget reports [SUPERSEDED]
**As a** user
**I want** to see how I'm doing against my budget plan
**So that** I can adjust my spending to stay on target

**NOTE** This story has been superseded by [`PRD-BUDGETS.md`](../budgets/PRD-BUDGETS.md), which provides comprehensive budget management including budget reports.

**Acceptance Criteria**:
- 🚫 Superseded - See [`PRD-BUDGETS.md`](../budgets/PRD-BUDGETS.md) for budget reporting requirements

### Story 6: User - Views complete history of income/expenses over time
**As a** user
**I want** to see how my income/expenses compare over all known time
**So that** I can detect larger-scale patterns, and plan for future spending

**Acceptance Criteria**:
- [ ] Report shows columns of total values for each year
- [ ] User can choose year range (start, end). Defaults to "beginning of recorded history" as start, and current year as end
- [ ] User cannot view a chart for this report

### Story 7: User - Investigates underlying transactions
**As a** User
**I want** discover which transactions exactly comprise one of the numbers shown
**So that** I can understand what underlying actions caused the result I'm seeing

**Acceptance Criteria**:
- [ ] User can select any number on a report and drill in, opening the Transactions page in a new browser tab
- [ ] Clicking a monthly cell (e.g., "Home:Utilities" for March) applies category AND month filters
- [ ] Clicking a row total applies category filter for the entire year
- [ ] Clicking a column total filters to that month for all categories included in the report (e.g., Income report shows all Income:* categories)
- [ ] Clicking subtotal rows (e.g., "Home") filters to all subcategories using wildcard pattern (e.g., "Home:*")
- [ ] Filters are visible and editable on the transactions page

#### Interaction with Transaction Filtering

The requirements we will have on filtering:
- [ ] Reports can specify a date range
- [ ] Reports can specify categories with wildcard, e.g. "Income:*" matches "Income" and "Income:Salary"
- [ ] Reports can use special "Expenses" moniker to mean "all transactions not in Income, Taxes, Savings, or Transfer" top level categories
- [ ] Reports can (and almost always will) specify *both* a date range and a category with wildcard.

### Story 8: User - Defines a custom report [FUTURE]
**As a** user
**I want** define my own report
**So that** I can examine some aspect of my financial picture in a specific way I'm interested in

**Acceptance Criteria**:
- [ ] User can customize any of the existing built-in report parameters
- [ ] User has complete CRUD control of report defintions
- [ ] Custom reports are available to anyone in the workspace (tenant)

**NOTE** This is included here for future vision. Remainder of PRD does not include further consideration of this

---

## Technical Approach

**Layers Affected**:
- [x] Frontend (Vue/Nuxt): reports display page(s)
- [x] Controllers (API endpoints): Give API access to report generation logic for front end
- [x] Application (Features/Business logic): Generate reports
- [ ] Entities (Domain models)
- [x] Database (Schema changes): May need composite indexes on (TenantKey, Category, Year) for optimal split-based query performance (see Constraints section)

**Key Business Rules**:

1. **Split-Based Aggregation** - Reports aggregate split amounts by category, not transaction amounts. Each split contributes independently to its category total.

2. **Hierarchical Category Rollups** - Categories use `:` delimiter for hierarchy (e.g., "Home:Utilities:Electric"). Reports show both detail rows for each level and rollup subtotals for parent categories.

3. **Uncategorized Split Handling** - Whether uncategorized splits (empty string category) are included is defined per report. When included, they appear as top-level "Uncategorized" row. Each split in a multi-split transaction is counted independently.

4. **Drill-Down Navigation** - Clicking any report cell/row opens transactions page in new browser tab with appropriate filters applied. Clicking subtotal rows (e.g., "Home") filters to all subcategories ("Home:*"). Filters are visible and editable on transactions page.

5. **Year-over-Year Report** - Separate report type showing columns for each year where any data exists. Shows only years with data (no zero columns for missing years). No month-by-month view available for this report type.

6. **Report Configuration** - Report definitions are hard-coded in application (not database-stored). Category filtering is defined at report-definition level. Users select from pre-defined reports only (custom report creation is future).

7. **Performance Targets** - Single year reports must complete in <500ms. Multi-year aggregation (e.g., 15 years) must complete in <2s. Expected volume: 1,500 transactions per year per tenant (30,000 transactions over 20 years).

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../../../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../../../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../../../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../../../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

**All questions have been resolved and integrated into the appropriate sections above.**

---

## Success Metrics

**Feature Adoption**:
- **Report Usage Rate**: 70%+ of active users view at least one report per month (indicates reports deliver value worth returning to)
- **Drill-Down Usage**: 40%+ of report viewers use drill-down to transactions at least once (validates the new capability addresses a real need)

**User Engagement**:
- **Report View Depth**: Average of 2+ different report types viewed per session (indicates users explore multiple perspectives on their data)
- **Configuration Persistence**: 50%+ of users modify default report settings (depth level, month display) and those settings persist across sessions (indicates customization provides value)

**Performance**:
- **Query Performance**: 95%+ of single-year reports complete in <500ms, 95%+ of multi-year reports complete in <2s (meets user experience targets)
- **Error Rate**: <1% of report requests result in errors or timeouts (indicates stable, reliable functionality)

---

## Dependencies & Constraints

**Dependencies**:
- Requires PRD-TRANSACTION-SPLITS to be complete, so Category field is available. We are actually summarizing the **SPLITS** in the system
- Requires PRD-TRANSACTION-FILTERING to be complete, so we can show the drill-down. There may be changes required to that design to accomodate needs of report-driven filters

**Constraints**:
- Database technology choice may need revisiting if performance targets aren't met with SQLite.
- May need composite indexes on (TenantKey, Category, Year) for optimal split-based query performance

---

## Notes & Context

**Background**: This feature represents a core value proposition of the YoFi financial tracking application. While transaction entry and categorization are necessary data collection activities, reports transform that raw data into actionable insights that drive user decision-making about their financial lives.

**Design Philosophy**:
- **Progressive Disclosure**: Reports start at high-level summaries (Summary Report, Level 1 reports) and allow users to drill down progressively to more detail (Level 2+ reports, individual transactions). This prevents information overload while maintaining access to full detail when needed.
- **Split-Centric Model**: Reports aggregate transaction splits rather than transactions themselves. This correctly handles multi-category transactions (e.g., a single grocery receipt split between "Food" and "Home" categories) and aligns with how users conceptualize their spending.
- **Performance First**: Performance targets (<500ms single-year, <2s multi-year) define acceptable user experience. Implementation approach (real-time queries, pre-aggregation, indexes, caching) is flexible and should be chosen based on what achieves these targets most effectively.

**Evolution from V1**: This PRD captures proven patterns from YoFi V1 that worked well for users:
- The Income/Taxes/Expenses/Savings/All report structure maps to how users naturally think about their finances
- Chart.js provided reliable visualization without excessive complexity
- Category hierarchy with `:` delimiter proved intuitive and flexible

**New in V3**: Drill-down to transactions (Story 7) is a new capability designed to address a gap from V1. When users saw unexpected values in reports, they had to manually search transactions to understand what drove those numbers. Direct drill-down eliminates this friction.

**Future Extensibility**: While custom reports (Story 8) are deferred, the hard-coded report definitions are designed to be easily refactored into database-backed configurations when that capability is added. The report definition model (category filters, depth levels, month display) captures the parameters users would want to customize.

**Related Documents**:
- [`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md) - Defines split data model that reports aggregate
- [`PRD-TRANSACTION-FILTERING.md`](../transactions/PRD-TRANSACTION-FILTERING.md) - Defines filtering UX that drill-down navigation depends on
- [`PRD-GUIDANCE.md`](../PRD-GUIDANCE.md) - General guidance on PRD scope and structure

---

## Handoff Checklist (for AI implementation)

**Review Result**: ✅ **APPROVED** - Ready for implementation

**Checklist Status**:
- ✅ All user stories have clear acceptance criteria (Stories 1-4, 6-7 complete; 5, 8 appropriately marked as future)
- ✅ Open questions resolved and integrated into Technical Approach section
- ✅ Technical approach clearly indicates affected layers (Frontend, Controllers, Application, Entities, Database)
- ✅ Code patterns referenced with file links (all paths verified and corrected)
- ✅ All related document links verified and corrected
- ✅ Database layer properly marked with index requirements

**Required Fixes Before Implementation**:
None - all issues resolved.

**Strengths**: Excellent WHAT/WHY focus per PRD-GUIDANCE.md patterns, comprehensive business rules, clear success metrics, strong product context.

**Recommendation**: Ready for implementation.
