---
status: Draft
owner: James Coliz
target_release: [Release Milestone when predominance of work is expected]
ado: [Link to ADO Item]
---

# Product Requirements Document: Transaction Attachments

## Problem Statement

Users have documentation related to transactions, such as receipts or invoices. It's helpful to store them with the transaction, so they're easier to find later.
Attachments also help in determining the correct categories and splits for a transaction.

---

## Goals & Non-Goals

### Goals
- [ ] Bulk document upload with auto-matching to transactions
- [ ] Filename-based matching patterns
- [ ] Document storage and retrieval

### Non-Goals
- ❌ OCR/text extraction from attachments
- ❌ In-app preview/viewing of attachments
- ❌ Attachment editing/annotation
- ❌ Multiple attachments per transaction (noted in Story 1, should be here too)
- ❌ Sharing attachments outside the tenant

## Functional Stories

### Story 1: Direct Transaction Attachment
**As a** User
**I want** to directly attach a document to a transaction
**So that** I can retain documentation immediately when I know which transaction it belongs to

**Acceptance Criteria**:
- [ ] From transaction detail page "Attachments" subsection, user can upload a file
- [ ] User can download the attachment from transaction details page
- [ ] User can download the attachment from transactions list page (download affordance is the only indicator of attachment existence)
- [ ] User can delete the attachment from transaction details page
- [ ] Upload control is hidden after an attachment is uploaded (only one attachment per transaction)

### Story 2: Bulk Upload to Pending Attachments
**As a** User
**I want** to upload multiple documents before knowing which transactions they belong to
**So that** I don't lose receipts while waiting for bank imports or manual transaction entry

**Acceptance Criteria**:
- [ ] User can access a dedicated "Pending Attachments" page
- [ ] User can upload multiple files simultaneously
- [ ] If one upload fails, the remaining files continue to process
- [ ] If any upload fails, a details pane shows the errors listed per file
- [ ] Failed uploads must be explicitly re-uploaded through normal flow
- [ ] User can view all pending attachments in a list
- [ ] User can download any pending attachment
- [ ] User can delete individual pending attachments
- [ ] User can select multiple pending attachments and delete them at once
- [ ] User can see filename guidance to help the system match attachments to transactions

### Story 3: Automatic Matching Suggestions
**As a** User
**I want** the system to suggest which pending attachments match which transactions
**So that** I can quickly associate receipts without manual searching

**Acceptance Criteria - From Pending Attachments Page**:
- [ ] When viewing pending attachments list, matching state is computed and displayed for each attachment
- [ ] If system finds a single high-confidence match, a "Match" button is shown
- [ ] Clicking "Match" button immediately attaches to the matched transaction, removes from pending list, and shows toast notification
- [ ] If system finds multiple matches OR only a single low-confidence match, an "Assign" button is shown
- [ ] Clicking "Assign" button navigates to the Match Review Page (Story 4)
- [ ] "Match" and "Assign" buttons are mutually exclusive
- [ ] If no matches found, no button is shown

**Acceptance Criteria - From Transaction Details Page**:
- [ ] In "Attachments" subsection, if potential matches exist, additional text and "Match" or "Assign" button are shown
- [ ] Uses same confidence logic as Pending Attachments Page to determine which button to show
- [ ] Matching affordance is not shown if transaction already has an attachment
- [ ] Matching affordance is not shown if no potential matches exist
- [ ] Upload control remains available even when potential matches exist
- [ ] Clicking "Match" button immediately attaches to transaction, shows toast notification, and updates page to show attachment
- [ ] Clicking "Assign" button navigates to Match Review Page (Story 4)
- [ ] After successful matching, the potential matches affordance is removed/hidden

### Story 4: Manual Match Review
**As a** User
**I want** to review and select from multiple potential transaction matches
**So that** I can confidently assign the attachment to the correct transaction

**Acceptance Criteria**:
- [ ] Page displays the pending attachment details (filename, size, file type)
- [ ] Page shows list of potential matching transactions, ordered by match confidence
- [ ] Confidence score is not displayed to user (only affects ordering)
- [ ] Each transaction shows key details to aid decision (date, payee, amount)
- [ ] User can click "Match" on any transaction to complete the assignment
- [ ] After matching, user is returned to the originating page (Pending Attachments Page or Transaction Details Page)
- [ ] User can cancel/go back without matching
- [ ] Page handles "no matches found" state with clear messaging
- [ ] Page clearly indicates which page the user came from (breadcrumb or back button)

---

## System Requirements

### File Handling
- [ ] Supported file types: PDF, PNG, JPG (future file types will be considered based on user feedback)
- [ ] Maximum file size: 7MB
- [ ] Upload progress indication (loading state shown during upload)
- [ ] Toast or dialog notifications shown on upload failure
- [ ] Only one attachment allowed per transaction

### Tenant Scoping & Security
- [ ] All attachments (direct and pending) are tenant-scoped
- [ ] All users in a tenant can view and manage all attachments in that tenant
- [ ] All users in a tenant can view and manage all pending attachments in that tenant

### Race Condition Handling
- [ ] If two users attempt to match the same pending attachment simultaneously, the second user receives a toast error notification
- [ ] No data loss occurs in race condition scenarios
- [ ] User can retry the operation after receiving race condition error

### Matching Algorithm
- [ ] Matching state is computed when pending attachments list is fetched (if performance becomes an issue, will switch to stored state with periodic updates)
- [ ] Algorithm determines "high confidence" vs "low confidence" matches to decide between "Match" and "Assign" buttons
- [ ] Date is required in filename to enable matching
- [ ] Only transactions within ±1 week of attachment date are considered for matching
- [ ] Limits on number of pending attachments will be established based on real-life performance testing
- [ ] Detailed matching algorithm specification deferred to technical design

---

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [ ] Frontend (Vue/Nuxt)
- [ ] Controllers (API endpoints)
- [ ] Application (Features/Business logic)
- [ ] Entities (Domain models)
- [ ] Database (Schema changes)

**Storage Strategy**
- For production: Attachments will be stored in an Azure Store Account blob container
- For container: Attachments will be stored in the container file system. If this causes problems during functional testing, we will mount a local volume
- For development: Attachments will be stored in the local file system.

**High-Level Entity Concepts**:

**[EntityName] Entity** (new or modified):
- PropertyName (description - what it represents, required/optional)
- PropertyName (description)
- PropertyName (description)

[Add more entities as needed]

**Key Business Rules**:
1. **Race condition handling**. If two users attempt to act on attachment at the same time, one of them will get a failure. This is an extraordinarily rare occurence, so needs only the least possible energy applied to it, so as to avoid data loss or significant user pain.
2. **Rule Name** - Description of business rule that affects user experience
3. **Rule Name** - Description of business rule

**Code Patterns to Follow**:
- Entity pattern: [`BaseTenantModel`](../src/Entities/Models/BaseTenantModel.cs) or [`BaseModel`](../src/Entities/Models/BaseModel.cs)
- CRUD operations: [`TransactionsController.cs`](../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

- [ ] **Q** What constitutes "high confidence"? This affects UX significantly **A** This will go into technical approach. Goal of this algorithm will be to ensure high accuracy. When "Match" is shown, a very high percentage, user should accept it. When "Assign" is shown, a reasonable %age of the time, user is not just picking the top confidence.

- [ ] **Q** "file-name guidance" undefined: States "User can immediately see file-name guidance" but never explains what this means Needs concrete examples or clarification (e.g., "Use format: YYYYMMDD_PayeeName.pdf") **A** This will be provided as part of the technical approach details for matching.

- [ ] **Q** "User can immediately see file-name guidance" - The Open Questions (line 147) defer this to technical approach, but this wording suggests it's visible to the user Should clarify: Is this guidance shown as instructions/help text on the page, or is it just documentation for technical implementation? **A** Guidance is shown to the user. Before this document is complete, this will be filled in.

- [ ] **Q** "Only one attachment is allowed per transaction" - but Story 3 (line 83) says "upload control is still available" even when potential matches exist. This seems contradictory: If only one attachment allowed, shouldn't the upload control be disabled after attachment? Or does "upload control is still available" mean you can replace the existing attachment? **A** A potential attachment is not an attachment. It's still perfectly allowed to upload an attachment with a possible match available. If user does this, then the matching UI goes away, because "User will not see this affordance [The matching UI] if the transaction already has an attachment"

- [ ] **Q** "matching state is decided whenever the list is fetched" - Performance concern noted, but: What happens with hundreds of pending attachments and thousands of transactions? **A** Matching algorithm will specify that a date is **required** in the filename to even consider matching. Only transactions considered +/- 1 week of the attachment date. This will reduce the scope. We will put limits on number of attachments based on real-life performance testing.

- [ ] **Q** User Context Advantage **Strength**: Story correctly identifies the key value—user already knows which transaction they want  **Observation**: This suggests the matching confidence threshold might be LOWER for Story 3 than Story 2, since user context provides additional signal **Question**: Should the acceptance criteria note this? Or is it purely implementation detail? **A** Will consider this when specifying matching algorithm.

---

## Success Metrics

[How will we know this feature is successful?]
- [Metric 1]
- [Metric 2]

---

## Dependencies & Constraints

**Dependencies**:
- Dependencies: Azure Storage Account provisioning, file upload library (e.g., multipart form data handling)

**Constraints**:
- Constraints: 7MB file size, PDF/PNG/JPG only, single attachment per transaction

---

## Notes & Context

[Any additional context, links to related documents, or background information]

**Related Documents**:
- [Link to companion Design Document if it exists]
- [Link to related PRDs]

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
