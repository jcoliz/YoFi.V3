---
status: Draft
ado: Scenario 2252
parent: Story 2225 [User Can] Split Single Transactions across multiple categories
prd: PRD-TRANSACTION-SPLITS.md
---

# Design

## Acceptance Criteria

- [ ] User can add a single split to an existing single-category transaction, and the transaction will be automatically balanced

## Functional Test

```gherkin
Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Scenario: Add a single split to an existing transaction
    Given I have a workspace with a transaction:
        | Payee          | Amount | Category |
        | Maverick Markets | -100.00 | Fuel   |
    And I am on the transactions detail page for that transaction
    When I add a new split with the following details:
        | Category | Amount |
        | Snacks | -10.00 |
    Then the following splits are displayed:
        | Category | Amount |
        | Snacks | -10.00 |
        | Fuel | -90.00 |
```

