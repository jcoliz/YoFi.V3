---
status: Draft # Draft | In Review | Approved | Implemented
target_release: [Release Milestone when predominance of work is expected]
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
- Not designed to be a user facing feature
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

**Application Layer Involvement**: Will make changes as needed to support these operations to the individual data set application feature classes.

**Key Business Rules**:
1. **Requires Owner Role** - Workspace owner role enforced on any endpoint, and on frontend.
2. **YAML** - For now, all data import will be YAML for current and future entities.
3. **YAML Format** - Contains all entity fields
4. **All enrivonments** - This will be used for testing in all environments.

**File/performance limits**
- No limits will be enforced. This is used primarily by developers.

**Code Patterns to Follow**:
- Application Features for entitity manipulation
- All data access is tenant scoped
- Tenant-scoped authorization: Existing pattern with `[RequireTenantRole]`
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

- [ ] Should we ACTUALLY add clear and bulk upload/download into each specific domain feature? Or should I have a Tenant Data Admin feature for all of it?
- [ ] [Question 2 that needs resolution]

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
