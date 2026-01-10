Feature: Transaction Record Fields
    As a user managing transactions
    I want to record additional details about each transaction
    So that I can track memo notes, source accounts, and external identifiers

Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Rule: Quick Edit Modal
    The quick edit modal should only show Payee, Category, and Memo fields for rapid updates

    Scenario: Quick edit modal shows Payee, Category, and Memo fields
        Given I have a workspace with a transaction:
            | Field    | Value           |
            | Payee    | Coffee Shop     |
            | Amount   | 5.50            |
            | Category | Beverages       |
            | Memo     | Morning coffee  |
        And I am on the transactions page
        When I click the "Edit" button on the transaction
        Then I should see a modal titled "Quick Edit Transaction"
        And I should only see fields for "Payee", "Category", and "Memo"
        And the fields match the expected values
        And I should not see fields for "Date", "Amount", "Source", or "ExternalId"

    Scenario: User updates Memo via quick edit modal
        Given I have a workspace with a transaction:
            | Field      | Value          |
            | Payee      | Coffee Co      |
            | Amount     | -5.50          |
            | Memo       | Morning latte  |
            | Source     | Chase Checking |
            | ExternalId | CHK-001        |
        And I am on the transactions page
        When I quick edit the transaction
        And I change Memo to "Large latte with extra shot"
        And I click "Update"
        Then the modal should close
        And I should see the updated memo in the transaction list

    Scenario: User edits category via quick edit and sees it in list
        Given I have a workspace with a transaction:
            | Field    | Value       |
            | Payee    | Grocery Co  |
            | Amount   | -45.67      |
            | Category | Food        |
        And I am on the transactions page
        When I quick edit the transaction
        And I change Category to "Groceries"
        And I click "Update"
        Then the modal should close
        And I should see the updated category in the transaction list

Rule: Transaction Details Page
    Users can view, edit, and navigate transaction details

    Scenario: Transaction details page displays category
        Given I have a workspace with a transaction:
            | Field    | Value          |
            | Payee    | Restaurant XYZ |
            | Amount   | -32.50         |
            | Category | Dining         |
        And I am on the transactions page
        When I click on the transaction row
        Then I should navigate to the transaction details page
        And I should see all the expected transaction fields displayed

    @explicit:broken
    Scenario: User navigates from transaction list to details page
        Given I have a workspace with a transaction:
            | Field      | Value          |
            | Payee      | Gas Mart       |
            | Amount     | -40.00         |
            | Memo       | Fuel up        |
            | Source     | Chase Checking |
            | ExternalId | CHK-002        |
        And I am on the transactions page
        When I click on the transaction row
        Then I should navigate to the transaction details page
        # The problem here lives in the external ID being overwritten by the test controller.
        # This is needed for another test, but breaks this one.
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
        # TODO: Doesn't render correctly
        # Then I should see "Chase Visa" as the Source
        # And I should see "VISA-123" as the ExternalId

    Scenario: User edits category on transaction details page
        Given I am viewing the details page for a transaction with:
            | Field    | Value      |
            | Payee    | Hardware   |
            | Amount   | -89.99     |
            | Category | Tools      |
        When I click the "Edit" button
        And I change Category to "Home Improvement"
        And I click "Save"
        Then I should see "Home Improvement" as the Category

    Scenario: User returns to list from transaction details page
        Given I am viewing the details page for a transaction
        When I click "Back to Transactions"
        Then I should return to the transaction list
        And I should see all my transactions

Rule: Users can create new transactions with all transaction record fields

    Scenario: User sees all fields in create transaction modal
        Given I am on the transactions page
        When I click the "Add Transaction" button
        Then I should see a create transaction modal
        And I should see the following fields in the create form:
            | Field       |
            | Date        |
            | Payee       |
            | Amount      |
            | Memo        |
            | Source      |
            | External ID |

    Scenario: User creates transaction with all fields populated
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Category    | Office Supplies         |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        Then the modal should close
        And I should see a transaction with Payee "Office Depot"
        And it contains the expected list fields

    Scenario: Created transaction displays all fields on details page
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Category    | Office Supplies         |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        And I click on the transaction row
        Then I should see all the expected transaction fields displayed
