# Writing Feature Files

Feature files under [`Features/`](../Features/) define the behavior covered by the browser tests.
Write them for a reader who understands the product, not the implementation.

## Scenario Shape

Use `Feature` to describe a user goal, `Rule` to group related behavior, and `Scenario` to describe
one concrete example. A scenario should make its setup, action, and observable result clear:

```gherkin
Feature: Transaction memo
    As a user managing transactions
    I want to update a transaction memo
    So that I can keep useful notes

Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Rule: Editing a transaction
    Scenario: User updates a transaction memo
        Given I have a workspace with a transaction:
            | Field | Value        |
            | Payee | Coffee Shop  |
            | Memo  | Morning brew |
        And I am on the transactions page
        When I quick edit the transaction
        And I change Memo to "Coffee with Alex"
        And I click "Update"
        Then I should see the updated memo in the transaction list
```

Keep the action (`When`) before the assertions (`Then`). Do not start a new action phase after
assertions have begun; split distinct behaviors into separate scenarios.

## Writing Good Scenarios

- Describe outcomes and user intent. Avoid exposing implementation details such as HTTP calls,
  database tables, or how the test creates its data.
- Keep each scenario focused on one behavior and make important expected results explicit.
- Use a Gherkin data table for structured test input. For transaction data, the existing steps take
  `Field`/`Value` tables or a table with explicit external IDs; see [Test Control](TEST-CONTROLS.md).
- Use scenario outlines only when the examples genuinely exercise the same behavior with different
  inputs.
- Prefer existing step phrases. Search the step catalog generated into the test output directory
  after a functional test run, or search [`Steps/`](../Steps/).
- Keep setup in `Background` only when it applies to every scenario in that feature.

## Step Wording and Binding

The Gherkin step text must match a C# step definition's attribute. Parameter placeholders are
captured by the generator and passed to the matching method. Use the same phrase for the same
behavior across features so steps can be reused.

See [writing steps](writing-steps.md) for binding, implementation, and shared state conventions.
