# Tenancy Feature Review

## Overview
This document provides a comprehensive review of [`Tenancy.feature`](Tenancy.feature:1) for completeness and clarity against the design specifications in [`0009-accounts-and-tenancy.md`](../../../docs/adr/0009-accounts-and-tenancy.md:1) and [`TENANCY-DESIGN.md`](../../../docs/TENANCY-DESIGN.md:1).

**Review Date**: 2025-11-22
**Reviewer**: Architect Mode Analysis

---

## Executive Summary

### ✅ Strengths
- **Excellent coverage** of core multi-tenancy scenarios
- **Well-structured** using Gherkin Rules for logical grouping
- **Comprehensive role-based permissions** testing
- **User-centric language** consistently uses "workspace" terminology as specified in ADR
- **Good scenario diversity** covering happy paths and edge cases

### ⚠️ Areas for Improvement
1. **Missing first-time user flow** (auto-workspace creation)
2. **Incomplete invitation workflow** (pending/rejected states)
3. **Limited error handling scenarios**
4. **Missing API-level authorization tests**
5. **Terminology inconsistency** with design documents

---

## Detailed Analysis

### 1. Terminology Consistency

#### ✅ Correct Usage
The feature file correctly uses **"workspace"** terminology for user-facing scenarios, aligned with ADR 0009's UI layer specification.

#### ⚠️ Inconsistency Found
- **Design documents use**: `Account` / `UserAccountAccess` / `AccountRole`
- **Feature file uses**: "workspace" (correct for UI)
- **Missing clarification**: The baseclass `WorkspaceTenancySteps` should be documented

**Recommendation**: Add a comment in the feature file clarifying that "workspace" is the UI term for "tenant/account" in the implementation.

```gherkin
# Note: "Workspace" is the user-facing term for "Tenant/Account" in the implementation layer
# See docs/adr/0009-accounts-and-tenancy.md for terminology rationale
```

---

### 2. Coverage Gaps

#### 🔴 Critical Gaps

##### A. First-Time User Experience (ADR Phase 1)
**Design Requirement** (ADR line 151):
> **New User Flow**: Auto-create personal tenant → redirect to dashboard

**Missing Scenario**:
```gherkin
Scenario: New user automatically gets a personal workspace
    Given I have just registered a new account
    When I complete registration successfully
    Then a personal workspace should be automatically created for me
    And it should be named after my username or email
    And it should be set as my default workspace
    And I should be redirected to the dashboard
```

##### B. Invitation Workflow States
**Design Gap**: Only covers accepted invitations, missing:
- Pending invitation state
- Declined/rejected invitations
- Canceling sent invitations

**Missing Scenarios**:
```gherkin
Scenario: User views pending invitations
    Given I am logged in as "charlie"
    And I have a pending invitation to "Family Budget"
    When I view my invitations
    Then I should see the invitation from "alice"
    And I should have options to accept or decline

Scenario: User declines workspace invitation
    Given I am logged in as "charlie"
    And I have a pending invitation to "Family Budget"
    When I decline the invitation
    Then the invitation should be marked as declined
    And I should not have access to "Family Budget"
    And "alice" should be notified of the decline

Scenario: Owner cancels pending invitation
    Given I am logged in as "alice"
    And I invited "charlie" to "Family Budget"
    But "charlie" has not responded yet
    When I cancel the invitation
    Then "charlie" should no longer see the invitation
    And I should be able to send a new invitation if needed
```

##### C. Authorization/Security Scenarios
**Design Requirement** (ADR lines 89-110):
> API Structure: `/api/tenant/{tenantId}/transactions`
> Policies: TenantView, TenantEdit, TenantOwn

**Missing Scenarios**:
```gherkin
Rule: API Authorization Enforcement
    The system prevents unauthorized API access to workspace data

    Scenario: API rejects request with invalid workspace ID
        Given I am logged in as "bob"
        And I have editor access to "Family Budget"
        When I attempt to access transactions via API with an invalid workspace ID
        Then the API should return a 404 Not Found error
        And I should not receive any financial data

    Scenario: API enforces role-based permissions
        Given I am logged in as "charlie"
        And I have viewer access to "Family Budget"
        When I attempt to POST a new transaction via API
        Then the API should return a 403 Forbidden error
        And the transaction should not be created
```

##### D. Edge Cases and Constraints

**Missing from ADR rules** (line 65):
> Each tenant must have at least one Owner

**Missing Scenario**:
```gherkin
Scenario: Cannot remove last owner from workspace
    Given I am logged in as "alice"
    And I own a workspace named "Family Budget"
    And I am the only owner of "Family Budget"
    When I attempt to transfer ownership without accepting a role myself
    Then I should see an error message
    And the transfer should be prevented
    And I should remain the owner

Scenario: Cannot delete workspace with active users without confirmation
    Given I am logged in as "alice"
    And I own a workspace named "Family Budget"
    And "bob" and "charlie" have access to "Family Budget"
    When I request to delete "Family Budget"
    Then I should be warned that 2 other users will lose access
    And I should need to acknowledge this before proceeding
```

#### 🟡 Moderate Gaps

##### E. User Preferences Integration
**Design Requirement** (TENANCY-DESIGN.md lines 246-253):
> UserPreferences with DefaultAccountId

**Missing Scenario**:
```gherkin
Scenario: User preferences persist across sessions
    Given I am logged in as "bob"
    And I have access to multiple workspaces
    And I set "Family Budget" as my default workspace
    When I log out and log back in
    Then "Family Budget" should be automatically selected
    And I should see "Family Budget" data immediately
```

##### F. Data Isolation Verification
**Critical Security Requirement** (ADR line 66):
> Financial data is completely isolated by tenant

**Missing Scenario**:
```gherkin
Scenario: Workspace data is completely isolated
    Given I am logged in as "alice"
    And I own "Alice's Personal" with 10 transactions
    And I have viewer access to "Family Budget" with 20 transactions
    When I switch to "Family Budget"
    Then I should only see the 20 transactions from "Family Budget"
    And I should not see any transactions from "Alice's Personal"
    And API calls should only return "Family Budget" data
```

##### G. Workspace Naming and Validation

**Missing Scenarios**:
```gherkin
Scenario: Workspace name must be unique per user
    Given I am logged in as "alice"
    And I own a workspace named "Family Budget"
    When I try to create another workspace named "Family Budget"
    Then I should see an error message
    And the duplicate workspace should not be created

Scenario: Workspace name has reasonable length limits
    Given I am logged in as "alice"
    When I try to create a workspace with a name longer than 200 characters
    Then I should see a validation error
    And the workspace should not be created
```

---

### 3. Scenario Quality Assessment

#### ✅ Well-Written Scenarios

**Example**: [Scenario: Editor can modify financial data but cannot manage users](Tenancy.feature:42)
- Clear role-based permission boundaries
- Multiple assertions grouped logically
- Tests both positive (can do) and negative (cannot do) capabilities

**Example**: [Scenario: User switches between multiple workspaces](Tenancy.feature:67)
- Uses data tables effectively
- Tests context switching clearly
- Validates role display

#### ⚠️ Scenarios Needing Enhancement

**Line 118-124**: [Scenario: Invitation expires after reasonable time](Tenancy.feature:118)
```gherkin
Scenario: Invitation expires after reasonable time
    Given I am logged in as "alice"
    And I invited "charlie" to access "Family Budget"
    But "charlie" has not responded for 30 days
    When the invitation expires
    Then "charlie" should no longer be able to accept the invitation
    And I should be able to send a new invitation if needed
```

**Issues**:
1. "30 days" is hardcoded - should reference configuration
2. "When the invitation expires" is vague - how is expiration triggered?
3. Missing notification to inviter about expiration

**Improved Version**:
```gherkin
Scenario: Invitation expires after configured timeout period
    Given the invitation timeout is configured to 30 days
    And I am logged in as "alice"
    And I invited "charlie" to access "Family Budget" 31 days ago
    When the system processes invitation expiration
    Then the invitation should be marked as expired
    And "charlie" should see "invitation has expired" when attempting to accept
    And I should be notified that the invitation expired
    And I should be able to send a new invitation to "charlie"
```

---

### 4. Missing Design Elements

#### A. Transaction Source Tracking
**ADR Requirement** (lines 112-118):
> Each transaction includes a `Source` field

**Not Tested**: The feature file doesn't verify that workspace transactions maintain source tracking.

#### B. UserPreferences Table
**TENANCY-DESIGN Requirement** (lines 244-254):
> Separate UserPreferences table with Theme, DefaultAccountId

**Not Tested**: Theme preferences or other user-level settings.

#### C. JWT Claims Structure
**ADR Requirement** (lines 91-97):
> JWT includes `entitlements` claim with tenant roles

**Not Tested**: Token-based authorization scenarios.

---

### 5. Structural Recommendations

#### A. Add Cross-References to Design Docs

Add at the top of [`Tenancy.feature`](Tenancy.feature:1):
```gherkin
# Design References:
# - ADR 0009: docs/adr/0009-accounts-and-tenancy.md
# - Technical Design: docs/TENANCY-DESIGN.md
# - Database Schema: docs/TENANCY-DESIGN.md (lines 66-137)
```

#### B. Add Rule for Data Isolation

```gherkin
Rule: Data Isolation and Security
    Financial data is completely isolated by workspace and enforced at API level

    Scenario: [Add scenarios from section 2.D above]
```

#### C. Consider Phase-Based Organization

The ADR mentions implementation phases. Consider organizing scenarios:

```gherkin
@phase1
Scenario: User creates their first personal workspace
    # Phase 1 (MVP): Single-user workspaces

@phase2
Scenario: Workspace owner invites family member with editor access
    # Phase 2: Multi-user tenants, invitation system

@phase3
Scenario: Workspace owner transfers ownership
    # Phase 3: Advanced features
```

---

## Priority Recommendations

### 🔴 High Priority (Must Have)

1. **Add first-time user auto-workspace creation scenario** (ADR requirement)
2. **Add pending/declined invitation scenarios** (incomplete workflow)
3. **Add data isolation verification scenarios** (security critical)
4. **Add "cannot remove last owner" constraint scenario** (data integrity)
5. **Add API authorization enforcement scenarios** (security critical)

### 🟡 Medium Priority (Should Have)

6. **Add workspace naming validation scenarios**
7. **Add user preferences persistence scenarios**
8. **Add invitation cancellation scenario**
9. **Improve invitation expiration scenario** (clarity)
10. **Add workspace deletion with active users warning**

### 🟢 Low Priority (Nice to Have)

11. **Add phase tags for implementation tracking**
12. **Add design document cross-references**
13. **Add JWT claims verification scenarios**
14. **Add transaction source tracking verification**

---

## Comparison with Other Features

### Pattern Consistency

Compared to [`Authentication.feature`](Authentication.feature:1):
- ✅ Both use Rules for logical grouping
- ✅ Both have comprehensive error scenarios
- ✅ Both test access control
- ⚠️ Authentication has more validation edge cases

### Suggested Pattern Alignment

Add validation scenarios similar to Authentication's approach:
```gherkin
Scenario: Workspace creation fails with invalid name
    Given I am logged in as "alice"
    When I try to create a workspace with an empty name
    Then I should see a validation error
    And the workspace should not be created
```

---

## Conclusion

The [`Tenancy.feature`](Tenancy.feature:1) file provides **strong foundational coverage** of workspace tenancy scenarios with **well-structured Rules** and **user-centric language**. However, it requires additions to achieve **complete coverage** of the design specifications.

### Overall Assessment
- **Completeness**: 75% ✅
- **Clarity**: 90% ✅
- **Alignment with Design**: 80% ✅

### Next Steps
1. Review and prioritize the missing scenarios listed above
2. Add high-priority scenarios (first-time user flow, data isolation, API authorization)
3. Enhance existing scenarios (invitation expiration clarity)
4. Consider adding phase tags for implementation tracking
5. Update after implementing to verify actual behavior matches specifications

---

## Appendix: Design Coverage Matrix

| Design Requirement | Feature Coverage | Status |
|-------------------|------------------|--------|
| Auto-create workspace on registration | ❌ Missing | Critical Gap |
| Role-based permissions (Owner/Editor/Viewer) | ✅ Covered | Complete |
| Multi-workspace per user | ✅ Covered | Complete |
| Workspace switching | ✅ Covered | Complete |
| Invitation workflow | 🟡 Partial | Missing states |
| Default workspace preference | ✅ Covered | Complete |
| Workspace deletion | ✅ Covered | Complete |
| User removal from workspace | ✅ Covered | Complete |
| Ownership transfer | ✅ Covered | Complete |
| Access control enforcement | 🟡 Partial | Missing API tests |
| Data isolation | ❌ Missing | Critical Gap |
| At least one owner constraint | ❌ Missing | Important Gap |
| UserPreferences integration | 🟡 Partial | Theme missing |
| Transaction source tracking | ❌ Missing | Design element |
| JWT entitlements | ❌ Missing | Implementation detail |

**Legend**: ✅ Complete | 🟡 Partial | ❌ Missing
