@category:feature-1982
@category:transaction-splits
Feature: Transaction Splits

    Personal finance tracking requires categorizing transactions across multiple categories.
    For example, a single grocery store purchase might include both groceries (Food) and household
    supplies (Home). Previously, YoFi.V3 only supported a single category per transaction, forcing
    users to either create artificial splits or accept imprecise categorization. This would have limited
    the value of category-based reports and budgeting.

    Background:
        Given the application is running
        And I am logged in as a user with "Editor" role

    @category:story-2252
    @category:add-splits
    Rule: Add a single split to an existing transaction

        Scenario: Add a single split to an existing transaction with one split
            Given I have a workspace with a transaction:
                | Payee            | Amount  | Category |
                | Maverick Markets | -100.00 | Fuel     |
            And I am on the transactions detail page for that transaction
            When I add a new split with the following details:
                | Category | Amount | Memo         |
                | Snacks   | -10.00 | I was hungry |
            Then the following splits are displayed:
                | Category | Amount | Memo         |
                | Fuel     | -90.00 |              |
                | Snacks   | -10.00 | I was hungry |
            And the transaction amount remains -100.00
            And the transaction is balanced

        Scenario: Add a single split to an existing transaction with multiple splits
            Given I have a workspace with a transaction for payee "Super Duper Mart" having these splits:
                | Amount  | Category                 | Memo                 |
                | -125.00 | Groceries                |                      |
                | -50.00  | Supplies                 |                      |
                | -75.00  | Healthcare:Prescriptions | Can't forget my meds |
            And I am on the transactions detail page for that transaction
            When I add a new split with the following details:
                | Category   | Amount | Memo                  |
                | Dining Out | -25.00 | Also had dinner there |
            Then the following splits are displayed:
                | Amount  | Category                 | Memo                  |
                | -100.00 | Groceries                |                       |
                | -50.00  | Supplies                 |                       |
                | -75.00  | Healthcare:Prescriptions | Can't forget my meds  |
                | -25.00  | Dining Out               | Also had dinner there |
            And the transaction amount remains -250.00
            And the transaction is balanced

    @category:story-2250
    @category:view-splits
    Rule: View splits on list and details page

        @id:1
        Scenario: Split indicator is visible on the transactions list page for transactions with splits
            Given I have a workspace with a transaction:
                | Payee            | Amount  | Category | Days Ago |
                | Maverick Markets | -100.00 | Fuel     | 1        |
            And I have a workspace with a transaction for payee "Super Duper Mart" having these splits:
                | Amount  | Category                 | Memo                 |
                | -125.00 | Groceries                |                      |
                | -50.00  | Supplies                 |                      |
                | -75.00  | Healthcare:Prescriptions | Can't forget my meds |
            When I view my transactions
            Then the following transactions should be shown:
                | Payee            | Category | Split Indicator |
                | Super Duper Mart |          | Yes             |
                | Maverick Markets | Fuel     | No              |

        @id:2
        Scenario: View splits on the transaction details page
            Given I have a workspace with a transaction for payee "Super Duper Mart" having these splits:
                | Amount  | Category                 | Memo                 |
                | -125.00 | Groceries                |                      |
                | -50.00  | Supplies                 |                      |
                | -75.00  | Healthcare:Prescriptions | Can't forget my meds |
            And I am on the transactions detail page for that transaction
            Then the following splits are displayed:
                | Amount  | Category                 | Memo                 |
                | -125.00 | Groceries                |                      |
                | -50.00  | Supplies                 |                      |
                | -75.00  | Healthcare:Prescriptions | Can't forget my meds |
