Feature: Bank Import
    Users can upload OFX/QFX bank files and review/import transactions
    into their workspace. The system detects duplicates and allows selective import.

Background:
    Given the application is running
    And I have an existing account
    And I have an active workspace "My Finances"
    And I am logged into my existing account

Scenario: User uploads bank file and sees import review page
    Given I have existing transactions with external IDs:
        | ExternalId     | Date       | Payee            | Amount    |
        | 2024010701     | 2024-01-07 | Gas Station      | -89.99    |
        | 2024011201     | 2024-01-12 | Online Store     | -199.99   |
        | 2024012201     | 2024-01-22 | Rent Payment     | -1200.00  |
    And I am on the import review page
    When I upload OFX file "checking-jan-2024.ofx"
    Then page should display 15 transactions
    And 12 transactions should be selected by default
    And 3 transactions should be deselected by default

@explicit:wip
Scenario: User accepts selected transactions from import review
    Given There are 15 transactions ready for import review, with 12 selected
    And I am on the import review page
    When I import the selected transactions
    Then I should be redirected to transactions page
    And I should see 12 new transactions in the transaction list
    And import review queue should be completely cleared
