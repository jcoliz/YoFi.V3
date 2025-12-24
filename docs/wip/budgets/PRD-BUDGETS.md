---
status: In Review
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
- ML/AI-suggested budgets (Stories 4-5 use rules-based algorithms, not machine learning)
- Budget forecasting or "if you keep spending" projections
- Multiple budget scenarios or A/B comparison
- Budget sharing across different tenants
- Multi-user approval workflows for budget changes
- Envelope or zero-based budgeting methodologies (future enhancement)

---

## Feature Deep-Dive

The entire answer to "how am I doing on my budget?" is presented through reports. Therefore, it's essential to understand how reports works to help understand how budgeting fits in. Let's walk through a narrative

### Year 1

At the end of the year, my "Income" report looks like this:

| **Income** | **$ Total** | **% Total** |
|------------|-------------|-------------|
| Salary | 50000 | 80.65% |
| Bonus | 10000 | 16.13% |
| - | 2000 | 3.23% |
| **Total** | **62000** | **100%** |

I picked up a little cash for babysitting and lawn mowing, but it wasn't big enough to put in its own category. I could have put it in "Income:Others", but I didn't. I just put it in "Income".

### Year 2

This year, I discovered budgetting. I had YoFi copy over my actuals from Year 1 to make up my Year 2 budget. Now I will look at a hypothetical "Income vs Budget" report.

| **Income** | **Actual** | **Budget** | **% Progress** |
|------------|------------|------------|----------------|
| Salary | 55,000 | 50,000 | 110.00% |
| Bonus  | 12,000 | 10,000 | 120.00% |
| -      | 3,500  | 2,000  | 175.00% |
| **Total** | **70,500** | **62,000** | **113.71%** |

In this case, I added budget line items for "Income:Salary", "Income:Bonus", and "Income" (no subcategory) at the amounts noted above. The system inteprets "Income" category to mean "Income with no subcategory".

I've "over budget" in all my areas. I got a raise, got a bigger bonus, and did more lawn mowing. Good for me!

### Year 3

This year, I created the budget from last years actuals, BUT I am making enough that I will break out each of the additional items into their own category. Here's how "Income vs Budget" looks at the end of the year.

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | 60,000     | 55,000     | 109%           |
| Bonus        | 10,000     | 12,000     | 83%            |
| Babysitting  | 2,000      | 0          | —              |
| Lawn mowing  | 2,000      | 0          | —              |
| -            | 0          | 3,500      | 0%             |
| **Total**    | **74,000** | **70,500** | **105%**       |

The $3500 uncategorized budget didn't help me track how I was doing on my smaller items, but that's OK. It contributes to the overall totals, so I can see I'm still just a little over budget (good!) on income this year.

**Q**: The $3,500 budgeted for "Income" (no subcategory) becomes unused because actuals moved to subcategories. Is this the intended teaching point — that budgets don't automatically adjust when users refine their categorization?

**A**: While it's true that budgets do not automatically adjust, the teaching point is to see what is the result when we have budget in a top-level category, but no transactions in the top-level category.

**Q** Why show budget "0" instead of "—" (dash) to indicate "no budget exists"?
**A** Perhaps a blank would be better

**Q** Is "0" the actual budget value, or is it showing "0" because no budget line item exists?
**A** As described here, it's because no budget line item exists

**Q** The "—" in %Progress makes sense (can't divide by zero),
**A** Correct

**Q** but shouldn't budget column also show "—"?
**A** Well, Progress column is Actual/Budget, so technically that's zero

**Q**: Is this the intended behavior to demonstrate that budgets remain tied to their specific category paths and don't "follow" spending when users change categorization habits? This is a valuable real-world scenario showing budget maintenance requirements.
**A** Again, this is mostly to show what the system does given varying inputs. This narrative is an observed path of how things are actually used. There is often a lag between when user makes a change to their budget system, and then the world shifts the next year.

**Q** Is there any UI affordance to help users identify "orphaned" budgets (budgets with zero spending because categorization changed)? Or is this left to users to notice via the 0% spent indicator?
**A** No, only because additional affordances provide clutter more than clarity in my view. For year 3, this is prime example. We don't need a callout on the "Income" (no subcategory) budget. It's getting caught in the roll-up, so the user gets the info they need in this case.

**Q** Edge case: What if user had transactions in "Income:Side gigs" in Year 2, then renamed the category to "Income:Babysitting" in Year 3? The budget system would show budget for "Side gigs" and actuals for "Babysitting" (no match). Is this expected, or should there be category rename/merge tooling?
**A** This depends on what the user did when setting the Year 3 budget. If the user set their budget with "Side gigs", but in fact put transactions in "Babysitting", then yes you'd see this:

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | 60,000     | 55,000     | 109%           |
| Bonus        | 10,000     | 12,000     | 83%            |
| Babysitting  | 2,000      | 0          | —              |
| Lawn mowing  | 2,000      | 0          | —              |
| Side Gigs    | 0          | 3,500      | 0%             |
| **Total**    | **74,000** | **70,500** | **105%**       |

User can always go CHANGE the budget mid-year when they realize they are tracking a different way than expected.

**Q** Partial Year Budget Creation
The narrative shows budgets created at the start of the year. What if user creates the "Income:Babysitting" budget mid-year (July 1)?
- Would it show partial budget accumulation (Jul-Dec only)?
- How would %Progress be calculated against a partial-year budget when comparing to full-year actuals?
This scenario isn't covered in the narrative but could be common (users adopt budgeting mid-year).

**A** For simplicity, let's say user entered "Income:Babysitting" as "yearly" budget line item, with date 7/1, and amount $2000. It's now 12/31. User received $500 for babysitting on 3/1, 6/1, 9/1, and 12/1. For simplicity, let's say no other side gigs. Then we'd have this:

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | 60,000     | 55,000     | 109%           |
| Bonus        | 10,000     | 12,000     | 83%            |
| Babysitting  | 2,000      | 2,000      | 100%           |
| **Total**    | **72,000** | **69,000** | **104%**       |

**Q**: Why does Actual show full-year spending ($2k including March and June) against partial-year budget ($2k starting July)?
**A**: This might make a bit more sense if I had used a spending case instead of an income case. Let me switch over to a spending example for this answer. Consider a budget line item as "permission to spend". On 7/1 I received permission to spend $2000. From that day forward, I have that permission, and I can spend it whenever I want. What would happen if I viewed a report on 6/31? I'd have spent $1000 already against a 0 budget, so I am $1000 over budget (bad!). Now on 7/1, I have spent $1000 against a $2000 budget, so I am at 50% of by budget half way through the year--perfect!

But yes, in your options this is option A: (Actual = full year regardless of budget start date). This is important because it means users adopting budgeting mid-year will often see >100% spent on partial-year budgets.

**Recommendation**: Confirm Option A is intended, and consider adding this as explicit behavior in Story 2 acceptance criteria: "Actual column always shows cumulative spending from Jan 1 to report date, regardless of budget line item StartDate."

Yes, all the stories need an update. That's why I wrote this, to have a reference to update the stories.

**A** Budget Display for Categories Without Budget Line Items. Yes, we will leave this as an open question for now. This is only a UI decision and shouldn't affect the design of the feature.

### Year 4

**Q** Inferred rule: Actual column shows transactions directly assigned to that exact category path, WITHOUT rolling up from children** (different from Budget column, which DOES roll up): Is this the correct interpretation? This is a critical behavioral difference between Budget (rolls up) and Actual (doesn't roll up) columns.
**A** NO. Let me add Year 4 to try to explain.

In year 4, I decide to clump all my side-gigs under a "Income:Side Gigs", e.g. "Income:Side Gigs:Babysitting," and "Income:Side Gigs:Lawn Mowing". I made $2000 babysitting and $3000 lawn mowing.
At the end of the year "Income vs Budget" looks like this.

Because this is a ONE-LEVEL report, all subcategories are rolled up. '-' is a special case, covering uncategorized splits in the parent.

| **Income**   | **Actual** | **Budget** | **% Progress** |
|--------------|------------|------------|----------------|
| Salary       | $65,000    | $60,000    | 108%           |
| Bonus        | $15,000    | $10,000    | 150%           |
| Side Gigs    | $5,000     | $4,000     | 125%           |
| **Total**    | **$85,000** | **$74,000** | **115%**       |

**Q** Are report depth levels user-configurable, or are specific reports pre-defined at specific depths? (This may be covered in Reports PRD, so acceptable to leave unanswered here)
**A** Both. Specific reports default to certain hierarchy levels, but user can always change. Note that in year 3, we are STILL only showing a ONE-LEVEL report. In that case, we put splits into "Income:Babysitting" and "Income:Lawn Mowing".

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
- [ ] Budget line items include amount (budget amount per frequency period)
- [ ] Budget line items include frequency (weekly, monthly, quarterly, or yearly)
- [ ] Budget line items include start date (date when funds become "available", typically Jan 1)
- [ ] Budget line items include memo (optional descriptive note)

**Budget Accumulation Behavior:**
- [ ] Budget line items apply only to the calendar year of their start date
- [ ] Budget accumulates from start date through Dec 31 based on frequency
- [ ] Example: Weekly $50 budget starting 12/1 provides ~4 weeks of budget before year end expiration

### Story 2: User - Views Budget Status
**As a** User who is watching my spending
**I want** see how I'm doing against my budget
**So that** I can stay on track with my financial goals

**Acceptance Criteria**:

**Report Selection:**
- [ ] User can select from pre-defined budget reports comparing actual spending against budgeted spending

**Report Column Definitions:**
- [ ] Budget column shows cumulative budget available from year start to report date (sum of all elapsed periods based on frequency)
- [ ] Actual column shows cumulative spending from year start to report date (same time period as budget column)
- [ ] %Spent column shows percentage of budget consumed (Actual / Budget × 100%)
- [ ] Example: Budget $50, Actual $30, %Spent 60%

**Report Header Information:**
- [ ] Budget reports show % complete of the year so far in header
- [ ] Users can compare spent% versus year% to assess pacing (e.g., 60% through year with 60% spent = on track)

**Pre-defined budget reports**
- "Full Budget": Shows hierarchy of categories with any budget applied within them, with rollup totals. Shows 2 columns: Category (with hierarchy), and Budget (total annual budget for entire year). No actuals comparison.
- "All vs Budget": Shows all categories (Income, Expenses, Taxes, Savings, etc.) which have a budget, and rollup totals. Columns: Budget (cumulative to date), Actual, %Spent. Note that Budget CANNOT have a zero at any point in this report.
- "Expenses Budget": Like "Full Budget" but only shows expenses (see Reports PRD for a definition of what "Expenses" is). Shows only budget column, no actuals comparison.
- "Expenses vs Budget": Like "All vs Budget", but only Expenses. Includes table view (Budget, Actual, %Spent columns) and chart view with special handling. Chart is a double bar chart: X axis shows top-level expense categories in descending total budget amount (all subcategory budgets roll up to parent), Y axis is $ amount. Each X-axis stop has a PAIR of bars showing "Actual" and "Budget" for that top-level category. Mixed-level budgets are additive (e.g., $10k Transportation + $3k Transportation:Repairs:Jeep = $13k total rolled up to Transportation bar).

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
- [ ] Example: $10k at "Transportation" + $3k at "Transportation:Repairs:Jeep" = $13k total at Transportation level
- [ ] Budget column shows additive rollup: parent budget + sum of all descendant budgets

**Automatic Rollup Behavior:**
- [ ] Parent categories without direct budgets automatically show rolled-up values from children
- [ ] Example: Only "Transportation:Repairs:Jeep" budgeted at $3k → "Transportation" and "Transportation:Repairs" show $3k rolled up

**Spent% Calculation:**
- [ ] Spent% at any level = (All category spending including children) / (Sum of all budget line items for that category tree)

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

**Frequency Detection:**
- [ ] System automatically determines appropriate frequency per category based on spending patterns
- [ ] Consistent monthly spending → monthly frequency
- [ ] Irregular spending → yearly frequency

**Budget Line Item Creation:**
- [ ] System creates new budget line items with Amount = (average actual spending / appropriate periods)
- [ ] StartDate = 1/1/[new year]
- [ ] Frequency = [determined frequency from spending pattern analysis]
- [ ] Memo auto-generated documenting calculation basis

**User Review and Approval:**
- [ ] System shows preview of proposed budget line items before creation
- [ ] Preview includes comparison: historical actuals vs. proposed budget amounts
- [ ] User can review and edit proposed line items (modify amounts, frequencies, add/remove categories)
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
- [ ] Example: Electric bill 2024 actual = $1200, 5-year CAGR = 5% → 2025 budget = $1260 annual ($105/month)
- [ ] System auto-generates memo: "Based on [last year] actual $[amount] + [X]% CAGR ([year range] trend)"

**Notes**:
- CAGR calculation requires minimum 5 years of data; with fewer years, this feature is not available
- Statistical threshold (R² > 0.7) ensures CAGR is only suggested when trend is meaningful, avoiding spurious growth on random variations
- Last year actual as baseline (not average) captures current spending level plus projected growth
- **Dependency**: This story depends on Story 4 (historical budget creation). Story 5's "fallback to average" assumes Story 4's average calculation is available.

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

- [ ] **Story 2: Report Location - What Goes and What Stays?** - Budget report specifications will be split between Budgets PRD and Reports PRD. Need to determine precisely which content stays in Story 2 here vs which detailed specifications move to Reports PRD. For each of the 4 pre-defined budget reports ("Full Budget", "All vs Budget", "Expenses Budget", "Expenses vs Budget"), decide what level of detail belongs in each document. Consider: high-level descriptions vs detailed column specs, chart specifications, rollup behavior details, filtering rules, etc.

- [ ] **Story 3: Actual Spending Rollup for Mixed-Level Budgets** - When "Transportation" has $10k direct budget AND "Transportation:Repairs:Jeep" has $3k budget (total $13k rolled-up budget), what does the "Actual" column at "Transportation" level show?
  - Option A: ALL Transportation spending including all children (e.g., $15k total)
  - Option B: Only spending for categories with budgets, rolled up (e.g., $8k if only Transportation direct + Jeep have spending)
  - This affects spent% calculation: Actual / $13k

- [ ] **Story 3: Spent% for Partially-Budgeted Category Trees** - When only "Transportation:Repairs:Jeep" has $3k budget (no budget at "Transportation" or "Transportation:Repairs" levels), the parent categories show $3k rolled-up budget but ALL Transportation spending in Actual column (including Fuel, Insurance, unbudgeted Repairs, etc.). This could create misleadingly high spent% (e.g., $15k actual / $3k budget = 500%). Is this intended behavior, or should Actual column only show spending for categories that have budgets?

**Resolved Questions** (moved to appropriate sections):
- ✅ **Budget accumulation model** → Business Rule #1
- ✅ **Annual renewal** → Business Rule #2
- ✅ **Additive hierarchy** → Business Rule #3 and Story 2 report descriptions
- ✅ **Category flexibility** → Business Rule #5
- ✅ **Non-Goals clarification** → Updated to "ML/AI-suggested budgets" (Stories 4-5 are rules-based)
- ✅ **Story 5 dependency** → Added dependency note to Story 5
- ✅ **Frequency detection algorithm** → Appropriately vague for PRD level, deferred to design document
- ✅ **Target release** → Header shows "Beta 3" (predominant milestone); Stories 4-5 tagged [Post V3]
- ✅ **Story 2: Report list from YoFi V1** → Pre-defined budget reports documented in Story 2
- ✅ **Story 2: "Full Budget" columns** → 2 columns: Category (with hierarchy), Budget (total annual)
- ✅ **Story 2: "All vs Budget" scope** → All categories with any budget (Income, Expenses, Taxes, Savings, etc.)
- ✅ **Story 2: "Expenses vs Budget" chart rollups** → Only top-level categories shown, all subcategory budgets roll up to parent, additive model
- ✅ **Story 2: "Expenses Budget" vs "Expenses vs Budget"** → "Budget" reports show only budget column (no actuals), "vs Budget" reports show Budget/Actual/%Spent columns
- ✅ **Story 4: Minimal spending threshold** → Updated to $100/year (V1 standard practice)
- ✅ **Story 4: Category name changes** → System creates budgets for categories as they exist in historical data; users expected to move transactions when renaming categories
- ✅ **Story 5: R² threshold** → 0.7 is acceptable starting point, can tune based on real outcomes
- ✅ **BudgetLineItem.Amount semantics** → Per-period amount (Frequency=Monthly, Amount=$500 means $500/month)

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
