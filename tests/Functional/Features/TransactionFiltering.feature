Feature: Transaction Filtering
  As a YoFi user
  I want to search my transactions with one text input
  So that I can quickly find the transactions I care about

  Detailed matching rules (amounts, signs, escaping, etc.) are
  covered by unit tests. These scenarios confirm the end-to-end experience.

  Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

  @category:2213
  Rule: Users can quickly find transactions by searching text

    @pri:1
    @id:1
    Scenario: User searches across payee, memo, and category
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category    | Memo           |
        | Starbucks Coffee | -5.50  | Drinks      |                |
        | Office Depot     | -18.00 | Supplies    | Coffee filters |
        | Supermarket      | -60.00 | Food:Coffee |                |
        | Gas Mart         | -40.00 | Auto:Fuel   |                |
      When I search for transactions matching "coffee"
      Then the following transactions should be shown:
        | Payee            |
        | Starbucks Coffee |
        | Office Depot     |
        | Supermarket      |

    @pri:2
    @id:2
    Scenario: Search with no matches tells the user nothing was found
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category |
        | Starbucks Coffee | -5.50  | Drinks   |
        | Gas Mart         | -40.00 | Auto     |
      When I search for transactions matching "zebra"
      Then I should see that no transactions match my search

    @pri:2
    @id:3
    Scenario: Clearing the search shows all transactions again
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category |
        | Starbucks Coffee | -5.50  | Drinks   |
        | Gas Mart         | -40.00 | Auto     |
        | Supermarket      | -60.00 | Food     |
      And I have searched for transactions matching "coffee"
      When I clear the search
      Then I should see exactly 3 transactions

    @pri:1
    @id:4
    Scenario: Search is retained after viewing transaction details
      # The first transaction must match the search, as it is the row clicked
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category |
        | Starbucks Coffee | -5.50  | Drinks   |
        | Gas Mart         | -40.00 | Auto     |
      And I have searched for transactions matching "coffee"
      When I click on the first seeded transaction
      And I click "Back to Transactions"
      Then I should return to the transaction list
      And the search box should contain "coffee"
      And the following transactions should be shown:
        | Payee            |
        | Starbucks Coffee |
