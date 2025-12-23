# YoFi.V3 Alpha/Beta Release Schedule

**Document Version**: 1.0
**Date**: December 2025
**Status**: Draft - For Review and Approval

---

## Executive Summary

Based on analysis of 6 approved PRDs and historical development velocity (Phase 1 completed in ~8 weeks, Nov-Dec 2025), this document provides estimated delivery timelines for Alpha 1 through Beta 3 releases.

**Key Findings**:
- **Alpha 1 target**: 8-10 weeks (approximately **mid-March 2026**)
- **Beta 1 target**: 16-20 weeks from now (approximately **mid-May 2026**)
- **Beta 3 target**: 24-30 weeks from now (approximately **late June 2026**)

**Critical Path**: Transaction Record → Transaction Splits → Bank Import → Payee Rules (auto-categorization essential for usability)

---

## Historical Velocity Analysis

### Phase 1 Accomplishments (Nov-Dec 2025, ~8 weeks)

**Completed in ~8 weeks**:
- ✅ Multi-tenancy infrastructure with role-based authorization
- ✅ Authentication system (ASP.NET Core Identity + NuxtIdentity + JWT)
- ✅ Transaction CRUD with tenant isolation
- ✅ Frontend pages (transactions list, tenant management, workspaces)
- ✅ Comprehensive integration and functional tests
- ✅ Error handling framework
- ✅ Production infrastructure (Azure deployment, CI/CD pipelines)

**Observations**:
- **Strong foundation-first approach** - Invested heavily in architecture, testing, and infrastructure
- **Comprehensive testing from day one** - Unit, integration, and functional tests
- **Clean architecture** - Multiple projects, clear separation of concerns
- **Documentation discipline** - ADRs, TENANCY.md, ARCHITECTURE.md created alongside implementation

**Estimated Velocity**:
- **Full-stack feature delivery**: ~1-2 weeks per feature (with testing and documentation)
- **Complex features (multi-tenancy)**: 3-4 weeks
- **Simple features (transactions CRUD)**: 1-2 weeks

---

## PRD Complexity Analysis

### Story Point Estimation

| Feature | Stories | Acceptance Criteria | Complexity | Estimated Weeks |
|---------|---------|---------------------|------------|----------------|
| **Transaction Record** | 3 | 7 criteria | Low | 1-2 weeks |
| **Transaction Splits** | 6 (1 superseded) | 50+ criteria | High | 3-4 weeks |
| **Transaction Filtering** | 5 | 20+ criteria | Medium | 2-3 weeks |
| **Bank Import** | 4 | 34 criteria | High | 3-4 weeks |
| **Payee Rules** | 5 | 50+ criteria | High | 3-4 weeks |
| **Tenant Data Admin** | 5 | 30+ criteria | Medium | 2-3 weeks |
| **Reports** | 8 | 40+ criteria | High | 4-5 weeks |
| **Transaction Receipts** | 4 | 30+ criteria | High | 3-4 weeks |

### Complexity Factors

**Transaction Record** (Low Complexity):
- ✅ Entity schema mostly exists (Date, Payee, Amount already present)
- Add 4 new fields (Category, Memo, Source, ExternalId)
- Category cleanup logic (whitespace normalization)
- Plumb through existing CRUD stack
- **Estimate**: 1-2 weeks

**Transaction Splits** (High Complexity):
- New Split entity with many-to-one relationship
- Complex business rules (balance validation, at least one split)
- Excel upload feature (XLSX parsing)
- Modified Transaction entity (add Splits navigation)
- DTOs for list view (HasMultipleSplits, IsBalanced)
- Comprehensive design document already exists
- **Estimate**: 3-4 weeks

**Transaction Filtering** (Medium Complexity):
- Collapsible filter UI component
- Multi-field search (payee, category, memo, amount)
- Date range quick-select and custom inputs
- Query parameter handling in API
- Filter logic in Application layer
- localStorage for preferences
- Comprehensive UI design document exists (651 lines)
- **Estimate**: 2-3 weeks

**Bank Import** (High Complexity):
- OFX/QFX file parsing (new library integration)
- Import review workflow (temporary state management)
- Duplicate detection (exact and potential duplicates)
- Three-category display (New/Exact/Potential)
- Persistent review state across sessions
- Select/deselect UI with bulk operations
- **Estimate**: 3-4 weeks

**Payee Rules** (High Complexity):
- New PayeeMatchingRule entity
- Pattern matching (substring and regex)
- Conflict resolution (multi-aspect precedence)
- ReDoS protection (100ms timeout)
- Usage statistics (LastUsedAt, MatchCount)
- Category normalization
- Rule CRUD UI
- Manual trigger matching endpoint
- Advanced matching (source, amount, ranges)
- **Estimate**: 3-4 weeks

**Tenant Data Admin** (Medium Complexity):
- XLSX import/export for all entity types
- Data-agnostic design (works with any entity)
- Duplicate detection during import
- Entity selection UI (checkboxes for transactions/payees/budgets)
- Sample data loading
- Delete all data workflow
- **Estimate**: 2-3 weeks

**Reports** (High Complexity):
- Income/expense report with category breakdown
- Chart visualization (bar/line charts)
- Date range configuration
- Summary report (category totals)
- Complete history over time (year columns)
- Drill-down to underlying transactions (filtering integration)
- Export to Excel/CSV
- **Estimate**: 4-5 weeks

**Transaction Receipts** (High Complexity):
- New Attachment entity with TransactionId FK
- Azure Blob Storage integration (production)
- File system storage (development/container)
- Inbox zero workflow (receipts inbox)
- Filename-based matching algorithm (date, amount, payee parsing)
- Confidence levels (High/Medium/None)
- Match vs Assign UI patterns
- Match review page with transaction selection
- Direct upload from transaction details
- Race condition handling
- **Estimate**: 3-4 weeks

---

## Release Planning

### Alpha 1: Primary Path MVP (Target: **Mid-March 2026**, 8-10 weeks)

**Goal**: Happy path works end-to-end. First reviewable version.

**Features Required**:
1. **Transaction Record** (Stories 1, 2, 3) - 1-2 weeks
2. **Transaction Splits** (Story 3 - Simple Single-Category Workflow, Story 5 - Import with Splits) - 2 weeks
3. **Bank Import** (Story 1 - Upload Bank File) - 2-3 weeks
4. **Payee Rules** (Story 2 - Auto-categorize on Import) - 2 weeks
5. **Tenant Data Admin** (Story 4 - Load Sample Data) - 1 week
6. **Transaction Filtering** (Story 1 - Quick Text Search) - 1 week
7. **Reports** (Story 1 - View Built-in Income/Expense Report) - 2 weeks

**Iteration Breakdown**:
- **Iteration 1** (Weeks 1-3): Transaction Record, Transaction Splits (simple workflow), Bank Import (Story 1)
- **Iteration 2** (Weeks 3-5): Tenant Data Admin (sample data), Transaction Filtering (quick search)
- **Iteration 3** (Weeks 5-7): Payee Rules (auto-categorize), Bank Import integration
- **Iteration 4** (Weeks 7-10): Reports (income/expense), polish, testing, evaluation guide

**System Readiness**:
- Docker publishing path working
- Evaluator's guide created
- Home screen with evaluation guidance
- Test API key protection (already complete)

**Total Duration**: **8-10 weeks** from start (assume concurrent work in progress from Phase 1 completion)

---

### Beta 1: First Personally Usable (Target: **Mid-May 2026**, 16-20 weeks total)

**Goal**: Sufficient for personal use with real financial data.

**Additional Features**:
1. **Transaction Filtering** (Story 2 - Uncategorized, Story 4 - Advanced Filtering) - 2 weeks
2. **Tenant Data Admin** (Story 2 - Import with Detection) - 2 weeks
3. **System Readiness** (Security, logging, seed user, production troubleshooting) - 2 weeks

**Cumulative Work**: Alpha 1 (8-10 weeks) + Beta 1 additions (6 weeks) = **14-16 weeks**

**Security Enhancements**:
- Test API key in production (already complete)
- Seed user functionality
- No registration without invitation
- Log redaction for financial data (implement PII redaction)
- Functional tests against production container
- Production troubleshooting guide

**From Alpha 1 End**: **6 weeks**
**Total from Now**: **16-20 weeks**

---

### Beta 2: Household Usable (Target: **Early June 2026**, 20-24 weeks total)

**Goal**: Complete pathway for household usage. Beta testers can use alongside YoFi V1.

**Additional Features**:
1. **Transaction Splits** (Full implementation - Stories 1, 4, 6) - 3 weeks
2. **Bank Import** (Stories 2, 3, 4 - Review, Manage State, Handle Errors) - 2 weeks
3. **Payee Rules** (Story 1 - CRUD) - 2 weeks
4. **Reports** (Story 2 - Configure Display, Story 4 - Summary Report) - 2 weeks
5. **Transaction Receipts** (Story 1 - Direct Upload) - 1 week

**System Readiness**:
- Invitations MVP
- User role management API (from TODO.md Phase 2)

**From Beta 1 End**: **10 weeks**
**Total from Now**: **20-24 weeks**

---

### Beta 3: Household Active (Target: **Late June 2026**, 24-30 weeks total)

**Goal**: Beta testers move wholly off YoFi V1.

**Additional Features**:
1. **Transaction Receipts** (Stories 2, 3, 4 - Full implementation) - 3 weeks
2. **Payee Rules** (Story 3 - Manual Trigger Matching) - 1 week
3. **Reports** (Stories 4, 6 - Summary, History Over Time) - 2 weeks

**From Beta 2 End**: **6 weeks**
**Total from Now**: **24-30 weeks**

---

### V3.0: Public Release (Target: **Q3 2026**, 30-40 weeks total)

**Goal**: Widely released. Minimal V1 regressions.

**Additional Features**:
1. **Transaction Filtering** (Story 3 - Default Date Range) - 1 week
2. **Tenant Data Admin** (Stories 1, 3 - Export with Selection, Delete All Data) - 2 weeks
3. **Reports** (Story 3 - Chart Form) - 2 weeks
4. **Polish and refinement** - 4-6 weeks

**From Beta 3 End**: **9-11 weeks**
**Total from Now**: **30-40 weeks**

---

## Risk Factors & Contingencies

### High-Risk Items

**1. Bank Import File Parsing** (High Risk, 3-4 weeks)
- **Risk**: OFX/QFX parsing more complex than expected
- **Mitigation**: YoFi V1 reference implementation exists (https://github.com/jcoliz/yofi)
- **Contingency**: +1-2 weeks if library integration issues arise

**2. Azure Blob Storage Integration** (Medium Risk, Transaction Receipts)
- **Risk**: Storage account provisioning, authentication, SDK integration
- **Mitigation**: Use local file system for Alpha/Beta, defer Blob Storage to V3.0
- **Contingency**: +1 week if authentication issues occur

**3. Matching Algorithm Complexity** (Medium Risk, Payee Rules & Receipts)
- **Risk**: Conflict resolution, ReDoS protection, performance with large rule sets
- **Mitigation**: Well-specified PRDs with clear business rules
- **Contingency**: +1 week for performance optimization

**4. Excel Upload Security** (Medium Risk, Transaction Splits & Tenant Data Admin)
- **Risk**: File size limits, MIME validation, malicious file handling
- **Mitigation**: Use established libraries (EPPlus, ClosedXML)
- **Contingency**: Security review may require additional hardening (+1 week)

### Dependencies

**Critical Path**:
```
Transaction Record → Transaction Splits → Bank Import → Payee Rules
```

- **Transaction Record** must be complete before Transaction Splits (entity schema)
- **Transaction Splits** must support simple workflow before Bank Import (single split creation)
- **Bank Import** must work before Payee Rules provide value (auto-categorization on import)

**Parallel Work Opportunities**:
- **Transaction Filtering** can be developed in parallel with Bank Import
- **Tenant Data Admin** (sample data) can be developed early for Alpha 1
- **Reports** can begin once Transaction Record is complete

---

## Schedule Confidence Levels

### High Confidence (±10%)

- **Transaction Record**: 1-2 weeks (entity changes, plumbing through stack)
- **Transaction Filtering** (Story 1 - Quick Search): 1 week (single search bar, backend filtering)
- **Tenant Data Admin** (Story 4 - Sample Data): 1 week (hardcoded data loading)

### Medium Confidence (±20%)

- **Transaction Splits** (Full): 3-4 weeks (new entity, complex business rules, but good design doc)
- **Payee Rules** (Full): 3-4 weeks (new entity, pattern matching, but clear specifications)
- **Transaction Filtering** (Full): 2-3 weeks (UI component, multiple filters, localStorage)
- **Reports** (Story 1): 2 weeks (basic income/expense report)

### Lower Confidence (±30%)

- **Bank Import** (Full): 3-4 weeks (file parsing, new library, import review workflow)
- **Transaction Receipts** (Full): 3-4 weeks (Azure Storage, matching algorithm, inbox workflow)
- **Reports** (Full): 4-5 weeks (charts, drill-down, export, multiple report types)

---

## Recommended Approach

### Prioritize Critical Path

Focus on features that unlock other features:
1. **Transaction Record** (unlocks everything)
2. **Transaction Splits** (simple workflow only for Alpha 1)
3. **Bank Import** (Story 1 - Upload only)
4. **Payee Rules** (Story 2 - Auto-categorize)

### Defer Non-Critical Stories

**Alpha 1 Deferrals**:
- Transaction Splits Stories 1, 4, 6 (advanced features) → Beta 2
- Bank Import Stories 2, 3, 4 (review workflow complexity) → Beta 2
- Payee Rules Stories 1, 3, 4, 5 (rule management UI) → Beta 2/Post V3
- Transaction Filtering Stories 2, 3, 4 (advanced filters) → Beta 1/V3.0
- Reports Stories 2-8 (advanced features) → Beta 2/V3.0+

**Rationale**: Alpha 1 should demonstrate **happy path only**. Complex workflows and edge case handling deferred to Beta releases.

### Maintain Testing Discipline

Based on Phase 1 success, maintain comprehensive testing:
- **Unit tests** for business logic (Application layer)
- **Integration tests** for API endpoints (Controller layer)
- **Functional tests** for workflows (Playwright)

**Estimated testing overhead**: 30-40% of implementation time (already factored into estimates above)

---

## Milestone Summary

| Milestone | Target Date | Cumulative Weeks | Key Features |
|-----------|-------------|------------------|--------------|
| **Alpha 1** | Mid-March 2026 | 8-10 weeks | Primary path MVP (import → auto-categorize → report) |
| **Beta 1** | Mid-May 2026 | 16-20 weeks | Personally usable with real data |
| **Beta 2** | Early June 2026 | 20-24 weeks | Household usable (splits, receipts, advanced features) |
| **Beta 3** | Late June 2026 | 24-30 weeks | Full YoFi V1 replacement readiness |
| **V3.0** | Q3 2026 | 30-40 weeks | Public release |

---

## Resource Allocation Assumptions

**Assumptions**:
- Single developer (you) working full-time on YoFi.V3
- AI-assisted development (architect mode for planning, code mode for implementation)
- Leveraging existing patterns from Phase 1
- Comprehensive PRDs reduce design time
- Testing remains high priority (30-40% of time)

**Velocity Modifiers**:
- **+20% time** if working part-time or with interruptions
- **-10% time** if AI assistance improves with familiarity
- **+15% time** if significant refactoring needed (unlikely given Phase 1 quality)

---

## Next Steps

1. **Review and approve this schedule** with stakeholders
2. **Prioritize Alpha 1 stories** and assign to iterations
3. **Begin with Transaction Record** (1-2 weeks, unlocks other features)
4. **Establish weekly check-ins** to track progress and adjust estimates
5. **Update this document** as actual velocity data becomes available

---

## Appendix: Story-Level Dependencies

### Transaction Record
- **Blocks**: All other transaction features (splits, filtering, import, reports)
- **Depends on**: Multi-tenancy (✅ Complete)

### Transaction Splits
- **Blocks**: Category-based reports (accurate reporting requires splits)
- **Depends on**: Transaction Record

### Transaction Filtering
- **Blocks**: Reports Story 7 (Investigate Underlying Transactions)
- **Depends on**: Transaction Record (fields to filter on)

### Bank Import
- **Blocks**: Payee Rules Story 2 (auto-categorize on import)
- **Depends on**: Transaction Record (entities to import into)

### Payee Rules
- **Blocks**: Nothing (enhances usability but not required)
- **Depends on**: Bank Import (value proposition is auto-categorization on import)

### Tenant Data Admin
- **Blocks**: Nothing (utility feature)
- **Depends on**: Transaction Record (entities to export)

### Reports
- **Blocks**: Nothing (read-only analytics)
- **Depends on**: Transaction Record + Transaction Splits (for accurate category reports)

### Transaction Receipts
- **Blocks**: Nothing (document management feature)
- **Depends on**: Transaction Record (transactions to attach receipts to)

---

**Document Owner**: James Coliz
**Last Updated**: December 2025
**Next Review**: Weekly during Alpha 1 implementation
