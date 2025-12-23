---
status: Draft
owner: James
target_release: Beta 3
ado: [Link to ADO Item]
---

# Product Requirements Document: Budgets

## Problem Statement

Users need a way to plan and monitor their spending against category-specific targets to maintain financial discipline and achieve savings goals. Without budgets, users can only react to past spending through reports rather than proactively managing their financial behavior.

---

## Goals & Non-Goals

### Goals

- [ ] Set time-based spending targets per category and monitor progress in real-time
- [ ] Enhance Reports with budget variance analysis
- [ ] Support flexible budget structures with category hierarchy rollups
- [ ] Focus on essential budgeting without overwhelming casual users

### Non-Goals

- Visible warnings or intrusive messaging. This is more nagging than helpful.
- AI-suggested budgets based on historical spending
- Budget forecasting or "if you keep spending" projections
- Multiple budget scenarios or A/B comparison
- Budget sharing across different tenants
- Multi-user approval workflows for budget changes
- Envelope or zero-based budgeting methodologies (future enhancement)

---

## User Stories

### Story 1: User - Creates/Edits Budget

**As a** User who is watching my spending
**I want** create or update a budget
**So that** I can stay on track with my financial goals

**Acceptance Criteria**:
- [ ] User can create, list, edit, and delete individual budget line items
- [ ] Budget line items include an amount and category, and descriptive memo
- [ ] Budget line items include a date, at which point the funds become "available". This is typically Jan 1 of a particular year by convention, but can be anthing
- [ ] Budget line items include a frequency, among: weekly, monthly, quarterly, or yearly. e.g. a "weekly" budget of $50 will make $50 "available" in that category starting on the start date, and then every week thereafter.
- [ ] Budget line items apply only to the calendar year of their date. (e.g. a 'weekly' budget with start date of 12/1 would provide about 4 weeks of new "available" budget before expiring at year end.)

### Story 2: User - Views Budget Status
**As a** User who is watching my spending
**I want** see how I'm doing against my budget
**So that** I can stay on track with my financial goals

**Acceptance Criteria**:
- [ ] User can select pre-defined budget reports which compare/contrast actual spending against budgeted spending
- [ ] Budget reports list the actual spending in that category tree in one column, then the budgeted spending for it in a second column, and the % of budget spent in a 3rd column (e.g., budget $50, actual $30, spent 60%)
- [ ] Budget reports show the % complete of the year so far in the header, so user can compare the spent% versus the current%. e.g., if we are 60% through the year, then my 60% spent lines are doing well
- [ ] Budget column shows cumulative budget available from year start to report date (sum of all periods that have elapsed based on frequency)
- [ ] Actual column shows cumulative spending from year start to report date (same time period as budget column)

**Pre-defined budget reports**
- Will pull this information from YoFi

### Story 3: User - Allocates budget at any category hierarchy level
**As a** User who is watching my spending
**I want** to assign budgets at varying granularity
**So that** I can track spending according to my own particular mental model

**Acceptance Criteria**:
- [ ] User can specify budget at a high level, like "Entertainment". In this case, only the "Entertainment" top-level category will have values in the budget and %spent columns
- [ ] User can specify budget at low levels, like "Transportation:Repairs:Jeep Cherokee". In this case all three of those levels will have values in budget and %spent columns
- [ ] User can specify budget at mixed levels, e.g., $3k in "Transportation:Repairs:Jeep Cherokee" and another $10k in "Transportation". Budget column shows $13k at "Transportation" level (additive rollup), and actual spending rolls up all Transportation:* spending for spent% calculation
- [ ] If only child categories are budgeted (e.g., "Transportation:Repairs:Jeep" = $3k), parent categories automatically show rolled-up values (e.g., "Transportation" shows $3k budget with all Transportation spending in actual column)
- [ ] Spent% at parent level = (All category spending including children) / (Sum of all budget line items for that category tree)

**TODO**
- I actually need to research what YoFi does in criterion 3 above

### Story 4: User - Creates new budget based on historical data [Post V3]
**As a** User who is watching my spending
**I want** help creating my budget with historical inputs
**So that** new-year budget-writing is not so tedious

**Acceptance Criteria**: **Needs further design**
- [ ] [Specific, testable criterion 1]
- [ ] [Specific, testable criterion 2]

---

## Technical Approach

Budget feature introduces a new entity for tracking spending targets per category with time-based frequency. Reports are enhanced with budget comparison columns following existing report patterns.

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - Budget CRUD pages, budget report views
- [x] Controllers (API endpoints) - BudgetController for CRUD, enhance ReportsController for budget reports
- [x] Application (Features/Business logic) - BudgetFeature for CRUD, ReportsFeature for budget calculations
- [x] Entities (Domain models) - New BudgetLineItem entity
- [x] Database (Schema changes) - New BudgetLineItems table with tenant isolation

**High-Level Entity Concepts**:

**BudgetLineItem Entity** (new):
- TenantKey (Guid, required) - Workspace isolation, follows [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) pattern
- Category (string, required, non-blank) - Category using `:` delimiter hierarchy (e.g., "Home:Utilities")
- Amount (decimal, required, positive) - Budget amount per frequency period
- Frequency (enum, required) - Weekly/Monthly/Quarterly/Yearly
- StartDate (DateTime, required) - Date when budget becomes available
- Memo (string, optional) - Descriptive note for the budget line item
- Id (Guid, required) - Primary key
- Created/Modified timestamps - Standard audit fields

**Key Business Rules**:

1. **Budget Accumulation Within Calendar Year** - Budget "available" amount accumulates from StartDate through Dec 31 based on frequency. Weekly $50 from 12/1 = $200 by 12/29 (4 weeks). Budget never decreases; unspent amounts carry forward within the year. Users can "save up" budget across periods for larger purchases.

   **Example**: Weekly $50 budget starting 12/1 with spending on 12/17 ($30) and 12/27 ($120):

   | Date | Budget | Actual | %Spent | Note |
   |------|--------|--------|--------|------|
   | 12/1 | $50 | $0 | 0% | |
   | 12/8 | $100 | $0 | 0% | |
   | 12/15 | $150 | $0 | 0% | |
   | 12/17 | $150 | $30 | 20% | Spent $30 today |
   | 12/22 | $200 | $30 | 15% | |
   | 12/27 | $200 | $150 | 75% | Spent $120 today |
   | 12/29 | $250 | $150 | 60% | |

2. **Annual Expiration with Manual Renewal** - Budget line items only apply within their StartDate's calendar year. All accumulated budget expires Dec 31. Users must create new line items each January 1st (motivates Story 4: historical data copy feature).

3. **Additive Hierarchy Rollups** - Parent category budgets sum their own allocations plus all descendant allocations. If "Transportation" has $10k budget AND "Transportation:Repairs:Jeep" has $3k budget, Transportation shows $13k total. Rollups display even when parent has no direct budget (e.g., only "Transportation:Repairs:Jeep" budgeted → "Transportation" shows $3k rolled up).

4. **Spent Percentage Calculation** - Spent% = (Actual cumulative spending in category from Jan 1 to report date) / (Total budget available from all matching line items to date). May exceed 100% when overspending. May show high percentages for partially-budgeted category trees where spending exceeds budgeted subcategories.

5. **Category Flexibility** - Users can create budgets for any category type (Expenses, Income, Taxes, Savings, etc.). Only constraint is non-blank category name. Multiple line items can exist for same category (system sums them when calculating total available budget).

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

**All questions resolved. Key decisions documented:**

1. ✅ **Budget accumulation model** - Budgets accumulate within calendar year; unspent amounts carry forward until Dec 31. See Business Rule #1.

2. ✅ **Annual renewal** - Budgets expire Dec 31; users manually create new line items each year (Story 4 addresses this friction). See Business Rule #2.

3. ✅ **Additive hierarchy** - Parent budgets = own allocation + child allocations ($10k Transportation + $3k Transportation:Repairs = $13k). See Business Rule #3.

4. ✅ **Pre-defined budget reports** - Mirror existing Reports feature (Income, Taxes, Expenses, Savings, Detail variants) with added Budget/Actual/Spent% columns. See Story 2 criteria and Dependencies section.

5. ✅ **Category flexibility** - Any category type allowed (Income, Expenses, Taxes, etc.). Only constraint: non-blank category. See Business Rule #5.

---

## Success Metrics

**Feature Adoption**:
- **Budget Creation Rate**: 40%+ of active users create at least one budget within 90 days of feature availability (indicates feature discovery and perceived value)
- **Budget Coverage**: Users with budgets average 8+ budgeted categories (indicates comprehensive budget planning, not just experimenting)

**User Engagement**:
- **Report View Frequency**: Users with budgets view budget reports 2x more frequently than users without budgets view regular reports (indicates budgets drive ongoing engagement)
- **Budget Maintenance**: 60%+ of users who create budgets update them at least once per quarter (indicates continued use, not one-time setup)

**Effectiveness**:
- **Budget Discipline**: 60%+ of budgeted categories stay within 110% of budget target (indicates realistic goal-setting and spending awareness)
- **Year-over-Year Retention**: 70%+ of users who budget in Year 1 create new budgets in Year 2 (indicates sustained value from budgeting practice)

---

## Dependencies & Constraints

**Dependencies**:
- **Reports Feature** ([`PRD-REPORTS.md`](../reports/PRD-REPORTS.md)) - Budget reports extend existing report structure. Story 5 in Reports PRD ("Views budget reports") depends on this Budgets PRD implementation.
- **Transaction Splits** ([`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md)) - Budget calculations aggregate split amounts by category, not transaction amounts. Splits must be implemented first.
- **Transaction Filtering** ([`PRD-TRANSACTION-FILTERING.md`](../transactions/PRD-TRANSACTION-FILTERING.md)) - Users drilling down from budget reports need category filtering capability on transactions page.

**Constraints**:
- **No mid-year budget changes** - Budget line items are immutable within their frequency period. Users cannot retroactively change a budget and recalculate historical spent%. They can only add/edit/delete line items which affects future calculations.
- **Calendar year boundary** - System hardcoded to calendar year (Jan 1 - Dec 31). No support for fiscal years or custom budget periods in V3.
- **No rollover budget between years** - Unspent budget does not carry into next calendar year. Annual renewal is manual (addressed by Story 4 in future).

---

## Notes & Context

**Background**: Budgeting is a core personal finance practice that transforms YoFi from a passive tracking tool into an active financial management system. While transaction entry and reports show "what happened," budgets enable users to proactively plan "what should happen" and monitor progress toward spending goals.

**Design Philosophy**:
- **Accumulation Model**: Budget accumulates within the year to accommodate irregular spending patterns (e.g., save up weekly budget for quarterly expense). This is more forgiving than strict period-based budgets and better matches real-world household finance behavior.
- **Flexible Granularity**: Support both high-level budgets ("Entertainment": $500/month) and detailed breakdowns ("Transportation:Fuel:Truck": $200/month, "Transportation:Fuel:Sedan": $150/month). Users choose their own mental model.
- **Report Integration**: Budget variance appears as additional columns in existing Reports feature, not a separate UI. Leverages user familiarity with report structure and reduces learning curve.

**Evolution from YoFi V1**: YoFi V1 included budgeting with similar accumulation and hierarchy patterns. V3 preserves proven UX patterns while modernizing data model (tenant isolation, improved entity design) and enhancing report integration (drill-down to transactions, better visual indicators).

**Future Extensibility**: Story 4 (Post V3) addresses annual renewal friction through historical data copy/template features. Potential future enhancements include budget forecasting ("at current rate, will exceed budget by X"), budget templates/presets, and envelope budgeting support.

**Related Documents**:
- [`PRD-REPORTS.md`](../reports/PRD-REPORTS.md) - Story 5 defines budget variance reporting requirements
- [`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md) - Defines split data model that budgets aggregate
- [`PRD-TRANSACTION-FILTERING.md`](../transactions/PRD-TRANSACTION-FILTERING.md) - Filtering for budget drill-down navigation
- [`PRODUCT-ROADMAP.md`](../../PRODUCT-ROADMAP.md) - Strategic context: "Reports & Insights" theme, Beta 3 target

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
