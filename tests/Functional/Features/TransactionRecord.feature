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

    Scenario: User updates Memo via quick edit modal
        Given I have a workspace with a transaction:
            | Field      | Value          |
            | Payee      | Coffee Co      |
            | Amount     | -5.50          |
            | Memo       | Morning latte  |
            | Source     | Chase Checking |
            | ExternalId | CHK-001        |
        When I quick edit the "Coffee Co" transaction
        And I change Memo to "Large latte with extra shot"
        And I click "Update"
        Then the modal should close
        And I should see the updated memo in the transaction list

    Scenario: User navigates from transaction list to details page
        Given I have a workspace with a transaction:
            | Field      | Value          |
            | Payee      | Gas Mart       |
            | Amount     | -40.00         |
            | Memo       | Fuel up        |
            | Source     | Chase Checking |
            | ExternalId | CHK-002        |
        When I click on the transaction row
        Then I should navigate to the transaction details page
        And I should see all the expected transaction fields displayed

    Scenario: User edits all fields on transaction details page
        Given I am viewing the details page for a transaction with:
            | Field      | Value          |
            | Payee      | Gas Mart       |
            | Amount     | -40.00         |
            | Memo       | Fuel up        |
            | Source     | Chase Checking |
            | ExternalId | CHK-002        |
        When I click the "Edit" button
        And I change Source to "Chase Visa"
        And I change ExternalId to "VISA-123"
        And I click "Save"
        Then I should see "Chase Visa" as the Source
        And I should see "VISA-123" as the ExternalId
