# YoFi.V3 Product Roadmap

**Status**: In review
**Last Updated**: 2025-12-21
**Owner**: James Coliz

> [!NOTE] This document itself is a work in progress. When complete, I will move to top level docs as project documentation.

---

## Vision

YoFi.V3 is a modern personal finance tracking application that makes managing household money **effortless through intelligent automation**. We eliminate tedious manual categorization while providing the insights users need to make informed financial decisions.

**Core Principles:**
- **Automation First** - Minimize manual work through smart rules and pattern matching
- **Multi-User Ready** - Built for households and teams from day one
- **Data Privacy** - Your financial data stays under your control
- **Clean UX** - Simple workflows that don't overwhelm casual users

---

## User Journey

The core YoFi workflow follows a continuous cycle of data import, automated categorization, manual review, and insight generation:

```mermaid
graph TB
    Start([User Downloads Bank Statement]) --> Import[Import into YoFi via Bank Import]
    Import --> AutoCat[Categories Automatically Assigned via Payee Rules]
    AutoCat --> Review[Review/Edit Transactions in Transactions Page]
    Review --> Reports[View Reports & Gain Insights]
    Reports --> Export[Access Reports via API for Excel/External Apps]

    style Start fill:#e1f5ff
    style Import fill:#fff4e1
    style AutoCat fill:#e8f5e9
    style Review fill:#f3e5f5
    style Reports fill:#fff9c4
    style Export fill:#ffebee
```

**Primary Flow:**
1. **Download** transactions from bank → **Import** into YoFi
2. **Auto-categorize** transactions via payee matching rules
3. **Review/Edit** transactions (splits, categories, reconciliation)
4. **View reports** (category spending, trends, budgets)
5. **Export** data via API to Excel or external apps

**Side Quests (branch off from Review step):**
- **Update Payee Rules** - Create/edit matching rules, then re-match transactions
- **Workspace Data Management** - Bulk import/export all workspace data (XLSX)
- **Create/Modify Budgets** - Set up category budgets and spending targets

---

## Strategic Themes

### 🧮 Core Transaction Management
**Goal**: Provide robust, flexible transaction tracking that handles real-world complexity

**Why It Matters**: Users need accurate transaction records as the foundation for all financial insights. Single-category transactions are insufficient for real-world scenarios (grocery trips with food + household items, business expenses with mixed categories).

**Features**:
- Transaction CRUD with splits → [PRD: Transaction Record](wip/transactions/PRD-TRANSACTION-RECORD.md), [PRD: Transaction Splits](wip/transactions/PRD-TRANSACTION-SPLITS.md)
- Transaction filtering and search → [PRD: Transaction Filtering](wip/transactions/PRD-TRANSACTION-FILTERING.md)
- Transaction record validation
- Investment transactions management

### 🤖 Data Intelligence & Automation
**Goal**: Reduce manual categorization work through intelligent pattern matching

**Why It Matters**: Manual categorization at scale (hundreds of transactions per month) creates friction that prevents users from maintaining their financial tracking. Automation is the key differentiator that makes YoFi practical for daily use.

**Features**:
- Payee matching rules (substring and regex) → [PRD: Payee Rules](wip/payee-rules/PRD-PAYEE-RULES.md)
- Category autocomplete (future)
- Split templates (future)
- Bulk operations (future)

### 📥 Import & Integration
**Goal**: Seamlessly bring in transaction data from external sources

**Why It Matters**: Manual data entry is the #1 barrier to adoption. Users need frictionless import from their banks and other financial institutions.

**Features**:
- Bank file import (OFX/QFX) with duplicate detection → [PRD: Bank Import](wip/import-export/PRD-BANK-IMPORT.md)
- Tenant data administration (XLSX import/export) → [PRD: Tenant Data Admin](wip/import-export/PRD-TENANT-DATA-ADMIN.md)
- API integrations (future - Plaid, etc.)

### 👥 Multi-User & Collaboration
**Goal**: Enable households and teams to manage finances together

**Why It Matters**: Financial management is often a shared responsibility. Multi-tenancy with role-based access enables collaboration while maintaining data isolation.

**Features**:
- Multi-tenancy (workspace) foundation
- Role-based access (Owner/Editor/Viewer)
- User management
- Activity auditing (future)

### 📊 Reports & Insights
**Goal**: Transform transaction data into actionable insights

**Why It Matters**: The ultimate value of transaction tracking is understanding spending patterns and making informed decisions. Reports close the loop on the user journey.

**Features**:
- Category spending reports → [PRD: Reports](wip/reports/PRD-REPORTS.md)
- Trend analysis and visualizations
- Budget creation and tracking → *PRD to be created (V3.1)*
- API access for external reporting tools → *PRD to be created (Beta 2-3)*
- Custom dashboards
- Net worth tracking (balance sheet view)
- Tax planning and historical tax analysis

### 📎 Document Management
**Goal**: Connect supporting documents with transactions for complete records

**Why It Matters**: Financial tracking requires receipts, invoices, and statements as evidence. Automating attachment matching reduces manual filing work.

**Features**:
- Transaction attachments (receipts, invoices) → *PRD to be created (Beta 3)*
- Bulk document upload with auto-matching to transactions
- Filename-based matching patterns
- Document storage and retrieval

---

## Release Milestones

### Alpha 1: Primary Path MVP, First Reviewable

Happy path works end to end. First version it would make sense to send out for evaluation. There may be additional Alpha releases TBD, if more is needed before going Beta.

**Feature readiness**

- Minimum useful user stories from features on the primary path.
- Probably needs import with first set of sample data

**System readiness**

- All alpha builds of YoFi are not supported with real financial data.
- We need an evaluator's guide.
- Should update the Home Screen with evaluation guidance.
- Must get docker publishing path working

### Beta 1: First personally usable

Just enough for me to use it personally for very limited scenarios with my personal data.

**Feature readiness**

TBD. Enough that I can use it for my personal finances. Probably needs at least data import.

**System readiness**

Actual financial data now here, so production security needs to be tighter.

-	Test API Key in prod
-	Seed User
-	No registration without invitation. Perhaps make test control API generate invitations.
-	Log redaction for financial data
-	Functional tests against production
-	Production troubleshooting guide

### Beta 2: Household usable

Complete pathway for common household usage. Asking for additional beta testers at this point. Not recommending for primary use. Recommend users use YoFi V1 and V3 side-by-side and report issues.

**System Readiness**

-	Invitations MVP

### Beta 3: Household active

Ready for beta testers to move wholly off YoFi V1

### V3.0: Public Release

Widely released. Minimal V1 regressions, except rarely used corner cases.

### V3.1: Near Backlog

Deferred/deferrable stories from release, but really want them.

### Post V3: Under consideration

Expecting to put energy into designing these items once V3.0 is out.

### Future: For potential consideration

May consider in the future. Not actively expecting to put energy on them.

---

## Implementation Timeline

Stories are the fundamental unit of delivery. This timeline shows the expected **release milestone** for each story. Features are completed when all their stories are done. Stories targeted for the next milestone are further sequenced into iterations.

### Story-Level Iteration Roadmap

| Feature: Story                                  | Milestone | Iteration | Status               |
|-------------------------------------------------|-----------|-----------|----------------------|
| **Multi-Tenancy**: All stories complete         | ✅        | ✅        | ✅ Implemented       |
| **Transaction Record**                          |           |           | 🎨 Design Complete   |
| Story 1 - Represent Imported Data               | A1        | 1         | 🎨 Design Complete   |
| Story 2 - Add Additional Information            | A1        | 2         | 🎨 Design Complete   |
| Story 3 - Manage Transactions                   | A1        | 3         | 🎨 Design Complete   |
| **Transaction Splits**                          |           |           | 🎨 Design Complete   |
| Story 1 - Split Single Transaction              | B2        |           | 🎨 Design Complete   |
| Story 2 - View Category Reports                 | -         |           | 🚫 Superseded        |
| Story 3 - Simple Single-Category Workflow       | A1        | 1         | 🎨 Design Complete   |
| Story 4 - Detect Unbalanced Transactions        | B2        |           | 🎨 Design Complete   |
| Story 5 - Import Transactions with Splits       | A1        | 1         | 🎨 Design Complete   |
| **Transaction Filtering**                       |           |           | 🎨 Design Complete   |
| Story 1 - Quick Text Search                     | A1        | 2         | 🎨 Design Complete   |
| Story 2 - Find Uncategorized                    | B1        |           | 🎨 Design Complete   |
| Story 3 - Default Date Range                    | V3.0      |           | 🎨 Design Complete   |
| Story 4 - Advanced Filtering                    | B1        |           | 🎨 Design Complete   |
| Story 5 - Reports Integration                   | V3.1      |           | 💡 Future            |
| **Bank Import**                                 |           |           | ✔️ Approved          |
| Story 1 - Upload Bank File                      | A1        | 1         | ✔️ Approved          |
| Story 2 - Review Imported Transactions          | B2        |           | ✔️ Approved          |
| Story 3 - Manage Import State                   | B2        |           | ✔️ Approved          |
| Story 4 - Handle Import Errors                  | B2        |           | ✔️ Approved          |
| **Payee Rules**                                 |           |           | ✔️ Approved          |
| Story 1 - Establish Rules (CRUD)                | B2        |           | ✔️ Approved          |
| Story 2 - Auto-categorize on Import             | A1        | 3         | ✔️ Approved          |
| Story 3 - Manual Trigger Matching               | B3        |           | ✔️ Approved          |
| Story 4 - Advanced Matching (Source/Amount)     | Post V3   |           | ✔️ Approved          |
| Story 5 - Rule Cleanup                          | Post V3   |           | ✔️ Approved          |
| **Tenant Data Admin**                           |           |           | ✔️ Approved          |
| Story 1 - Export with Selection                 | V3.0      |           | ✔️ Approved          |
| Story 2 - Import with Detection                 | B1        |           | ✔️ Approved          |
| Story 3 - Delete All Data                       | V3.0      |           | ✔️ Approved          |
| Story 4 - Load Sample Data                      | A1        | 2         | ✔️ Approved          |
| Story 5 - Handle Import Errors                  | V3.1      |           | ✔️ Approved          |
| **Reports**                                     |           |           | 📝 Draft             |
| Story 1 - View Built-in Income/Expense Report   | A1        | 4         | 📝 Draft             |
| Story 2 - Configure Report Display              | B2        |           | 📝 Draft             |
| Story 3 - View Report in Chart Form             | V3.0      |           | 📝 Draft             |
| Story 4 - View Summary Report                   | B3        |           | 📝 Draft             |
| Story 5 - View Budget Reports                   | V3.0      |           | 💡 Future            |
| Story 6 - View Complete History Over Time       | B3        |           | 📝 Draft             |
| Story 7 - Investigate Underlying Transactions   | V3.1      |           | 📝 Draft             |
| Story 8 - Define Custom Report                  | Post V3   |           | 💡 Future            |

### Reading this timeline:

- Each row represents one **user story** from a PRD
- Stories within a feature may be worked on in parallel, or at completely unrelated times
- Features are complete when all their stories show ✅
- This is a working timeline - move stories between iterations as priorities change

### How to use this timeline:

1. **Planning**: Assign stories to iterations based on priority and dependencies
2. **Tracking**: Update status as stories progress
3. **Adjusting**: Move stories between iterations as needed
4. **Communicating**: Share progress by pointing to specific story completions

### Status Values
- **💡 Future** - Identified but not yet planned
- **📝 Draft** - Initial requirements gathering, not yet reviewed
- **✔️ Approved** - Requirements approved, ready for design
- **🎨 Design Complete** - Technical design finished, ready for implementation
- **🚧 In Progress** - Implementation underway
- **✅ Implemented** - Code complete and deployed to production
- **🚫 Superseded** - Story kept for reference but replaced by another story or approach

---

## Story Delivery Approach

**Incremental Delivery Philosophy:**
- Features are delivered through **individual user stories**, not monolithic releases
- Stories are prioritized based on user value and technical dependencies
- A "feature" may have stories delivered across multiple releases
- Features remain in "🚧 In Progress" until all stories are complete

**Example: Payee Rules Feature**
```
Story 1: Establish Rules (CRUD) → Delivered in Beta 2
Story 2: Auto-categorize on Import → Delivered in Alpha 1, Iteration 3
Story 3: Manual Trigger Matching → Delivered in Beta 3
Story 4: Advanced Matching (Source/Amount) → Delivered Post V3
Story 5: Rule Cleanup → Delivered Post V3
```

**Tracking Story Progress:**
- Each PRD lists user stories with acceptance criteria
- Feature Map shows "X/Y complete" to track progress
- Stories can be linked to ADO work items for detailed tracking
- Status updates reflect latest completed stories

## Success Metrics

### Product-Level KPIs

**User Engagement**:
- Monthly Active Users (MAU) growth rate
- Average transactions tracked per user per month (target: 100+)
- User retention rate at 90 days (target: >60%)

**Automation Effectiveness**:
- % of transactions auto-categorized via payee rules (target: >80%)
- Average time to categorize 100 transactions (target: <20 seconds)
- % of users who create payee rules within first month (target: >90%)

**Data Quality**:
- % of unbalanced split transactions (target: <1%)
- % of uncategorized transactions older than 30 days (target: <5%)

**Feature Adoption**:
- % of users using bank import (target: >60% within 30 days)
- % of users creating split transactions (target: >20%)
- Average payee rules per active user (target: >50)

### Feature-Specific Metrics
See individual PRDs for detailed feature success metrics.

---

## Out of Scope

**Explicitly NOT in YoFi.V3**:
- ❌ **Bill payment integration** - Tracking only, no payment execution
- ❌ **Tax preparation software** - Tax planning yes, but not a tax prep tool
- ❌ **Multi-currency** - Single currency per tenant (USD assumed)
- ❌ **Loan/mortgage amortization calculators** - Complex financial modeling out of scope
- ❌ **Cryptocurrency trading** - Too volatile and specialized for V3
- ❌ **Credit score monitoring** - External service integration, not core value

---

## Creating a New PRD

For guidance on creating a new PRD, see [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md#creating-a-new-prd).

---

## Related Documentation

### Architecture & Decisions
- [`ARCHITECTURE.md`](ARCHITECTURE.md) - System architecture overview
- [`adr/README.md`](adr/README.md) - Architecture Decision Records

### Technical Guides
- [`TENANCY.md`](TENANCY.md) - Multi-tenancy architecture
- [`LOGGING-POLICY.md`](LOGGING-POLICY.md) - Logging standards
- [`COMMIT-CONVENTIONS.md`](COMMIT-CONVENTIONS.md) - Git commit format

### Development
- [`CONTRIBUTING.md`](CONTRIBUTING.md) - Development guidelines
- [`scripts/README.md`](../scripts/README.md) - Automation scripts
- [`.roorules`](../.roorules) - Project coding standards

---

## Feedback & Updates

This roadmap is a living document. As we learn from users and evolve our understanding of their needs, features may move between releases or change priority.

**How to provide feedback:**
- Open a GitHub Discussion for strategic questions
- Open a GitHub Issue for specific feature requests
- Contact the owner directly for roadmap questions

**Last Major Update**: 2025-12-21 - Initial roadmap creation with integrated feature map
