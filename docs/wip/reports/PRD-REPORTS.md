# Product Requirements Document: Reports

**Status**: Draft
**Created**: [YYYY-MM-DD]
**Owner**: [Your Name]
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

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [x] Frontend (Vue/Nuxt): reports display page(s)
- [x] Controllers (API endpoints): Give API access to report generation logic for front end
- [x] Application (Features/Business logic): Generate reports
- [ ] Entities (Domain models)
- [?] Database (Schema changes): Possibly changes to indexing

**High-Level Entity Concepts**:

**[EntityName] Entity** (new or modified):
- PropertyName (description - what it represents, required/optional)
- PropertyName (description)
- PropertyName (description)

[Add more entities as needed]

**Key Business Rules**:
1. **Rule Name** - Description of business rule that affects user experience
2. **Rule Name** - Description of business rule
3. [Add more business rules that belong in PRD scope]

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

Based on my review of the Reports PRD, I have several clarifying questions to help refine the requirements and create a detailed implementation plan.

### 1. **Category Structure & Data Model**

The PRD mentions "subtotals at each category level" (e.g., "Home:Utilities" and "Home"), but the current Transaction model doesn't have a category field - it uses splits instead.

**A:** Category will be implemented when PRD-TRANSACTION-RECORD is implemented.

**Questions:**
- Should reports aggregate split amounts by category across all transactions?

**A:** YES. We are actually displaying a summary of **SPLITS**

- For hierarchical categories (e.g., "Home:Utilities:Electric"), should the report show:
  - All three levels separately (Electric, Utilities, Home)?
  - Only the deepest level with user-controlled depth display?
  - Both detail rows and rollup subtotals?

**A:** Both detail and rollup subtotals

- How should transactions with multiple splits be counted? (e.g., if a transaction has splits for "Food:Groceries" and "Home:Supplies", does it appear in both categories?)

**A:** In this case, the amount in groceries shows up in that summary cell, and supplies in that summary cell. For the "drill down" transaction view, then yes both transactions would show up. This is how transaction filtering should work with splits regardless of this.

### 2. **Report Definitions & Built-in Reports**

Story 1 mentions "pre-defined income and/or expense reports" but doesn't specify what these are.

**Questions:**
- What specific built-in reports should be included? Examples:
  - "All Expenses" (excludes income/positive amounts)?
  - "All Income" (only positive amounts)?
  - "Net Income/Expense" (both)?
  - Specific category groups (e.g., "Housing", "Transportation")?

**A:** Good question. I just need to go pull this in from YoFi V1

- How do users distinguish income from expenses? By:
  - Amount sign (positive = income, negative = expense)?
  - Category naming convention?
  - Explicit transaction type field (future enhancement)?

**A:** Good question. For now, we will define some hard-coded conventions for reports. In this case "Income" top level category is income, everything else is expense. Hard-coded categories will be case insensitive. For this question, an "Income" report would include all subcategories of top-level "Income"

- Should report definitions be stored in the database or hardcoded in the application?

**A:** For simplicity, I was thinking we would hard-code them. I'm open to reconsidering this

- Negative amounts in Income category: If someone enters a negative amount in an "Income:Salary" split, does it show as negative income or get treated specially?

No special treatment for unexpect sign in any category. This is normal and expected behavior.

### 3. **Category Filtering in Report Definitions**

Story 1 says "User's choice of report filters which categories are included or excluded."

**Questions:**
- Is category filtering defined at report-definition level (each built-in report has fixed categories) OR at runtime (user selects which categories to include/exclude)?

**A:** Category filtering is defined as property  of the report defition.

- If runtime filtering: Should this be a multi-select list, regex pattern, or category hierarchy selector?
- Can users create custom combinations of the built-in report definitions, or only use them as-is?

**A:** Until "User - Defines a custom report" is complete, NO they can only choose a pre-defined report

### 4. **Data Aggregation & Performance**

Reports need to aggregate potentially thousands of transactions across multiple dimensions (time, category, splits).

**Questions:**
- Should reports query raw transaction/split data in real-time, or use a pre-aggregated reporting table/view?

**A:** YoFi does this now using Azure SQL server. I an seeing many thousands of records across 15 years, and the year-over-year report gets built in less than a second. This requirement may affect our choice of database, which I accept.

- What is the acceptable performance threshold? (e.g., <500ms for a year's worth of data?)

**A:** <500ms for a single year is good. As noted above, YoFi today aggregates all data over 15 years in roughly a second. We should strive for no more than 2s.

- Should there be a maximum date range for performance reasons?

**A:** Absolutely not.

- How many transactions do you anticipate per tenant? (hundreds, thousands, tens of thousands?)

**A:** Good question, I will research this

### 5. **Chart Visualization Specifics**

Story 3 mentions charts but leaves the specifics as "TBD."

**Questions:**
- What chart library should be used? (Chart.js, Recharts, D3, other?)
- For month-by-month reports, which chart type? (line chart, bar chart, stacked bar?)
- For category breakdown reports, which chart type? (pie chart, horizontal bar, tree map?)
- Should charts be interactive (clickable to drill down)?
- Are there accessibility requirements for charts (screen reader support, alternative data tables)?

### 6. **Summary Report Structure**

Story 4 describes a "summary report" but leaves the sections "to be designed."

**Questions:**
- What high-level sections should the summary include? Suggestions:
  - Total Income vs. Total Expenses
  - Net Income (Income - Expenses)
  - Top 5 Expense Categories
  - Monthly spending trend (last 12 months)
  - Uncategorized transaction warning/count

**A:** Just need to go pull this over from YoFi, and add it in

- Should the summary always show the current year, or allow year selection?

**A:** YES

- Should it include year-over-year comparison data?

**A:** NO

### 7. **Drill-Down to Transactions (Story 7)**

This is a critical feature for user trust and data verification.

**Questions:**
- When drilling down, should the transactions page open:
  - In a new browser tab/window?
  - In a modal overlay?
  - By navigating to the transactions page with filters applied?

**A:** I like the idea of in a new browser tab

- Should the applied filter be visible and editable on the transactions page?

**A:** YES

- Should there be a "back to report" navigation affordance?

**A:** NO, they're in a new tab, they can just close it.

- What if the user clicks a subtotal row (e.g., "Home") - should it include all subcategories ("Home:*")?

**A:** YES Exactly. This applies to all subtotals and even the report grand tota.

### 8. **Year-over-Year Report (Story 6)**

**Questions:**
- Should this be a separate report type, or an option available for all reports?

**A:** Separate report type. That's waht YoFi does today and it works well.

- What happens if data doesn't exist for some years (e.g., user started in 2023)? Show zeros or omit columns?

**A:** Show columns for all years where there is any data in any row

- Should there be year-over-year % change calculations?

**A:** Interesting idea, will consider. Right now, I worry that this clutters too much

- Can this be combined with month-by-month view (12 months × N years grid)?

**A:** NO

### 9. **Uncategorized Transactions Handling**

**Questions:**
- Should uncategorized splits (empty string category) be:
  - Always shown as "Uncategorized" row?
  - Only shown if user opts in via report configuration?
  - Different behavior for different report types?

**A:** Whether to include uncategorized transaction is a property of the report definition. I was not imaginging allowing user to configure, until such time as they are creating custom reports

- If a transaction has 2 splits (one categorized, one uncategorized), does the transaction appear in both sections?

**A:** We are really only summarizing **SPLITS** so the categorized split will affect the total of that category, and the uncategorized split will affect the total of the uncategorized category

### 10. **State Management & User Preferences**

**Questions:**
- Should report configuration (year, depth level, chart/table view) be:
  - Persisted per user (saved to database)?
  - Saved in browser localStorage only?
  - Reset to defaults each visit?

**A:** Will have to think about this some more

- Should there be "Save Report Configuration" functionality for later recall?

**A:** Will have to think about this some more

- Are report configurations tenant-scoped or user-scoped?

**A:** When user is creating own reports, they are tenant scoped.

---

## Success Metrics

[How will we know this feature is successful?]
- [Metric 1]
- [Metric 2]

---

## Dependencies & Constraints

**Dependencies**:
- Requires PRD-TRANSACTION-SPLITS to be complete, so Category field is available. We are actually summarizing the **SPLITS** in the system
- Requires PRD-TRANSACTION-FILTERING to be complete, so we can show the drill-down

**Constraints**:
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
