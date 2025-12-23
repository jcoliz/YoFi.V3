---
status: Approved (Detailed Design Complete)
owner: James Coliz
target_release: Beta 2
ado: TBD
---

# Product Requirements Document: Reports Export API

## Problem Statement

YoFi users who perform advanced financial analysis need their transaction data in Excel for budgeting, forecasting, and custom reporting. Currently, data is locked in the web UI with no programmatic access. Power users must manually export data or cannot access it at all, limiting the value of YoFi for analytical workflows.

**User Impact**: Financial analysts, small business owners, and power users cannot integrate YoFi data into their existing Excel-based financial models and processes.

---

## Goals & Non-Goals

### Goals
- [ ] Enable Excel users to connect directly to YoFi as a data source via API
- [ ] Provide secure, read-only access to aggregated report data
- [ ] Support multiple workspaces from a single Excel connection
- [ ] Deliver data in Excel-optimized format (flat, tabular JSON)

### Non-Goals
- Individual transaction CRUD operations via API (use web UI for editing)
- Write access to data (read-only reports only)
- Real-time data streaming or push notifications
- Support for non-Excel tools in Phase 1 (future: Power BI, Tableau, custom scripts)
- Bulk data export in other formats (CSV, PDF) - Excel can convert if needed

---

## User Stories

### Story 1: Power User - Connect Excel to YoFi Reports

**As a** power user who analyzes spending patterns in Excel
**I want to** connect Excel directly to YoFi's API as a data source
**So that** I can import my transaction data into custom financial models without manual data entry

**Acceptance Criteria**:
- [ ] User can generate an API key from YoFi's web interface
- [ ] API key works with Excel Power Query's "From Web" data source
- [ ] User can authenticate using Bearer token in HTTP header
- [ ] Data loads into Excel as a properly formatted table with detected column types
- [ ] User can refresh data in Excel to get latest from YoFi without re-authentication
- [ ] User can configure the date range for reports via query parameters (fromDate, toDate)
- [ ] API provides only the as-defined structure of the report (no customization of columns/layout)

**Example Excel Workflow**:
1. User navigates to Workspace Settings → API Keys in YoFi web UI
2. Clicks "Generate New API Key" → Receives key like `yofi_user_abc123...`
3. Opens Excel → Data tab → Get Data → From Web
4. Enters URL: `https://api.yofi.app/api/tenant/{workspaceId}/reports/spending/by-category?fromDate=2024-01-01&toDate=2024-12-31`
5. Adds Authorization header: `Bearer yofi_user_abc123...`
6. Data loads as table with columns: Category, Amount, PercentTotal
7. User creates pivot table, charts, and formulas using YoFi data
8. Clicks "Refresh All" weekly to update with latest transactions

### Story 2: Multi-Workspace User - Access Multiple Workspaces from Excel

**As a** user with both personal and business workspaces in YoFi
**I want to** access data from multiple workspaces using the same API key
**So that** I can build consolidated reports without managing multiple credentials

**Acceptance Criteria**:
- [ ] Single API key works for all workspaces user has access to
- [ ] User can query list of available workspaces via API
- [ ] User can create separate Excel queries for each workspace by changing workspace ID in URL
- [ ] Authorization correctly enforces user's role (Viewer/Editor/Owner) in each workspace
- [ ] User denied access to workspace returns clear 403 error

**Example Workflow**:
1. User has API key for personal account
2. First query: `GET /api/user/tenants` → Returns list of workspaces with IDs
3. Creates Excel query for Personal Finances: `/api/tenant/{personal-id}/reports/spending/by-category`
4. Creates Excel query for Business Expenses: `/api/tenant/{business-id}/reports/spending/by-category`
5. Both queries use same API key
6. Excel worksheet has tabs for "Personal" and "Business" data

### Story 3: Business Owner - Secure API Key Management

**As a** workspace owner concerned about data security
**I want to** generate, view, and revoke API keys for my workspace
**So that** I can control programmatic access and respond to security incidents

**Acceptance Criteria**:
- [ ] Only workspace Owners can generate API keys
- [ ] API key shown in full only once at creation (user must copy immediately)
- [ ] User can view list of active API keys with creation date, last used date, and name
- [ ] User can revoke any API key instantly
- [ ] Revoked key immediately returns 401 Unauthorized on next use
- [ ] User can name API keys (e.g., "Excel - Finance Laptop", "John's Analysis")

**Example Workflow**:
1. Owner navigates to Workspace Settings → API Keys
2. Sees list: "Excel - Finance Laptop (created 2024-01-15, last used 2 hours ago)"
3. Clicks "Generate New API Key"
4. Names it: "Excel - Home Computer"
5. Copies key: `yofi_user_xyz789...` (shown once)
6. Later, laptop is stolen → Owner revokes "Excel - Finance Laptop" key
7. Attacker attempts to use stolen key → Receives 401 Unauthorized

### Story 4: Excel User - Discover Available Reports

**As an** Excel user new to YoFi's API
**I want to** discover what reports are available
**So that** I know what data I can pull into Excel

**Acceptance Criteria**:
- [ ] API provides endpoint listing available report types
- [ ] Each report includes name, description, and example URL
- [ ] User can test API endpoints with sample date ranges
- [ ] Error messages clearly explain what went wrong (invalid dates, unauthorized, etc.)

**Example Workflow**:
1. User queries: `GET /api/tenant/{id}/reports/available`
2. Receives list of reports:
   - Spending by Category (aggregated spending grouped by category)
   - Monthly Spending Trend (spending per month over time)
   - Income vs Expenses (comparison report)
3. User selects "Spending by Category" and builds Excel query

---

## Report Data Format Requirements

### JSON Structure for Excel Compatibility

All report endpoints MUST return **array of objects** format where each object represents one table row:

**✅ REQUIRED Format:**
```json
[
  {
    "category": "Groceries",
    "amount": 1234.56,
    "percentTotal": 23.45,
    "transactionCount": 42
  },
  {
    "category": "Utilities",
    "amount": 456.78,
    "percentTotal": 8.67,
    "transactionCount": 12
  },
  {
    "category": "Entertainment",
    "amount": 789.01,
    "percentTotal": 14.98,
    "transactionCount": 28
  }
]
```

**Why This Format**:
- Each JSON object becomes one Excel row automatically
- Object keys become column headers
- Numbers, dates, and strings are correctly typed
- Excel can immediately sort, filter, pivot, and chart the data
- No complex Power Query transformations required

**❌ PROHIBITED Formats:**
- Nested objects (requires flattening)
- Separate arrays for columns (requires complex transformations)
- Object-of-objects with keys as data (cannot easily convert to rows)
- Wrapper objects containing data arrays (adds unnecessary nesting)

### Data Type Requirements

**Numeric Values:**
- Use `decimal` for currency (amount, balance)
- Use `int` for counts (transactionCount, categoryCount)
- Never use strings for numeric values

**Dates:**
- Use ISO 8601 format: `"2024-01-15"` for dates
- Use ISO 8601 with timezone: `"2024-01-15T10:30:00Z"` for timestamps
- Excel auto-recognizes these formats

**Column Naming:**
- Use camelCase for JSON property names: `percentTotal`, `transactionCount`
- Excel users can rename columns in Power Query if desired
- Keep names concise and descriptive

### Example Report Response

**Request:**
```
GET /api/tenant/abc-123-def/reports/spending/by-category?fromDate=2024-01-01&toDate=2024-12-31
Authorization: Bearer yofi_user_9f8e7d6c5b4a3210
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/json

[
  { "category": "Groceries", "amount": 1234.56, "percentTotal": 23.45, "transactionCount": 42 },
  { "category": "Utilities", "amount": 456.78, "percentTotal": 8.67, "transactionCount": 12 },
  { "category": "Entertainment", "amount": 789.01, "percentTotal": 14.98, "transactionCount": 28 },
  { "category": "Transportation", "amount": 234.56, "percentTotal": 4.45, "transactionCount": 8 },
  { "category": "Healthcare", "amount": 567.89, "percentTotal": 10.78, "transactionCount": 15 }
]
```

---

## Key Business Rules

### Authentication & Authorization

1. **API Key Scoping** - API keys are scoped to the user who created them, not to a specific workspace. A single key works for all workspaces the user can access.

2. **Role-Based Access** - API keys respect workspace role assignments:
   - Viewer role or higher required for all report endpoints
   - Editor role does NOT grant additional API access (reports are read-only)
   - Owner role required to generate/revoke API keys

3. **Key Rotation** - Users can revoke and regenerate keys without affecting main account password. Revoked keys fail immediately with 401 Unauthorized.

4. **Scope Limitation** - API keys grant access ONLY to report endpoints (`/api/tenant/*/reports/*`). They cannot access transaction CRUD endpoints or other write operations.

### Data Access

5. **Workspace Isolation** - Report data is strictly isolated by workspace. Users cannot access data from workspaces they don't belong to, even with valid API key. Returns 403 Forbidden.

6. **Date Range Filtering** - Reports require date range parameters (fromDate, toDate). No default "all time" queries to prevent accidentally loading large datasets in Excel.

7. **No Personal Data in Reports** - Aggregated reports do not expose personally identifiable information (PII) like email addresses or usernames. Only financial data and category names.

### Excel Integration

8. **Stable URLs** - Report endpoint URLs and parameters must remain stable. Breaking changes require deprecation period and API versioning.

9. **Response Performance** - Report endpoints should respond within 5 seconds for typical date ranges (1 year of data). Larger ranges may require pagination in future.

10. **Error Clarity** - All errors return standard ProblemDetails JSON with clear messages suitable for Excel users (not just developers).

---

## Success Metrics

**Adoption Metrics:**
- Number of users who generate API keys (target: 10% of active users within 3 months)
- Number of users who make API requests per month (indicates active Excel usage): 10%
- Number of workspaces accessed via API: 10%

**Quality Metrics:**
- API endpoint response time (target: < 2 seconds for P95)
- API error rate (target: < 1% excluding user errors like 401/403)
- No security incidents related to compromised API keys

**User Satisfaction:**
- Positive feedback on Excel integration from beta users
- Support tickets related to API access (target: < 5% of total tickets)

---

## Open Questions

- [x] ~~Should API keys be scoped to user or tenant?~~ **RESOLVED**: User-scoped for better UX (one key for all workspaces)
- [x] ~~What reports should be available in Phase 1?~~ **RESOLVED**: Any reports available in the web UI are automatically available via API (no separate list needed)
- [x] ~~Should we provide Excel template files with pre-configured queries users can download?~~ **RESOLVED**: Yes, good idea - add as separate user story or Phase 2 enhancement
- [x] ~~What rate limits should we apply to prevent abuse?~~ **RESOLVED**: 100 requests/minute, 1000 requests/hour per API key
- [x] ~~Should API keys expire automatically after 1 year, or only on manual revocation?~~ **RESOLVED**: Manual revocation only (keep it simple for Phase 1)

---

## Dependencies & Constraints

**Dependencies:**
- **Reports Feature (PREREQUISITE)** - This Export API feature depends on the Reports feature being implemented FIRST
  - See [`PRD-REPORTS.md`](PRD-REPORTS.md) for the prerequisite Reports feature specification
  - Reports feature will provide the aggregation logic (spending by category, monthly spending, etc.)
  - Reports feature will expose `ReportsFeature` class with methods for generating report data
  - This Export API simply wraps those existing report methods with API key authentication
  - **Blocker:** Cannot implement Export API until Reports feature is complete
- Existing multi-tenant authorization system ([`TenantRoleHandler`](../../src/Controllers/Tenancy/Authorization/TenantRoleHandler.cs)) - Already implemented for tenant-scoped access control

**Constraints:**
- Excel Power Query limitations (only Bearer token auth, no complex OAuth flows)
- Response size limits (Excel struggles with 100K+ rows, may need pagination)
- No offline support (Excel requires internet connection to refresh data)
- Performance impact: Report queries hit production database (consider caching or read replicas)

**Technical Constraints:**
- Must work with Excel 2016+ (desktop) and Microsoft 365 (desktop)
- Excel desktop: No CORS issues (native HTTP client, not browser-based)
- Excel Web (browser-based): **OUT OF SCOPE** - Power Query not fully supported in Excel Web, users must use desktop Excel
- API responses should be < 32 MB (Excel desktop query size practical limit)

---

## Implementation Scope

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - API key management UI (create, list, revoke keys)
- [x] Controllers (API endpoints) - ReportsController (report data), ApiKeysController (key management)
- [x] Application (Features/Business logic) - ReportsFeature (report generation), ApiKeyFeature (key CRUD), ApiKeyService (validation/hashing)
- [x] Entities (Domain models) - ApiKey entity, ApiKeyScope enum, validation result types
- [x] Database (Schema changes) - ApiKeys table with indexes

---

## Notes & Context

**Why Excel vs Other Export Formats:**
Excel is the dominant tool for financial analysis among YoFi's target users (small business owners, household finance managers). While CSV export is simpler technically, it lacks:
- Auto-refresh capabilities
- Direct connection to live data
- Type-safe data handling
- Built-in analytics (pivots, charts)

**Competitive Analysis:**
- Mint: No API access (data locked in app)
- YNAB: API available but requires OAuth, complex for Excel users
- Personal Capital: Dashboard-only, no data export
- Opportunity: YoFi can differentiate with easy Excel integration

**Related Documents:**
- [`PRD-REPORTS.md`](PRD-REPORTS.md) - **PREREQUISITE**: Reports feature that must be implemented first
- [`EXCEL-INTEGRATION-ARCHITECTURE.md`](EXCEL-INTEGRATION-ARCHITECTURE.md) - Detailed technical design (API key format, authentication flow, implementation details)

---

## Handoff Checklist - All Items Complete ✅

- [x] **Document stays within PRD scope** - Focuses on WHAT/WHY, technical details delegated to architecture document
- [x] **All user stories have clear acceptance criteria** - 4 stories with specific, testable criteria
- [x] **Open questions are resolved** - All 5 questions answered with clear decisions
- [x] **Report data format requirements explicit** - Array-of-objects format specified with examples and prohibited formats
- [x] **Business rules comprehensive** - 10 rules covering authentication, authorization, and data access
- [x] **Success metrics measurable** - Adoption, quality, and satisfaction metrics with targets
- [x] **Dependencies and constraints identified** - Reports feature prerequisite clearly documented; Excel limitations noted
- [x] **Related architecture document** - Links to both Reports PRD (prerequisite) and Excel Integration Architecture (technical details)
- [x] **Prerequisite dependency documented** - Reports feature dependency highlighted with link to [`PRD-REPORTS.md`](docs/wip/reports/PRD-REPORTS.md)

## Document Status

The PRD is **ready for approval** with:
- ✅ Clear problem statement and user stories
- ✅ Explicit data format requirements for Excel compatibility
- ✅ All open questions resolved
- ✅ Dependencies and constraints documented
- ✅ Architecture document provides implementation guidance

Ready to hand off to implementation team once Reports feature is complete.
