---
status: Draft
owner: James Coliz
target_release: [Release Milestone when predominance of work is expected]
ado: [Link to ADO Item]
---

# Product Requirements Document: [Feature Name]

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
- [What this feature explicitly will NOT do]
- [Scope boundaries]

---

## User Stories

### Story 1: User - Attaches file to transaction
**As a** User
**I want** to retain documentation related to a specific transaction
**So that** I have an easy way to find that documentation later

**Acceptance Criteria**:
- [ ] From transaction detail page, user can upload an attachment
- [ ] While it's uploading user can see a loading state on the page
- [ ] User can delete the attachment
- [ ] User can download the attachment
- [ ] Only one attachment is allowed per transaction. After an attachment is uploaded, the affordance to upload is not available
- [ ] From the transactions list, user can download the attachment, if it exists. (Affordance to download will be the only indicator of attachment existence. It's not important enough to show an strong indicator of attachment existence)
- [ ] User can upload these file types: PDF, PNG, JPG. Future filetypes will be considered based on user feedback.
- [ ] User will see a dialog or toast notifying them in case of failure
- [ ] Attachments are tenant scoped. All users in a tenant can see attachments in that tenant
- [ ] File size limitation will be established during detailed design.

### Story 2: User - Uploads a bulk of documents for later attachment
**As a** User
**I want** to upload a group of documents independently of transactions
**So that** I can retain the document immediately, before the transaction is available in the app (don't want to lose it!)

**Acceptance Criteria**:
- [ ] User can access a dedicated "Pending Attachments" page for the following operations (need a better name!)
- [ ] User can upload multiple attachments at once
- [ ] User can upload same kinds of files as described in story 1
- [ ] While it's uploading user can see a loading state on the page
- [ ] If one upload fails, the remaining continue to process
- [ ] If any upload fails, a details pane is shown with the errors listed per file
- [ ] If an upload fails, user must explicitly upload again through normal flow if desired
- [ ] User can see the existing Pending Attachments
- [ ] User can download any one of the Pending Attachments
- [ ] User can delete any one of them
- [ ] User can select multiple and delete many at once
- [ ] User can immediately see file-name guidance to help the system match attachments to transactions
- [ ] When a pending attachment is shown in the pending attachments list, its matching state is immediately shown (Match or Assign buttons, or nothing). Thus, the matching state is decided whenver the list is fetched. (If this turns out to be a performance problem, we can switch to storing it and periodically updating)
- [ ] If system has a single high-confidence match, user will see a "Match" button on the affected line
- [ ] User can click "Match" button to immediately attach to the matched transaction, and remove from this list. User will see a toast notification of this action.
- [ ] If system has multiple matches, or only a single low-confidence match, user will see an "Assign" button on the affected line. ("Match" and "Assign" buttons are mutually exclusive)
- [ ] User can click "Assign" button to review potential transaction matches for a pending attachment on dedicate page (see Story 4). When user returns, the now-matched attachment is no longer visible.
- [ ] Pending Attachments are tenant scoped. All users in a tenant interact with pending attachments in that tenant, as described above
- [ ] Multi-user race condition: If another user matches the attachment first, an error toast notification is shown

### Story 3: User - Matches an attachment from a transaction
**As a** user
**I want** to match a pending attachment to a transaction from the context of a transaction
**So that** I don't have to dig through the pending attachments when I know exactly which transaction I need

**Acceptance Criteria**:
- [ ] From transaction details page, if there are potential attachment matches, user will see an affordance indicating that potential matches exist
- [ ] If there are no potential matches for the transaction, no matching affordance is shown
- [ ] If there are potential matches for the transaction, the upload control is still available
- [ ] User will not see this affordance if the transaction already has an attachment
- [ ] In case of potential matches, user will see same 'Match' or 'Assign' buttons **using the same confidence logic** as described in Story 2"
- [ ] User can click "Match" button to immediately attach to the matched transaction. User will see a toast notification of this action. Page is updated to show the matched attachment as attached to this transaction.
- [ ] User can click "Assign" button to review potential transaction matches for a pending attachment on dedicate page (see Story 4)
- [ ] Multi-user race condition: If another user matches the attachment first, an error toast notification is shown
- [ ] After successful matching, the potential matches affordance is removed/hidden

**Story 4: User - Reviews and assigns pending attachments to transactions**

**As a** user
**I want** to review potential transaction matches for a pending attachment
**So that** I can confidently assign the attachment to the correct transaction

**Acceptance Criteria**:
- [ ] Page displays the pending attachment details (filename, size, file type)
- [ ] Page shows list of potential matching transactions, ordered by confidence score
- [ ] Each transaction shows key details (date, payee, amount) to aid matching decision
- [ ] User can click "Match" on any transaction to complete the assignment
- [ ] After matching, user is returned to the originating page (Pending Attachments or Transaction Details)
- [ ] User can cancel/go back without matching
- [ ] Page handles "no matches found" state with clear messaging
- [ ] Page clearly indicates which context the user came from (breadcrumb or back button)
- [ ] Multi-user race condition: If another user matches the attachment first, an error toast notification is shown

### Story N: [Actor] - [Action]
**As a** [type of user]
**I want** [to perform some action]
**So that** [I can achieve some goal]

**Acceptance Criteria**:
- [ ] [Specific, testable criterion 1]
- [ ] [Specific, testable criterion 2]


---

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [ ] Frontend (Vue/Nuxt)
- [ ] Controllers (API endpoints)
- [ ] Application (Features/Business logic)
- [ ] Entities (Domain models)
- [ ] Database (Schema changes)

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

- [ ] **Q** What constitutes "high confidence"? This affects UX significantly **A** This will go into technical approach. Goal of this algorithm will be to ensure high accuracy. When "Match" is shown, a very high percentage, user should accept it. When "Assign" is shown, a reasonable %age of the time, user is not just picking the top confidence.

- [ ] **Q** "file-name guidance" undefined: States "User can immediately see file-name guidance" but never explains what this means Needs concrete examples or clarification (e.g., "Use format: YYYYMMDD_PayeeName.pdf") **A** This will be provided as part of the technical approach details for matching.

- [ ] **Q** "User can immediately see file-name guidance" - The Open Questions (line 147) defer this to technical approach, but this wording suggests it's visible to the user Should clarify: Is this guidance shown as instructions/help text on the page, or is it just documentation for technical implementation? **A** Guidance is shown to the user. Before this document is complete, this will be filled in.

## Story 3 Review: User - Matches an attachment from a transaction

### Functional Clarity Questions

**3. Potential Match Display Ambiguity**
- **Line 81**: "user will see an affordance indicating that potential matches exist"
- **Question**: What specific affordance? A badge? A button? An icon with a count?
- **Recommendation**: Specify the visual indicator (e.g., "a badge showing the number of potential matches" or "a 'View Matches' button")

**4. Button Placement Unclear**
- **Lines 83-84**: Where exactly do the "Match" or "Assign" buttons appear on the transaction details page?
- **Question**: Are they:
  - In place of the upload affordance?
  - In a separate "Potential Matches" section?
  - As inline suggestions above the attachment area?
- **Recommendation**: Add clarity about UI placement

### Usability Observations

**10. User Context Advantage**
- **Strength**: Story correctly identifies the key value—user already knows which transaction they want
- **Observation**: This suggests the matching confidence threshold might be LOWER for Story 3 than Story 2, since user context provides additional signal
- **Question**: Should the acceptance criteria note this? Or is it purely implementation detail?

---

## Success Metrics

[How will we know this feature is successful?]
- [Metric 1]
- [Metric 2]

---

## Dependencies & Constraints

**Dependencies**:
- [Other features or systems this depends on]

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

When handing this off for detailed design/implementation:
- [ ] Document stays within PRD scope (WHAT/WHY). If implementation details are needed, they are in a separate Design Document. See [`PRD-GUIDANCE.md`](PRD-GUIDANCE.md).
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Code patterns to follow are referenced (links to similar controllers/features)
