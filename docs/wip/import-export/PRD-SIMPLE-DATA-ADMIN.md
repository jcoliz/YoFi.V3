---
status: In Review # Draft | In Review | Approved | Implemented
target_release: Alpha 1
references:
    - PRD-TENANT-DATA-ADMIN.md
    - docs\wip\payee-rules\PRD-PAYEE-RULES.md
    - [Link to design document]
ado: [Link to ADO Item]
---

# Product Requirements Document: Simple Data Administration

## Problem Statement

During the development process, the development team needs a simple way to import sample data for testing, without implementing the entire tenant data admin flow.

---

## Goals & Non-Goals

### Goals
- [ ] Clear workspace data sets
- [ ] Upload/Download workspace data sets in simple form
- [ ] Include payee matching rules
- [ ] Include transactions
- [ ] Will grow into the full tenant data admin functionality in the future

### Non-Goals
- Not designed to be an end-user facing feature in current form. Designed for developers.
- Not including other data sets beyond those listed above. Will add more data sets when starting work on those areas.

---

## User Stories

### Story 1: Workspace Owner - Manages payee matching rules in a simple way
**As a** workspace owner
**I want** quick access to large changes in my supported domains
**So that** I can rapidly iterate on feature development

**Supported Domains**:
- Payee matching rules
- Transactions

**Acceptance Criteria**:
- [ ] Owner can clear the entire data set for a chosen domain in a specific workspace, with confirmation
- [ ] Owner cannot recover data once deleted
- [ ] Owner can download entire database for a chosen domain in a specific workspace
- [ ] Owner can upload a YAML file containing items for a chosen domain, which will be immediately placed into the database for the specific workspace
- [ ] When domain data, if new item matches existing item on Key, it is replaced
- [ ] When domain data, if there is no Key or no Key match, it is added.

---

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [X] Frontend (Vue/Nuxt)
- [X] Controllers (API endpoints) - Dedicated controller for data administration. Will grow into the full data admin later.
- [X] Application (Features/Business logic) - Individual entity types expose functionality in their domain
- [ ] Entities (Domain models)
- [ ] Database (Schema changes)

**Application Layer**: Create a new feature class at src/Application/Features/TenantDataAdminFeature.cs that provides all bulk data administration operations across multiple entity types. This maintains clean separation of concerns, and provides a clear path to the full tenant data admin feature.

**Frontend**: Tenant data administration will be handled in a single page.
- Workspace selector
- Rows for each supported data type, showing
  - Data type name (e.g. "Transactions")
  - Item count
  - Download button
  - Delete button
- File picker upload control
  - Allows multi-select of files
  - Can import multiple data types at once, or multiple files of a single data type

**Controllers**
Single Admin controller: DataAdminController

- GET /api/tenant/{tenantId}/admin: Retrieves item counts for all domains
- GET /api/tenant/{tenantId}/admin/{data-type}/yaml: Download yaml file for this data type
- DELETE /api/tenant/{tenantId}/admin/{data-type}: Clear all of this data type
- POST /api/tenant/{tenantId}/admin/upload: Upload one or more files containint one or more data types

**Key Business Rules**:
1. **Requires Owner Role** - Workspace owner role enforced on any endpoint, and on frontend. This uses the existing required tenant user role system.
2. **YAML** - For now, all data import will be YAML for current and future entities.
3. **YAML Format** - Contains all entity fields
4. **All enrivonments** - This will be used for testing in all environments.

**Update/Replace Semantics**
All data types can be uploaded in a single file, or in multiple files to the same endpoint. This should work like OFX upload in Import page.

- If key exists and matches, replace entire record
- If key exists and doesn't match, treat as missing key
- If key is missing, create a new key on insert
- Upload doesn't affect any other records

**YAML**
- Include a root level wrapper by data type, so a single file can contain multiple data types, e.g.

```yaml
transactions:
  - key: "guid-here"
    date: "2024-01-15"
    payee: "Store Name"
    amount: 123.45
```

- For simplicity, no special validation is performed on entries. If the YAML fails to parse into a data type, we'll skip that item, and report failures back to the user.

**File/performance limits**
- No limits will be enforced. This is used primarily by developers.

**Code Patterns to Follow**:
- Application Features for entitity manipulation
- All data access is tenant scoped
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

**Multi-file Upload Behavior

When uploading multiple files simultaneously, should they be processed as a single transaction (all-or-nothing) or independently?
If one file fails to parse, should the entire upload batch be rejected, or should valid files still be processed?
What happens when multiple files contain the same key?
---

## Open Questions

### 1. Multi-file Upload Behavior

**Question**: When uploading multiple files simultaneously, what is the expected transactional behavior?
**Answer**: Process independently - Each file succeeds or fails independently

**Follow-up questions**:
- If one file fails to parse, should the entire upload batch be rejected? **A** NO
- What happens when multiple files contain the same key? (Last-write-wins, first-write-wins, error?) **A** Indeterminate. Whatever is easier to implement.\


### 2. Error Reporting Format

**Question**: What format should error reports take when YAML fails to parse or items are skipped?
**Answer**: Detailed per-file - Include file name, line numbers, specific errors

**Follow-up questions**:
- Should partial success (some items succeeded, some failed) be reported differently from complete failure? **A** Yes. Even one imported item is success.
- Should we return HTTP 200 with error details, or HTTP 207 Multi-Status, or HTTP 400 on any failure? **A** 200 with error details, unless everything fails then 400.

### 3. Data Type Identifier in URL

**Question**: What are the exact string values for the `{data-type}` URL parameter?

**Context**: The PRD uses `{data-type}` in endpoints like:
- `GET /api/tenant/{tenantId}/admin/{data-type}/yaml`
- `DELETE /api/tenant/{tenantId}/admin/{data-type}`

**Answer**: Kebab-case - `"transactions"`, `"payee-rules"`

**Follow-up questions**:
- Should this match the YAML root key exactly? **A** Yes
- Is this case-sensitive? **A** Case inssentive preferred, not required
- Should we support aliases (e.g., both "transactions" and "transaction")? **A** NO

### 4. YAML Structure for Payee Matching Rules

**Question**: What's the exact YAML structure expected for payee matching rules?

**Context**: The PRD shows an example for transactions but not for payee matching rules. Need to know:
- What fields should be included/excluded? **A** Include everything but the bigint ID
- Should it match the existing DTOs exactly or have a simplified format? **A** For starters, let's use TransactionEditDto plus Key and PayeeMatchingRuleEditDto plus key
- How should we represent the priority/ordering? **A** There is no priority or ordering

**Example needed**:
```yaml
payee-rules:
  - key: "guid-here"
    # All PayeeMatchingRuleEditDto fields go here
```

### 5. File Type Detection in Multi-Type Upload

**Question**: How does the `POST /api/tenant/{tenantId}/admin/upload` endpoint determine which data types are in the uploaded file(s)?

**Context**: The PRD states "a single file can contain multiple data types" with root-level wrappers like:
```yaml
transactions:
  - ...
payee-rules:
  - ...
```

**Answer**: Auto-detect from YAML root keys - Parse file, process all recognized root keys

**Follow-up questions**:
- Can you mix single-type and multi-type files in the same upload batch? **A** Yes
- What happens if a file has multiple root keys but only one is valid? **A** Process the valid one, return an error for the others


### 6. Owner Role Authorization

**Question**: What specific role name should be checked for authorization, and how should it be enforced?

**Answer**: This is not hard. There is an "Owner" role in the tenancy system. Just ensure user a role assignment to the tenant with that role. We do this everywhere for other roles.

### 7. Frontend Navigation & Access

**Question**: Where in the application should the data admin page be accessible?

**Questions to answer**:
- What should the route be? **A** `/data-admin`.
- Should it be in the main navigation menu or hidden/developer-only? **A** Add to main menu for now
- Should non-owners see the page at all, or get redirected? **A** We should follow data import which shows an access denied error. Note that user could swtich workspace, and now they DO have access.
- Should there be an environment flag to show/hide this feature? (e.g., only in development/staging?) **A** No. Tenant data admin will be a production feature in the future.

### 8. Item Count Display

**Question**: How should item counts be retrieved and displayed for each data type?
**Answer**: Real-time database query - Fetch count on page load. We'll need a single endpoint to fetch all counts.

**Follow-up**: Should counts update automatically after upload/delete operations? **A** Yes, we should re-fetch the counts.

### 9. Delete Confirmation UX

**Question**: What should the delete confirmation dialog say, and how strict should confirmation be?
**Answer**: Simple confirmation - "Are you sure you want to delete all X transactions?"

### 10. Download File Naming

**Question**: What should downloaded YAML files be named?

**Options**:
**Answer**: `YoFi-{workspace-name}-{data-type}-{timestamp}.yaml`

**Follow-up**: Should downloads be single-type or multi-type YAML? **A** Single type

---

## Dependencies & Constraints

**Dependencies**:
- Payee Matching Rules - Assumes entitites and application features, at least, are implemented
- Tenant Data admin - Conceptual dependency to align, as the full tenant data admin is the future

**Constraints**:
- [Technical, time, or resource constraints]

---

## Notes & Context

[Any additional context, links to related documents, or background information]

**Related Documents**:
- [Link to related PRDs]
- [Link to analysis documents]

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
