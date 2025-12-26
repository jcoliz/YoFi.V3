@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:TransactionRecordSteps
@template:Features/FunctionalTest.mustache
Feature: Transaction Record Fields
    As a user managing transactions
    I want to record additional details about each transaction
    So that I can track memo notes, source accounts, and external identifiers

Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Rule: Quick Edit Modal
    The quick edit modal should only show Payee and Memo fields for rapid updates

    Scenario: Quick edit modal shows only Payee and Memo fields
        Given I have a workspace with a transaction:
            | Field  | Value           |
            | Payee  | Coffee Shop     |
            | Amount | 5.50            |
            | Memo   | Morning coffee  |
        When I click the "Edit" button on the transaction
        Then I should see a modal titled "Quick Edit Transaction"
        And I should only see fields for "Payee" and "Memo"
        And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
