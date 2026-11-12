@category:feature-1983
@category:transaction-filtering
Feature: Transaction Filtering
  As a YoFi user
  I want to search and filter my transactions
  So that I can quickly find the transactions I care about

  Detailed matching rules (amounts, signs, escaping, etc.) are
  covered by unit tests. These scenarios confirm the end-to-end experience.

  Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

  @category:story-2213
  @category:quick-search
  Rule: Users can quickly find transactions by searching text

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

    @id:2
    Scenario: Search with no matches tells the user nothing was found
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category |
        | Starbucks Coffee | -5.50  | Drinks   |
        | Gas Mart         | -40.00 | Auto     |
      When I search for transactions matching "zebra"
      Then I should see that no transactions match my search

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

    @id:4
    Scenario: Search is retained after viewing transaction details
      # The first transaction must match the search, as it is the row clicked
      Given I have a workspace with a transaction:
        | Payee            | Amount | Category |
        | Starbucks Coffee | -5.50  | Drinks   |
        | Gas Mart         | -40.00 | Auto     |
      And I have searched for transactions matching "coffee"
      When I open the first transaction result
      And I click "Back to Transactions"
      Then I should return to the transaction list
      And the search box should contain "coffee"
      And the following transactions should be shown:
        | Payee            |
        | Starbucks Coffee |

  Rule: Users can filter transactions by date

    @category:bug-2271
    Scenario: Bug 2271 Transactions date filter can never pass validation
      Given I have a workspace with the following transactions:
        | Payee            |  Date       |
        | Starbucks Coffee |  2024-06-01 |
        | Gas Mart         |  2024-06-02 |
        | Supermarket      |  2024-06-03 |
        | Veterinarian     |  2024-06-04 |
        | Cinema           |  2024-06-05 |
        | Supermarket      |  2024-06-06 |
      When I filter transactions by:
        | Filter    | Value      |
        | From Date | 2024-06-02 |
        | To Date   | 2024-06-04 |
      Then the following transactions should be shown:
        | Payee    |
        | Gas Mart |
        | Supermarket |
        | Veterinarian |

  @category:story-2216
  @category:advanced-filtering
  @explicit:wip
  Rule: Users can combine advanced transaction filters

    @id:1
    Scenario: Quick search and advanced text and date filters narrow transactions together
      Given I have a workspace with the following transactions:
        | Payee              | Amount | Category        | Memo          | Date       |
        | Cafe Coffee House  | -12.00 | Food:Groceries  | Business trip | 2026-04-15 |
        | Cafe Coffee House  | -12.00 | Food:Groceries  | Personal      | 2026-04-15 |
        | Cafe Coffee House  | -12.00 | Household       | Business trip | 2026-04-15 |
        | Office Coffee Shop | -12.00 | Food:Groceries  | Business trip | 2026-04-15 |
        | Cafe Coffee House  | -12.00 | Food:Groceries  | Business trip | 2026-03-31 |
      When I search for transactions matching "coffee"
      And I filter transactions by:
        | Filter    | Value      |
        | Payee     | Cafe       |
        | Category  | Food       |
        | Memo      | trip       |
        | From Date | 2026-04-01 |
        | To Date   | 2026-04-30 |
      Then the following transactions should be shown:
        | Payee             |
        | Cafe Coffee House |

    @id:2
    Scenario: Uncategorized filter matches a blank category on any split
      Given I have a workspace with a transaction for payee "Mixed groceries" having these splits:
        | Amount | Category |
        | -15.00 | Food     |
        | -5.00  |          |
      And I have a workspace with a transaction for payee "Categorized grocery" having these splits:
        | Amount | Category  |
        | -20.00 | Food      |
        | -10.00 | Household |
      When I filter transactions to show uncategorized only
      Then the following transactions should be shown:
        | Payee           |
        | Mixed groceries |

    @id:3
    Scenario: Amount filter combines with the Last 30d date preset
      Given I have a workspace with the following transactions:
        | Payee              | Amount | Category | Days Ago |
        | Positive today       | 42.00  | Income   | 0 |
        | Negative yesterday   | -42.00 | Refunds  | 1 |
        | Positive 29 days ago | 42.00  | Income   | 29 |
        | Positive 30 days ago | 42.00  | Income   | 30 |
        | Positive 31 days ago | 42.00  | Income   | 31 |
      When I filter transactions by:
        | Filter | Value |
        | Amount | +42   |
      And I choose the "Last 30d" date filter preset
      Then the following transactions should be shown:
        | Payee                |
        | Positive today       |
        | Positive 29 days ago |

    @id:4
    Scenario: Clear All removes search and advanced criteria while keeping the panel open
      Given I have a workspace with the following transactions:
        | Payee             | Amount | Category | Memo          | Date       |
        | Cafe Coffee House | -12.00 | Food     | Business trip | 2026-04-15 |
        | Office Depot      | -18.00 | Supplies |              | 2026-04-16 |
        | Grocery Store     | -30.00 | Food     |              | 2026-04-17 |
      And my transaction filters are applied:
        | Filter    | Value      |
        | Search    | coffee     |
        | Payee     | Cafe       |
        | Category  | Food       |
        | Memo      | trip       |
        | From Date | 2026-04-01 |
        | To Date   | 2026-04-30 |
      When I clear the transaction filters
      Then I should see exactly 3 transactions
      And the transaction filter panel should remain open
      And the search and advanced filter fields should be empty

    @id:5
    Scenario: Applied filters and panel expansion are retained after navigating away
      Given I have a workspace with the following transactions:
        | Payee             | Amount | Category | Memo          |
        | Cafe Coffee House | -12.00 | Food     | Business trip |
        | Office Depot      | -18.00 | Supplies |              |
      And my transaction filters are applied:
        | Filter   | Value  |
        | Search   | coffee |
        | Payee    | Cafe   |
        | Category | Food   |
        | Memo     | trip   |
      And the transaction filter panel is expanded
      When I open the first transaction result
      And I return to the transaction list
      Then the search and advanced filter fields should retain their applied values
      And the transaction filter panel should remain open
      And the following transactions should be shown:
        | Payee             |
        | Cafe Coffee House |
