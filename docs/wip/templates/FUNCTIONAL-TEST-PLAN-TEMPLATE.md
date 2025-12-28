# Functional Test Plan Template

**Purpose:** Guide Architect mode in creating functional test plans that identify critical UI-dependent workflows worthy of functional testing.

**When to Use:** Step 10.5 of Implementation Workflow

---

## Instructions for Architect Mode

### 1. Review Testing Strategy

Read [`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md) completely before starting:
- Understand when functional tests are appropriate
- Review Decision Framework for test layer selection
- Understand Gherkin language tiers (Tier 1 vs Tier 2)
- Review scenario design principles

**Key Points:**
- Functional tests = 10-15% of total tests
- Focus ONLY on UI-dependent workflows
- Avoid testing API behavior (covered by Controller Integration tests)
- Each scenario = ONE workflow or ONE acceptance criterion

### 2. Locate or Create Test Plan Document

- **Location:** Same directory as PRD (`docs/wip/{feature-area}/`)
- **Filename:** `{FEATURE}-FUNCTIONAL-TEST-PLAN.md`
- **If exists:** Review and update
- **If new:** Create with YAML frontmatter:

```yaml
---
status: Draft
references:
  - PRD-{FEATURE}.md
  - {FEATURE}-DESIGN.md
---
```

### 3. Update PRD YAML Frontmatter

Add link to test plan in PRD references list:

```yaml
references:
  - PRD-{FEATURE}.md
  - {FEATURE}-DESIGN.md
  - {FEATURE}-FUNCTIONAL-TEST-PLAN.md
```

### 4. Identify Critical Scenarios

**Process:**
1. Review PRD acceptance criteria
2. Identify workflows that are TRULY UI-dependent
3. Ask: "What risk do we take by NOT testing this?"
4. Ask: "Is this already covered by Controller Integration tests?"
5. PRIORITIZE scenarios by risk/importance

**Target:** Aim for 3-5 scenarios maximum per feature (10-15% of total tests)

### 5. For Each Scenario: Create Analysis Block

**Structure:**

```markdown
### Scenario N: [Descriptive Title]

**Justification:** [2-3 sentences explaining why this test is critical and why other test layers don't cover it]

**Risk Category:** [Business Logic OR UI Contract]

**Language Tier:** [Tier 1 (Strong BDD) OR Tier 2 (Implementation-Aware)]

**Proposed Gherkin:**
```gherkin
Scenario: [Title matching above]
  Given [precondition]
  When [action]
  Then [expected outcome]
```
```

### 6. Justification Guidelines

**Good Justifications:**
- "Authentication flow failure prevents access to entire app. Controller tests mock auth. Business-critical capability transcending UI."
- "PRD requires quick edit limited to specific fields. Controller tests verify API accepts all fields, not that UI intentionally hides them. Business requirement IS the UI behavior."
- "Registration wizard must show validation errors on EACH step before proceeding. Multi-step UI flow not testable via API."

**Bad Justifications:**
- "Need to verify API returns 404 for invalid ID" ← Controller Integration test
- "Ensure transaction can be created via API" ← Controller Integration test
- "Verify authorization works correctly" ← Controller Integration test
- "Test all edge cases for validation" ← Unit test

### 7. Determine Gherkin Language Tier

**Tier 1 (Strong BDD) - Use When:**
- Risk = Business logic
- Scenario transcends specific UI implementation
- Could be implemented in different UI frameworks
- Focus on WHAT user accomplishes, not HOW

**Tier 1 Example:**
```gherkin
Scenario: User logs into existing account
  Given user has registered account
  When user logs in with valid credentials
  Then user should see their dashboard
```

**Tier 2 (Implementation-Aware) - Use When:**
- Risk = UI contract
- PRD specifies exact UI behavior
- Testing that UI intentionally shows/hides elements
- Business requirement IS the UI implementation

**Tier 2 Example:**
```gherkin
Scenario: Quick edit modal shows only Payee and Memo fields
  Given user is viewing transactions page
  When user clicks "Quick Edit" button on a transaction
  Then modal should display only Payee and Memo input fields
  And modal should NOT display Category or Amount fields
```

### 8. Single-Responsibility Principle

**CRITICAL:** Each scenario tests ONE thing.

**Anti-Pattern (TOO LONG):**
```gherkin
Scenario: Complete transaction workflow
  Given user logged in
  When user creates transaction
  Then transaction appears

  When user edits transaction
  Then changes are saved

  When user deletes transaction
  Then transaction is removed
```

**Best Practice (FOCUSED):**
```gherkin
Rule: Transaction Management

Scenario: User creates new transaction
  Given user is logged in and viewing transactions page
  When user creates transaction with amount $50.00
  Then transaction should appear in list

Scenario: User edits transaction payee
  Given user has existing transaction
  When user edits transaction payee to "New Payee"
  Then transaction should show updated payee

Scenario: User deletes transaction
  Given user has existing transaction
  When user deletes the transaction
  Then transaction should be removed from list
```

### 9. Use Gherkin Rule Keyword

Group related scenarios under a Rule:

```gherkin
Rule: Authentication

Scenario: User logs in with valid credentials
  ...

Scenario: User cannot login with invalid password
  ...

Rule: Transaction Creation

Scenario: User creates transaction with required fields
  ...

Scenario: User sees validation error for missing payee
  ...
```

### 10. Complete Test Plan Structure

```markdown
---
status: Draft
references:
  - PRD-{FEATURE}.md
  - {FEATURE}-DESIGN.md
---

# {Feature} Functional Test Plan

## Overview

[2-3 sentences describing feature and why functional tests are needed]

## Test Strategy

**Scope:** [What workflows are in scope for functional testing]

**Out of Scope:** [What's covered by Controller Integration or Unit tests]

**Target:** [Number] scenarios (10-15% of total test suite)

## Scenarios (Priority Order)

### Scenario 1: [Title]

**Justification:** [Why this test matters, why not covered elsewhere]

**Risk Category:** [Business Logic OR UI Contract]

**Language Tier:** [Tier 1 OR Tier 2]

**Proposed Gherkin:**
```gherkin
Scenario: [Title]
  Given [precondition]
  When [action]
  Then [expected outcome]
```

[Repeat for each scenario]

## Acceptance Criteria Coverage

- [ ] AC-1: [Description] - Covered by Scenario 1
- [ ] AC-2: [Description] - Covered by Controller Integration tests
- [ ] AC-3: [Description] - Covered by Scenario 2

[Map all PRD acceptance criteria to test layer]
```

### 11. Review and Approval Process

1. **Self-review checklist:**
   - [ ] Each scenario tests ONE thing
   - [ ] Justifications are compelling
   - [ ] Risk category stated for each
   - [ ] Language tier stated for each
   - [ ] Gherkin matches stated language tier
   - [ ] NO C# code in plan
   - [ ] Scenarios prioritized by importance

2. **Present to user:**
   - Summarize scope (number of scenarios)
   - Highlight any controversial decisions
   - Request approval

3. **Update status:** Change YAML to `status: Approved` after user approval

---

## Example: Transaction Record Functional Test Plan

```markdown
---
status: Approved
references:
  - PRD-TRANSACTION-RECORD.md
  - TRANSACTION-RECORD-DESIGN.md
---

# Transaction Record Functional Test Plan

## Overview

Transaction Record feature adds Memo, Source, and ExternalId fields. Functional tests verify quick edit modal field visibility and details page edit workflow.

## Test Strategy

**Scope:** UI-specific workflows where PRD specifies exact field visibility

**Out of Scope:**
- API CRUD operations (Controller Integration tests)
- Field validation rules (Unit tests)
- Data persistence (Data Integration tests)

**Target:** 3 scenarios (11% of 27 total tests)

## Scenarios (Priority Order)

### Scenario 1: Quick edit modal shows Payee, Category, and Memo fields

**Justification:** PRD requires quick edit limited to these specific fields for rapid updates. Controller tests verify API accepts all fields, but not that UI intentionally hides Amount/Date/Source/ExternalId. Business requirement IS the UI behavior.

**Risk Category:** UI Contract

**Language Tier:** Tier 2 (Implementation-Aware)

**Proposed Gherkin:**
```gherkin
Scenario: Quick edit modal shows Payee, Category, and Memo fields
  Given user is viewing transactions page with existing transaction
  When user clicks "Quick Edit" button
  Then modal should display Payee input field
  And modal should display Category input field
  And modal should display Memo input field
  And modal should NOT display Amount field
  And modal should NOT display Date field
  And modal should NOT display Source field
  And modal should NOT display ExternalId field
```

### Scenario 2: User edits transaction via quick edit

**Justification:** Verifies quick edit saves changes and reflects in list. Tests end-to-end quick edit workflow including modal interaction and list refresh.

**Risk Category:** UI Contract

**Language Tier:** Tier 2 (Implementation-Aware)

**Proposed Gherkin:**
```gherkin
Scenario: User edits Memo via quick edit and sees it in transaction list
  Given user is viewing transactions page with transaction:
    | Field | Value    |
    | Payee | Starbucks |
    | Memo  | (none)    |
  When user clicks "Quick Edit" button
  And user changes Memo field to "Morning coffee"
  And user clicks "Save"
  Then transaction should show "Morning coffee" in Memo column
```

### Scenario 3: User edits all fields on details page

**Justification:** Verifies full edit workflow including ALL fields (Payee, Amount, Date, Category, Memo, Source, ExternalId). Tests details page edit mode comprehensively.

**Risk Category:** UI Contract

**Language Tier:** Tier 2 (Implementation-Aware)

**Proposed Gherkin:**
```gherkin
Scenario: User edits all fields on transaction details page
  Given user is viewing details page for transaction
  When user clicks "Edit" button
  And user changes all fields
  And user clicks "Save"
  Then all fields should show updated values
```

## Acceptance Criteria Coverage

- [ ] AC-1: Memo field visible in list - Covered by Controller Integration
- [ ] AC-2: Quick edit includes Memo - Covered by Scenario 1 & 2
- [ ] AC-3: Details page shows all fields - Covered by Scenario 3
- [ ] AC-4: Source/ExternalId for imports - Covered by Controller Integration
- [ ] AC-5: Fields validate correctly - Covered by Unit tests
```

---

## Common Mistakes to Avoid

**❌ Too many scenarios:**
- "We need to test EVERY field in EVERY context" → No, Controller Integration covers API behavior

**❌ API testing in functional tests:**
- "Verify transaction POST returns 201" → Controller Integration test

**❌ Multiple When/Then cycles:**
- Scenario with 5 When/Then pairs → Split into 5 scenarios

**❌ Vague justifications:**
- "Important to test this" → WHY? What's the risk?

**❌ Writing C# code:**
- Step definitions, POMs, test control → Wait for Step 10.6 (Implementation Plan)

**❌ Wrong language tier:**
- Tier 1 Gherkin saying "click the blue button" → Tier 2 behavior
- Tier 2 Gherkin for auth flow → Tier 1 behavior

---

## Success Criteria

✅ Plan has 3-5 scenarios (10-15% target)
✅ Each scenario has compelling justification
✅ Risk category stated for each
✅ Language tier stated and matches Gherkin style
✅ Single-responsibility (one thing per scenario)
✅ NO C# code in plan
✅ Scenarios prioritized
✅ User approved (`status: Approved` in YAML)
