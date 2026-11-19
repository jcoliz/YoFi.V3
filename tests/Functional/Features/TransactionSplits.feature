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

    @category:story-2225
    @category:scenario-2252
    @category:add-splits
    Rule: Add a single split to an existing transaction

        @category:smoke
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
            And the splits total matches the transaction amount

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
            And the splits total matches the transaction amount

    @category:story-2225
    @category:scenario-2250
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

    @category:edit-splits
    @category:story-2225
    @category:scenario-2251
    Rule: Edit splits

        @id:1
        Scenario: Edit a non-first split amount
            Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
                | Amount | Category  | Memo |
                | -60.00 | Groceries |      |
                | -40.00 | Household |      |
            And I am on the transactions detail page for that transaction
            When I edit the "Household" split amount to -50.00
            Then the following splits are displayed:
                | Amount | Category  | Memo |
                | -50.00 | Groceries |      |
                | -50.00 | Household |      |
            And the transaction amount remains -100.00
            And the splits total matches the transaction amount

        @id:2
        Scenario: Edit the first split amount

            This ensures the transaction remains balanced after editing the first split amount

            Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
                | Amount | Category  | Memo |
                | -60.00 | Groceries |      |
                | -40.00 | Household |      |
            And I am on the transactions detail page for that transaction
            When I edit the "Groceries" split amount to -70.00
            Then the following splits are displayed:
                | Amount | Category  | Memo |
                | -70.00 | Groceries |      |
                | -30.00 | Household |      |
            And the transaction amount remains -100.00
            And the splits total matches the transaction amount

    @category:delete-splits
    @category:story-2225
    @category:scenario-2289
    Rule: Delete splits

        @id:1
        Scenario: Delete a non-first split
            Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
                | Amount | Category  | Memo |
                | -60.00 | Groceries |      |
                | -40.00 | Household |      |
                | -10.00 | Snacks |      |
            And I am on the transactions detail page for that transaction
            When I delete the "Household" split
            Then the following splits are displayed:
                | Amount | Category  | Memo |
                | -100.00 | Groceries |      |
                | -10.00 | Snacks |      |
            And the transaction amount remains -110.00
            And the splits total matches the transaction amount

        @id:2
        Scenario: Delete the first split
            Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
                | Amount | Category  | Memo |
                | -60.00 | Groceries |      |
                | -40.00 | Household |      |
                | -10.00 | Snacks |      |
            And I am on the transactions detail page for that transaction
            When I delete the "Groceries" split
            Then the following splits are displayed:
                | Amount | Category  | Memo |
                | -100.00 | Household |      |
                | -10.00 | Snacks |      |
            And the transaction amount remains -110.00
            And the splits total matches the transaction amount

        @id:3
        Scenario: Delete the second of two splits, leaving only one split
            Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
                | Amount | Category  | Memo |
                | -60.00 | Groceries |      |
                | -40.00 | Household |      |
            And I am on the transactions detail page for that transaction
            When I delete the "Household" split
            Then the splits list is empty
            And the transaction amount remains -100.00
            And no split balance warning is shown

    @category:story-2308
    @category:unbalanced-splits
    Rule: Handle unbalanced transactions

        @id:1
        Scenario: List shows an indicator for an unbalanced transaction
            Given I have a transaction for payee "Out of balance" with amount "-100.00" and these splits:
                | Amount | Category  |
                | -90.00 | Groceries |
                | -5.00  | Household |
            When I view my transactions
            Then that transaction is shown with an unbalanced indicator

        @id:2
        Scenario: Details show transaction and split totals for an unbalanced transaction
            Given I have a transaction for payee "Out of balance" with amount "-100.00" and these splits:
                | Amount | Category  |
                | -90.00 | Groceries |
                | -5.00  | Household |
            And I am on the transactions detail page for that transaction
            Then the split balance warning is visible
            And the split balance warning shows transaction amount "-100.00" and split total "-95.00"

    @category:story-2309
    @category:upload-splits
    Rule: Upload splits to a transaction

        @id:1
        Scenario: Upload valid split rows, append them, and remove the zero balancing split
            Given I have a workspace with a transaction:
                | Payee          | Amount  | Category |
                | Paystub        | 1000.00 | Income   |
            And I am on the transactions detail page for that transaction
            When I upload a split workbook with these first-worksheet rows:
                | Category | Amount | Memo     |
                | Salary   | 800.00 | Base pay |
                | Bonus    | 200.00 | Annual   |
            Then the splits should be uploaded successfully
            And the following splits are displayed:
                | Amount | Category | Memo     |
                | 800.00 | Salary   | Base pay |
                | 200.00 | Bonus    | Annual   |
            And the splits total matches the transaction amount

        @id:2
        Scenario: Append uploaded splits to existing allocations and preserve workbook order
            Given I have a workspace with a transaction for payee "Paystub" having these splits:
                | Amount | Category | Memo          |
                | 1000.00 | Income  | Original row  |
                |  -25.00 | Tax     | Existing item |
            And I am on the transactions detail page for that transaction
            When I upload a split workbook with these first-worksheet rows:
                | Category  | Amount | Memo        |
                | Deduction | -50.00 | Healthcare  |
                | Bonus     | 100.00 | Performance |
            Then the splits should be uploaded successfully
            And the following splits are displayed:
                | Amount  | Category  | Memo          |
                |  950.00 | Income    | Original row  |
                |  -25.00 | Tax       | Existing item |
                |  -50.00 | Deduction | Healthcare    |
                |  100.00 | Bonus     | Performance   |
            And the transaction is balanced
            And the splits total matches the transaction amount
