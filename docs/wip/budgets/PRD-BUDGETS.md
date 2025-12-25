---
status: Approved
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

- [ ] Set annual spending targets per category and monitor progress in real-time
- [ ] Enhance Reports with budget variance analysis
- [ ] Support flexible budget structures with category hierarchy rollups
- [ ] Focus on essential budgeting without overwhelming casual users

### Non-Goals

- Visible warnings or intrusive messaging. This is more nagging than helpful.
- ML/AI-suggested budgets (Stories 4-5 use rules-based algorithms, not machine learning)
- Budget forecasting or "if you keep spending" projections
- Multiple budget scenarios or A/B comparison
- Budget sharing across different tenants
- Multi-user approval workflows for budget changes
- Envelope or zero-based budgeting methodologies (future enhancement)

---

## Feature Deep-Dive

The entire answer to "how am I doing on my budget?" is presented through reports. Therefore, it's essential to understand how reports work to help understand how budgeting fits in. Let's walk through a narrative showing how budgets interact with category hierarchies across multiple years.

### Understanding Report Hierarchy Levels

Reports can display categories at different hierarchy depths. A **ONE-LEVEL report** shows only the immediate children of a root category, rolling up any deeper subcategories. For example, if viewing an "Income" report at ONE-LEVEL depth, transactions in "Income:Side Gigs:Babysitting" would roll up and display under "Side Gigs".

The special **"-" (dash) row** represents splits assigned directly to the root category with no subcategory (e.g., transactions tagged as just "Income" rather than "Income:Salary"). This is distinct from rollups.

Specific reports default to certain hierarchy levels, but users can always adjust the depth to drill down into more detail.

### Year 1: Basic Categorization

At the end of the year, my "Income" report looks like this:

| **Income** | **$ Total** | **% Total** |
|------------|-------------|-------------|
| Salary | 50000 | 80.65% |
| Bonus | 10000 | 16.13% |
| - | 2000 | 3.23% |
| **Total** | **62000** | **100%** |

I picked up a little cash for babysitting and lawn mowing, but it wasn't big enough to put in its own category. I could have put it in "Income:Others", but I didn't. I just put it in "Income" (no subcategory).

### Year 2: Introduction to Budgeting

This year, I discovered budgeting. I had YoFi copy over my actuals from Year 1 to make up my Year 2 budget. Now I will look at a hypothetical "Income vs Budget" report.

| **Income** | **Actual** | **Budget** | **% Progress** |
|------------|------------|------------|----------------|
| Salary | 55,000 | 50,000 | 110.00% |
| Bonus  | 12,000 | 10,000 | 120.00% |
| -      | 3,500  | 2,000  | 175.00% |
| **Total** | **70,500** | **62,000** | **113.71%** |

I added budget line items for "Income:Salary", "Income:Bonus", and "Income" (no subcategory) at the amounts shown. The system interprets "Income" as a category to mean "Income with no subcategory".

I'm "over budget" in all my areas. I got a raise, got a bigger bonus, and did more side gigs. Good for me!

### Year 3: Category Refinement and Budget Orphaning

This year, I created the budget from last year's actuals, BUT I'm making enough that I'll break out each of the additional items into their own category. Here's how "Income vs Budget" looks at the end of the year.

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | 60,000     | 55,000     | 109%           |
| Bonus        | 10,000     | 12,000     | 83%            |
| Babysitting  | 2,000      | 0          | —              |
| Lawn mowing  | 2,000      | 0          | —              |
| -            | 0          | 3,500      | 0%             |
| **Total**    | **74,000** | **70,500** | **105%**       |

This demonstrates an important scenario: **budget line items remain tied to specific category paths and don't automatically follow spending when categorization changes**. The $3,500 budgeted for "Income" (no subcategory) now shows 0% spent because I moved all actuals into "Babysitting" and "Lawn mowing" subcategories.

The orphaned budget still contributes to the overall totals, so I can see I'm still a little over budget (good!) on income this year. This is working as designed—there's often a lag between when users change their budget structure and when actual spending patterns shift.

**Note on display**: Categories without budget line items show "0" in the Budget column (this may change to blank or "—" as a UI refinement). The "—" in % Progress indicates division by zero (can't calculate percentage when budget is zero).

**Category rename scenario**: If I had budgeted for "Income:Side gigs" but actually recorded transactions in "Income:Babysitting", the report would show both rows—"Babysitting" with actuals but no budget, and "Side Gigs" with budget but no actuals. Users can always edit budget line items when they realize categorization has changed.

### Year 4: Hierarchy Restructuring

In year 4, I decide to organize all my side gigs under a parent category. I create "Income:Side Gigs:Babysitting" and "Income:Side Gigs:Lawn Mowing". I made $2,000 babysitting and $3,000 lawn mowing. At the end of the year, my "Income vs Budget" report looks like this:

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | $65,000    | $60,000    | 108%           |
| Bonus        | $15,000    | $10,000    | 150%           |
| Side Gigs    | $5,000     | $4,000     | 125%           |
| **Total**    | **$85,000** | **$74,000** | **115%**       |

This is still a ONE-LEVEL report. The "Side Gigs" row represents the rollup of all transactions and budgets from "Income:Side Gigs:Babysitting" ($2,000 actual) and "Income:Side Gigs:Lawn Mowing" ($3,000 actual).

**Key insight**: Both Budget AND Actual columns roll up hierarchically in reports. When displaying at ONE-LEVEL depth, all subcategories are aggregated into their parent category.

### Summary: Report Behavior

- **ONE-LEVEL reports**: Show immediate children of root category, roll up deeper levels
- **Budget column**: Rolls up all budget line items in category tree (additive)
- **Actual column**: Rolls up all transactions in category tree
- **Special "-" row**: Transactions directly assigned to parent with no subcategory
- **Hierarchy control**: Users can adjust report depth to drill into subcategories
- **Annual budgets**: All budgets are year-long (Jan 1 to Dec 31)
- **Category changes**: Budget line items remain tied to original category paths

---

## User Stories

### Story 1: User - Creates/Edits Budget

**As a** User who is watching my spending
**I want** create or update a budget
**So that** I can stay on track with my financial goals

**Acceptance Criteria**:

**Budget Line Item CRUD Operations:**
- [ ] User can create, list, edit, and delete individual budget line items

**Budget Line Item Fields:**
- [ ] Budget line items include category (category hierarchy path)
- [ ] Budget line items include amount (annual budget amount for the category, using category-appropriate signage)
- [ ] Budget line items include year (calendar year the budget applies to)
- [ ] Budget line items include memo (optional descriptive note)

**Budget Line Item Filtering:**
- [ ] User can filter budget list by year (via UI controls or URL parameters)
- [ ] User can filter budget list by category using substring match (e.g., "Taxes" matches "IncomeTaxes", "TaxesAPlenty", etc.)
- [ ] User can filter budget list by category using wildcard match (e.g., "Taxes:*" matches "Taxes", "Taxes:Income", "Taxes:Property", etc.)
- [ ] Category filtering follows same mechanism as transaction filtering
- [ ] Filters can be set via UI controls (text input, dropdowns) or URL query parameters (for report drill-down navigation)
- [ ] URL parameter support enables drill-down from budget reports to see which line items contribute to rolled-up totals

**Budget Scope:**
- [ ] All budgets are annual (full calendar year from Jan 1 to Dec 31)
- [ ] Budget line items apply only to their specified calendar year
- [ ] Users must create new budget line items for each year
- [ ] Multiple line items can exist for same category and year (no duplicate prevention or warnings)

### Story 2: User - Views Budget Status
**As a** User who is watching my spending
**I want** see how I'm doing against my budget
**So that** I can stay on track with my financial goals

**NOTE**: Budget report display specifications are in [`PRD-REPORTS.md`](../reports/PRD-REPORTS.md) Story 5. This story focuses on budget data availability and calculation behavior.

**Acceptance Criteria**:

**Budget Data Availability:**
- [ ] Budget line items created in Story 1 are available for report calculations
- [ ] Budget calculations use Year and Amount fields for annual budgets

**Column Calculation Behavior:**
- [ ] Budget column calculation: Sum all budget line items for the specified year
- [ ] Actual column calculation: Sum all transaction splits from Jan 1 to report end date
- [ ] %Spent calculation: (Actual / Budget × 100%)

**Hierarchy and Rollup Rules:**
- [ ] Budget values roll up additively: parent budget + sum of all descendant budgets (per Story 3 additive model)
- [ ] Actual values roll up ALL spending in category tree including unbudgeted subcategories
- [ ] Categories without budget line items show appropriate indicators (see Feature Deep-Dive for display behavior)

### Story 3: User - Allocates budget at any category hierarchy level
**As a** User who is watching my spending
**I want** to assign budgets at varying granularity
**So that** I can track spending according to my own particular mental model

**Acceptance Criteria**:

**High-Level Budgets:**
- [ ] User can specify budget at top level (e.g., "Entertainment")
- [ ] Top-level category shows budget and %spent values

**Low-Level Budgets:**
- [ ] User can specify budget at any subcategory depth (e.g., "Transportation:Repairs:Jeep Cherokee")
- [ ] All hierarchy levels show budget and %spent values rolled up from children

**Mixed-Level Budgets (Additive Model):**
- [ ] User can specify budgets at multiple hierarchy levels within same category tree
- [ ] Example: $10k at "Transportation" + $3k at "Transportation:Repairs:Jeep" = $13k total Budget at Transportation level
- [ ] Budget column shows additive rollup: parent budget + sum of all descendant budgets
- [ ] Actual column shows ALL spending in category tree: Transportation direct + Fuel + Repairs + Insurance + all subcategories

**Automatic Rollup Behavior:**
- [ ] Parent categories without direct budgets automatically show rolled-up values from children
- [ ] Example: Only "Transportation:Repairs:Jeep" budgeted at $3k → "Transportation" shows $3k Budget rolled up
- [ ] Actual column at "Transportation" level shows ALL Transportation spending (including unbudgeted categories like Fuel, Insurance)

**Spent% Calculation:**
- [ ] Spent% at any level = (All category spending including children) / (Sum of all budget line items for that category tree)
- [ ] Example: $3k budget at "Repairs:Jeep" only, but $15k total Transportation spending → Transportation shows 500% spent

### Story 4: User - Creates new budget based on historical data [Post V3]
**As a** User who is watching my budget
**I want** create next year's budget based on what I actually spent this year
**So that** my budget is realistic and I don't have to manually calculate averages for each category

**Acceptance Criteria**:

**Historical Data Selection:**
- [ ] User can select a prior year as basis for new budget
- [ ] User can optionally select multiple prior years to average spending across (e.g., 2022-2024 actuals for 2025 budget)
- [ ] Default source is immediate prior year's actuals (e.g., 2024 actuals for 2025 budget)

**Spending Calculation:**
- [ ] System calculates actual spending per category for selected year(s) (sum of all transaction splits in each category)
- [ ] When multiple years selected, system calculates average: (sum of all splits in category / number of years)
- [ ] Categories with minimal spending (< $100/year average) are excluded but can be manually added

**Budget Line Item Creation:**
- [ ] System creates new budget line items with Amount = average actual spending
- [ ] Year = target year for new budget
- [ ] Memo auto-generated documenting calculation basis (e.g., "Based on 2024 actual spending")

**User Review and Approval:**
- [ ] System shows preview of proposed budget line items before creation
- [ ] Preview includes comparison: historical actuals vs. proposed budget amounts
- [ ] User can review and edit proposed line items (modify amounts, add/remove categories)
- [ ] If budget line items already exist for target year, user receives warning before proceeding (avoids accidental duplication)

### Story 5: User - Applies CAGR growth to trending categories [Post V3]
**As a** User who is watching my budget
**I want** the system to detect spending trends and apply growth rates automatically
**So that** my budget accounts for inflation and predictable increases like utilities without manual calculation

**Acceptance Criteria**:

**CAGR Detection Requirements:**
- [ ] Feature available only when user selects 5+ years of historical data for budget creation
- [ ] System calculates CAGR (Compound Annual Growth Rate) for each category
- [ ] System offers CAGR option only for categories with statistically significant trends (R² > 0.7 threshold)
- [ ] Categories without significant CAGR trend fall back to average-based calculation from Story 4

**Budget Creation Preview with CAGR:**
- [ ] Categories with detected CAGR show checkbox option: "Apply CAGR growth (detected [X]% annual increase/decrease [year range])"
- [ ] CAGR checkbox defaults to OFF (opt-in per category)
- [ ] Preview shows both options side-by-side for comparison: "With CAGR: $1260/year" vs "Average: $1140/year"
- [ ] System displays visual indicator (e.g., 📈 icon) next to categories with detected growth trends

**CAGR Budget Calculation:**
- [ ] When CAGR checkbox selected: Budget amount = (last year actual spending) × (1 + CAGR)
- [ ] CAGR uses last year actual as baseline, NOT multi-year average
- [ ] Example: Electric bill 2024 actual = $1200, 5-year CAGR = 5% → 2025 budget = $1260 annual
- [ ] System auto-generates memo: "Based on [last year] actual $[amount] + [X]% CAGR ([year range] trend)"

**Notes**:
- CAGR calculation requires minimum 5 years of data; with fewer years, this feature is not available
- Statistical threshold (R² > 0.7) ensures CAGR is only suggested when trend is meaningful, avoiding spurious growth on random variations
- Last year actual as baseline (not average) captures current spending level plus projected growth
- **Dependency**: This story depends on Story 4 (historical budget creation). Story 5's "fallback to average" assumes Story 4's average calculation is available.

---

## Technical Approach

Budget feature introduces a new entity for tracking annual spending targets per category. Reports are enhanced with budget comparison columns following existing report patterns.

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
- Amount (decimal, required) - Annual budget amount for the full year, using typical signage for the category (expenses are negative, income is positive)
- Year (int, required) - Calendar year the budget applies to (e.g., 2025)
- Memo (string, optional) - Descriptive note for the budget line item
- Id (Guid, required) - Primary key
- Created/Modified timestamps - Standard audit fields

**Key Business Rules**:

1. **Annual Budget Scope** - All budgets are annual (full calendar year from Jan 1 to Dec 31). The Amount field represents the total budget for the entire year. Budget line items only apply to their specified Year.

2. **Annual Expiration with Manual Renewal** - Budget line items only apply to their specified calendar year. Users must create new budget line items for each year (motivates Story 4: historical data copy feature).

3. **Additive Hierarchy Rollups** - Parent category budgets sum their own allocations plus all descendant allocations. If "Transportation" has $10k budget AND "Transportation:Repairs:Jeep" has $3k budget, Transportation shows $13k total. Rollups display even when parent has no direct budget (e.g., only "Transportation:Repairs:Jeep" budgeted → "Transportation" shows $3k rolled up).

4. **Spent Percentage Calculation** - Spent% = (Actual spending in category for the year) / (Total annual budget from all matching line items). May exceed 100% when overspending. May show high percentages for partially-budgeted category trees where spending exceeds budgeted subcategories.

5. **Category Flexibility** - Users can create budgets for any category type (Expenses, Income, Taxes, Savings, etc.). Only constraint is non-blank category name. Multiple line items can exist for same category and year (system sums them when calculating total budget).

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

**All questions have been resolved and integrated into the appropriate sections.**

**Resolved Questions from simplification** (moved to appropriate sections):
- ✅ **Budget prorating** → Budget column shows full annual amount, report header shows % year complete for context
- ✅ **Budget amount signage** → Users enter signed values matching category direction (expenses negative, income positive)
- ✅ **Duplicate line items** → Allow unrestricted creation, no warnings (Story 1)
- ✅ **Budget filtering** → Added Year and Category pattern filtering to Story 1 for report drill-down

**Previously resolved questions** (moved to appropriate sections):
- ✅ **Story 2: Report Location** → Budget report display specifications moved to [`PRD-REPORTS.md`](../reports/PRD-REPORTS.md) Story 5. Budgets PRD Story 2 focuses on budget data calculations and availability.
- ✅ **Budget accumulation model** → Business Rule #1
- ✅ **Annual renewal** → Business Rule #2
- ✅ **Additive hierarchy** → Business Rule #3 and Story 2 report descriptions
- ✅ **Category flexibility** → Business Rule #5
- ✅ **Non-Goals clarification** → Updated to "ML/AI-suggested budgets" (Stories 4-5 are rules-based)
- ✅ **Story 5 dependency** → Added dependency note to Story 5
- ✅ **Target release** → Header shows "Beta 3" (predominant milestone); Stories 4-5 tagged [Post V3]
- ✅ **Story 2: Report list from YoFi V1** → Pre-defined budget reports documented in Story 2
- ✅ **Story 2: "Full Budget" columns** → 2 columns: Category (with hierarchy), Budget (total annual)
- ✅ **Story 2: "All vs Budget" scope** → All categories with any budget (Income, Expenses, Taxes, Savings, etc.)
- ✅ **Story 2: "Expenses vs Budget" chart rollups** → Only top-level categories shown, all subcategory budgets roll up to parent, additive model
- ✅ **Story 2: "Expenses Budget" vs "Expenses vs Budget"** → "Budget" reports show only budget column (no actuals), "vs Budget" reports show Budget/Actual/%Spent columns
- ✅ **Story 4: Minimal spending threshold** → Updated to $100/year (V1 standard practice)
- ✅ **Story 4: Category name changes** → System creates budgets for categories as they exist in historical data; users expected to move transactions when renaming categories
- ✅ **Story 5: R² threshold** → 0.7 is acceptable starting point, can tune based on real outcomes
- ✅ **BudgetLineItem.Amount semantics** → Annual amount for the full year
- ✅ **Actual spending rollup** → Feature Deep-Dive Year 4 demonstrates both Budget AND Actual columns roll up hierarchically (Option A: ALL spending including all children)
- ✅ **Report hierarchy control** → Feature Deep-Dive clarifies users can adjust report depth; specific reports default to ONE-LEVEL
- ✅ **Partially-budgeted trees** → Intended behavior; high spent% (e.g., 500%) indicates user needs to add more budget line items or reflects deliberate partial tracking

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
- **Calendar year boundary** - System hardcoded to calendar year (Jan 1 - Dec 31). No support for fiscal years or custom budget periods in V3.
- **No rollover budget between years** - Unspent budget does not carry into next calendar year. Annual renewal is manual (addressed by Story 4 in future).

---

## Notes & Context

**Background**: Budgeting is a core personal finance practice that transforms YoFi from a passive tracking tool into an active financial management system. While transaction entry and reports show "what happened," budgets enable users to proactively plan "what should happen" and monitor progress toward spending goals.

**Design Philosophy**:
- **Annual Simplicity**: All budgets are annual (Jan 1 to Dec 31), providing straightforward year-long targets without complex frequency calculations. This matches how most people think about yearly financial planning.
- **Flexible Granularity**: Support both high-level budgets ("Entertainment": $5,000/year) and detailed breakdowns ("Transportation:Fuel:Truck": $2,400/year, "Transportation:Fuel:Sedan": $1,800/year). Users choose their own mental model.
- **Report Integration**: Budget variance appears as additional columns in existing Reports feature, not a separate UI. Leverages user familiarity with report structure and reduces learning curve.

**Evolution from YoFi V1**: YoFi V1 included budgeting with frequency-based budgets and accumulation patterns. V3 simplifies to annual-only budgets while preserving the hierarchy patterns, modernizing the data model (tenant isolation, improved entity design), and enhancing report integration (drill-down to transactions, better visual indicators).

**Future Extensibility**: Story 4 (Post V3) addresses annual renewal friction through historical data copy/template features. Potential future enhancements include budget forecasting ("at current rate, will exceed budget by X"), budget templates/presets, and envelope budgeting support.

**Related Documents**:
- [`PRD-REPORTS.md`](../reports/PRD-REPORTS.md) - Story 5 defines budget variance reporting requirements
- [`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md) - Defines split data model that budgets aggregate
- [`PRD-TRANSACTION-FILTERING.md`](../transactions/PRD-TRANSACTION-FILTERING.md) - Filtering for budget drill-down navigation
- [`PRODUCT-ROADMAP.md`](../../PRODUCT-ROADMAP.md) - Strategic context: "Reports & Insights" theme, Beta 3 target

---

## Handoff Checklist (for AI implementation): ✅ **READY FOR HANDOFF**

The document successfully meets all handoff criteria:
1. ✅ Maintains PRD scope (WHAT/WHY vs HOW)
2. ✅ Clear, testable acceptance criteria for all stories
3. ✅ Functional questions resolved, only documentation organization remains open
4. ✅ All affected layers identified with descriptions
5. ✅ Code patterns referenced with specific file links

**Strengths:**
- Comprehensive Feature Deep-Dive with realistic multi-year narrative
- Well-structured acceptance criteria organized by functional area
- Extensive resolved questions list (20 items) shows thorough review
- Clear business rules with examples
- Appropriate level of detail for PRD (not overly prescriptive)

**Minor Note:**
The one remaining open question is about document organization (Budgets PRD vs Reports PRD content split), which is appropriate to resolve before final publication but doesn't block implementation handoff.
