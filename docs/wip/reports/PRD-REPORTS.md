# Product Requirements Document: Reports

**Status**: Draft
**Created**: 2025-12-21
**Owner**: James Coliz
**Target Release**: [Version or Sprint]
**ADO**: [Link to ADO Item]

---

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
- Pre-aggregated reporting tables or materialized views (query splits in real-time)
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
- [ ] A grand total is shown at the bottom, including all included categories
- [ ] Subtotals are shown at each category level, e.g. there would be a subtotal for "Home:Utilities", and also one for "Home".
- [ ] Report definition includes a choice of whether uncategorized transactions are included. If the report includes them, they are included as a top-level report item "Uncategorized".

### Story 2: User - Configures report display
**As a** user
**I want** customize how the report is shown to me
**So that** I can drill into a certain area to understand more **or** zoom out to view the data at a higher conceptual level

**Acceptance Criteria**:
- [ ] User can choose which year to display. Unless otherwise described later, reports are always in the context of a single calendar year
- [ ] User can choose whether to show independent month values with a row total, or show whole year only. The default is an aspect of report definition.
- [ ] User can choose how many levels deep of categories to show. The default level is an aspect of the report definition.

### Story 3: User - Views a report in chart form
**As a** user
**I want** to visualize the report
**So that** I can process the information better as a visual learner

**Acceptance Criteria**:
- [ ] For any report I can view the data in an easy-to-understand chart. It's OK if the chart shows a higher summary level than the table at any moment. (Exactly which form of chart TBD)
- [ ] User can choose between chart, table, or both (defaults to both)

### Story 4: User - Views summary report
**As a** user
**I want** to view a top-level summary of my entire income/expense picture
**So that** I can get a high-level sense of where we are

This is an "income statement" made easier to use for lay users.

**Acceptance Criteria**:
- [ ] Summary report includes a collection of high-level report sections (to be designed)
- [ ] From each section, there is an affordance for me to drill into that and directly view a dedicated report at a lower level of detail.

### Story 5: User - Views budget reports [PENDING]
**As a** user
**I want** to see how I'm doing against my budget plan
**So that** I can adjust my spending to stay on target

**NOTE** THis is a placeholder story. Will return to after completing Budget PRD in the future

**Acceptance Criteria**:
- [ ] [Specific, testable criterion 1]
- [ ] [Specific, testable criterion 2]

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
- [ ] User can select any number on a report, and choose to drill in. This will move user to Transactions page, with a filter applied based on what was included in that number.

### Story 8: User - Defines a custom report [FUTURE]
**As a** user
**I want** define my own report
**So that** I can examine some aspect of my financial picture in a specific way I'm interested in

**Acceptance Criteria**:
- [ ] User can customize any of the existing built-in report parameters
- [ ] User has complete CRUD control of report defintions

**NOTE** This is included here for future vision. Remainder of PRD does not include further consideration of this

---

## Technical Approach

**Layers Affected**:
- [x] Frontend (Vue/Nuxt): reports display page(s)
- [x] Controllers (API endpoints): Give API access to report generation logic for front end
- [x] Application (Features/Business logic): Generate reports
- [ ] Entities (Domain models)
- [?] Database (Schema changes): Possibly changes to indexing

**Key Business Rules**:

1. **Split-Based Aggregation** - Reports aggregate split amounts by category, not transaction amounts. Each split contributes independently to its category total.

2. **Hierarchical Category Rollups** - Categories use `:` delimiter for hierarchy (e.g., "Home:Utilities:Electric"). Reports show both detail rows for each level and rollup subtotals for parent categories.

3. **Income Category Convention** - Top-level "Income" category (case-insensitive) and all subcategories (e.g., "Income:Salary") are treated as income. All other categories are expenses. No special handling for unexpected signs (negative income is shown as negative).

4. **Uncategorized Split Handling** - Whether uncategorized splits (empty string category) are included is defined per report. When included, they appear as top-level "Uncategorized" row. Each split in a multi-split transaction is counted independently.

5. **Drill-Down Navigation** - Clicking any report cell/row opens transactions page in new browser tab with appropriate filters applied. Clicking subtotal rows (e.g., "Home") filters to all subcategories ("Home:*"). Filters are visible and editable on transactions page.

6. **Year-over-Year Report** - Separate report type showing columns for each year where any data exists. Shows only years with data (no zero columns for missing years). No month-by-month view available for this report type.

7. **Report Configuration** - Report definitions are hard-coded in application (not database-stored). Category filtering is defined at report-definition level. Users select from pre-defined reports only (custom report creation is future).

8. **Performance Targets** - Real-time query of split data (no pre-aggregation). Single year reports must complete in <500ms. Multi-year aggregation (e.g., 15 years) must complete in <2s.

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

### 1. **Built-in Report Definitions**

What specific built-in reports should be included initially? Need to pull from YoFi V1.

### 2. **Chart Visualization Library & Types**

- What chart library should be used? (Chart.js, Recharts, D3, other?)
- For month-by-month reports, which chart type? (line chart, bar chart, stacked bar?)
- For category breakdown reports, which chart type? (pie chart, horizontal bar, tree map?)
- Should charts be interactive (clickable to drill down)?
- Are there accessibility requirements for charts (screen reader support, alternative data tables)?

### 3. **Summary Report Sections**

What high-level sections should the summary report include? Need to pull from YoFi V1.

Answered: Summary report allows year selection (YES), does not include year-over-year comparison (NO).

### 4. **State Management & User Preferences**

- Should report configuration (year, depth level, chart/table view) be persisted per user (database), saved in browser localStorage, or reset each visit?
- Should there be "Save Report Configuration" functionality for later recall?

Note: Custom report configurations (Story 8 - future) will be tenant-scoped.

### 5. **Transaction Volume Research**

How many transactions are anticipated per tenant? (hundreds, thousands, tens of thousands?) - Research needed for indexing strategy.

---

## Success Metrics

[How will we know this feature is successful?]
- [Metric 1]
- [Metric 2]

---

## Dependencies & Constraints

**Dependencies**:
- Requires PRD-TRANSACTION-SPLITS to be complete, so Category field is available. We are actually summarizing the **SPLITS** in the system
- Requires PRD-TRANSACTION-FILTERING to be complete, so we can show the drill-down. There may be changes required to that design to accomodate needs of report-driven filters

**Constraints**:
- We may need to revisit choice of database technology if current choice doesn't support reporting perf metrics.
- [Technical, time, or resource constraints]

---

## Notes & Context

[Any additional context, links to related documents, or background information]

**Related Documents**:
- [Link to companion Design Document if it exists]
- [Link to related PRDs]

---

## Handoff Checklist (for AI implementation)

> [!NOTE] See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md) for guidance on PRD scope (WHAT/WHY vs HOW), what belongs in a PRD vs Design Document, and examples.

When handing this off for detailed design/implementation:
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
- [ ] Companion design document created (for complex features) OR noted as "detailed design during implementation"
