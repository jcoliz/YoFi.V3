Feature: Bank Import
  As a YoFi user
  I want to import transactions from bank files
  So that I can add my bank transactions to YoFi without manual entry
  NOTE: This feature file is generated from the PRD with the intent
  to be more business-driven than technical.

  Background:
    Given I am logged in as a user with Editor role
    And I have selected my workspace

  Rule: Users can upload bank files for import

    @pri:1
    @id:2
    @status:done
    Scenario: Successfully upload valid OFX file
      Given I have a valid OFX file with 10 transactions
      When I navigate to the Import page
      And I upload the OFX file
      Then I should see 10 transactions in the review list
      And all transactions should display date, payee, and amount

    @pri:2
    Scenario: Successfully upload valid QFX file
      Given I have a valid QFX file with 5 transactions
      When I navigate to the Import page
      And I upload the QFX file
      Then I should be redirected to the Import Review page
      And I should see 5 transactions in the review list

    @pri:2
    Scenario: Upload file with invalid format
      Given I have an invalid file with wrong format
      When I navigate to the Import page
      And I upload the invalid file
      Then I should see an error message "Unsupported file format - expected OFX or QFX"
      And I should remain on the Import page

    @pri:3
    Scenario: Upload corrupted bank file
      Given I have a corrupted OFX file
      When I navigate to the Import page
      And I upload the corrupted file
      Then I should see an error message "File appears corrupted - unable to parse transaction data"
      And I should remain on the Import page

    @pri:3
    Scenario: Viewer role cannot access import workflow
      Given I am logged in as a user with Viewer role
      And I have selected my workspace
      When I attempt to navigate to the Import page
      Then I should be denied access
      And I should see a permission error message

    @pri:2
    @id:7
    Scenario: Editor role can access import workflow
      Given I am logged in as a user with Editor role
      And I have selected my workspace
      When I navigate to the Import page
      Then I should see the file upload interface

    @pri:3
    Scenario: Owner role can access import workflow
      Given I am logged in as a user with Owner role
      And I have selected my workspace
      When I navigate to the Import page
      Then I should see the file upload interface

  Rule: Users can review imported transactions and identify duplicates

    @pri:1
    @id:1
    @status:done
    Scenario: Review new transactions with no duplicates
      Given I have uploaded an OFX file with 10 new transactions
      When I am on the Import Review page
      Then all 10 transactions should be marked as "New"
      And all 10 transactions should be selected by default

    @pri:1
    @id:6
    Scenario: Review transactions with exact duplicates
      Given I have 5 existing transactions in my workspace
      And I have uploaded an OFX file containing the same 5 transactions
      When I am on the Import Review page
      Then all 5 transactions should be deselected by default
      And no transactions should be highlighted for further review

    @pri:2
    @id:9
    @explicit:wip
    Scenario: Review transactions with potential duplicates
      Given I have 3 existing transactions in my workspace
      And I have uploaded an OFX file with 3 transactions matching the same dates and amounts but different payee names
      And I am reviewing them on the Import Review page
      Then all 3 transactions should be marked as "Potential Duplicate"
      And all 3 transactions should be highlighted
      And all 3 transactions should be deselected by default
      And I should see a summary "3 potential duplicates"

    @pri:3
    Scenario: Review mixed transaction types
      Given I have 10 existing transactions in my workspace
      And I have uploaded an OFX file with 20 transactions
      And 5 transactions are new
      And 10 transactions are exact duplicates
      And 5 transactions are potential duplicates
      When I am on the Import Review page
      Then I should see 5 transactions marked as "New" and selected
      And I should see 10 transactions marked as "Exact Duplicate" and deselected
      And I should see 5 transactions marked as "Potential Duplicate" and deselected
      And I should see a summary "5 new, 10 exact duplicates, 5 potential duplicates"

    @pri:2
    Scenario: Select and deselect individual transactions
      Given I have uploaded an OFX file with 10 new transactions
      And I am reviewing them on the Import Review page
      And all 10 transactions are selected by default
      When I deselect 3 transactions
      Then I should see 7 transactions selected
      And I should see 3 transactions deselected

    @pri:4
    Scenario: Reselect previously deselected transactions
      Given I have uploaded an OFX file with 10 new transactions
      And I am reviewing them on the Import Review page
      And I have deselected 3 transactions
      When I select 2 of the deselected transactions
      Then I should see 9 transactions selected
      And I should see 1 transaction deselected

    @id:3
    @pri:1
    Scenario: Accept selected transactions
      Given I have uploaded an OFX file with 10 new transactions
      And I am reviewing them on the Import Review page
      And 2 transactions are deselected
      When I click the "Accept" button
      Then I should see a confirmation "8 transactions accepted, 2 transactions remain in review"
      And the 8 accepted transactions should appear in my Transactions list
      And import review queue should be completely cleared

    @pri:2
    Scenario: Accept all transactions clears import review
      Given I have uploaded an OFX file with 10 new transactions
      And I am reviewing them on the Import Review page
      And all 10 transactions are selected
      When I click the "Accept" button
      Then I should see a confirmation "10 transactions accepted"
      And the Import Review page should be empty
      And all 10 transactions should appear in my Transactions list

    @pri:4
    Scenario: Manually select duplicate for import
      Given I have 5 existing transactions in my workspace
      And I have uploaded an OFX file containing the same 5 transactions as exact duplicates
      And I am reviewing them on the Import Review page
      And all 5 transactions are deselected by default
      When I manually select 2 of the duplicate transactions
      And I click the "Accept" button
      Then I should see a confirmation "2 transactions accepted"
      And those 2 transactions should be added to my Transactions list as new entries
      And I should now have duplicate transactions in my workspace

  Rule: Import review state persists and can be managed over time

    @pri:2
    Scenario: Import review state persists across sessions
      Given I have uploaded an OFX file with 10 transactions
      And I am reviewing them on the Import Review page
      And I have deselected 3 transactions
      When I log out and log back in
      And I navigate to the Import Review page
      Then I should see the same 10 transactions in review
      And the 3 transactions should still be deselected

    @pri:4
    Scenario: Return to pending import review later
      Given I have uploaded an OFX file with 10 transactions
      And I am reviewing them on the Import Review page
      When I navigate away to the Transactions page
      And I navigate back to the Import Review page
      Then I should see the same 10 transactions in review

    @pri:3
    Scenario: Upload additional files while import is in review
      Given I have uploaded an OFX file with 10 transactions
      And I am reviewing them on the Import Review page
      When I navigate back to the Import page
      And I upload another OFX file with 5 transactions
      Then I should be redirected to the Import Review page
      And I should see 15 transactions in the review list
      And all transactions should be merged into a single review queue

    @pri:4
    Scenario: Multiple imports preserve previous selection state
      Given I have uploaded an OFX file with 10 transactions
      And 5 transactions are selected for import
      When I upload another OFX file with 8 transactions
      Then I should see 18 total transactions in review
      And the previous selection state should be preserved for the first 10 transactions
      And the new 8 transactions should have default selection based on their duplicate status

    @pri:4
    Scenario: Delete all transactions from import review
      Given I have uploaded an OFX file with 10 transactions
      And I am on the Import Review page
      When I click the "Delete All" button
      And I confirm the deletion
      Then the Import Review page should be empty
      And no transactions from the import should appear in my Transactions list

    @pri:1
    @id:4
    @status:done
    Scenario: Transactions in import review do not appear in transaction list
      Given I have 5 existing transactions in my workspace
      And I have uploaded an OFX file with 10 new transactions
      When I navigate to the Transactions page
      Then I should see only the 5 original transactions
      And the 10 transactions in review should not appear

    @pri:4
    Scenario: Transactions in import review do not affect balance calculations
      Given I have 5 existing transactions totaling $500.00
      And I have uploaded an OFX file with 10 transactions totaling $1,000.00
      When I view my balance
      Then the balance should reflect only the $500.00 from existing transactions
      And the $1,000.00 from transactions in review should not be included

  Rule: Import errors are handled gracefully with clear feedback

    @pri:3
    Scenario: Import file with invalid date format
      Given I have an OFX file with 10 transactions
      And transaction 3 has an invalid date format
      When I upload the file
      Then I should see an error message "Transaction 3: Invalid date format"
      And I should see a summary "9 transactions imported for review, 1 transaction failed"
      And the 9 valid transactions should appear in the Import Review page

    @pri:3
    Scenario: Import file with missing required fields
      Given I have an OFX file with 10 transactions
      And transaction 5 is missing the amount field
      And transaction 7 is missing the date field
      When I upload the file
      Then I should see an error message "Transaction 5: Missing required field (amount)"
      And I should see an error message "Transaction 7: Missing required field (date)"
      And I should see a summary "8 transactions imported for review, 2 transactions failed"
      And the 8 valid transactions should appear in the Import Review page

    @pri:5
    Scenario: View details of failed transactions
      Given I have uploaded an OFX file with 10 transactions
      And 3 transactions failed to import
      When I view the import error details
      Then I should see a list of the 3 failed transactions
      And each failed transaction should show the reason for failure
      And each failed transaction should show the raw data from the file

    @pri:5
    Scenario: Import continues despite partial failures
      Given I have an OFX file with 100 transactions
      And 10 transactions have various validation errors
      When I upload the file
      Then the import should complete successfully
      And I should see a summary "90 transactions imported for review, 10 transactions failed"
      And the 90 valid transactions should be available for review
      And the system should not create partial or incomplete transaction records

    @pri:5
    Scenario: All transactions fail validation
      Given I have an OFX file with 5 transactions
      And all 5 transactions have invalid data
      When I upload the file
      Then I should see an error message "0 transactions imported for review, 5 transactions failed"
      And I should remain on the Import page
      And no transactions should appear in the Import Review page

  Rule: Large file imports are handled efficiently with pagination

    @pri:2
    Scenario: Large file import completes successfully
      Given I have a valid OFX file with 1000 transactions
      When I upload the file
      Then the upload should complete within a reasonable time
      And I should be redirected to the Import Review page
      And I should see pagination controls
      And the first page should display transactions

    @pri:3
    Scenario: Import review with pagination
      Given I have uploaded an OFX file with 500 transactions
      When I am on the Import Review page
      Then I should see transactions displayed in pages
      And I should see pagination controls

    @pri:5
    Scenario: Navigate between pages maintains state
      Given I have uploaded an OFX file with 500 transactions
      And I am on the Import Review page
      When I navigate to page 2
      Then I should see the next set of transactions
      And the selection state should be maintained across pages

    @pri:5
    Scenario: Accept transactions from multiple pages
      Given I have uploaded an OFX file with 500 transactions
      And I am on the Import Review page
      And I have selected 10 transactions on page 1
      And I have navigated to page 5 and selected 5 transactions
      When I click the "Accept" button
      Then I should see a confirmation "15 transactions accepted"
      And all 15 selected transactions should appear in my Transactions list

  Rule: Duplicate detection uses appropriate identification strategy

    @pri:5
    Scenario: Categories are not imported from bank files
      Given I have uploaded an OFX file with 10 transactions
      When I review the transactions on the Import Review page
      Then all transactions should have blank category fields
      And I should be able to accept transactions without categories

    @pri:3
    Scenario: Duplicate detection uses bank transaction ID when available
      Given I have a transaction with bank ID "ABC123" in my workspace
      And I upload an OFX file with a transaction having the same bank ID "ABC123"
      When I am on the Import Review page
      Then the transaction should be marked as "Exact Duplicate"
      And the duplicate detection should use the bank ID for matching

    @pri:4
    Scenario: Duplicate detection uses hash when bank ID not available
      Given I have a transaction on 2024-01-15 for $50.00 to "Test Payee" in my workspace
      And I upload an OFX file with a transaction on 2024-01-15 for $50.00 to "Test Payee"
      And the OFX file does not include bank transaction IDs
      When I am on the Import Review page
      Then the transaction should be marked as "Exact Duplicate"
      And the duplicate detection should use date + amount + payee hash for matching

  Rule: Tenant isolation ensures import review privacy

    @pri:1
    @id:5
    Scenario: Cannot access other tenants' import reviews
      Given I am logged in as User A in Tenant A
      And User A has uploaded an OFX file with 10 transactions
      When I log out and log in as User B from Tenant B
      And I navigate to the Import Review page
      Then I should not see User A's transactions
      And the Import Review page should be empty

    @pri:2
    Scenario: Import reviews are isolated within shared workspace
      Given I am logged in as User A with Editor role in Workspace "Family"
      And User B is also a member of Workspace "Family"
      And User B has uploaded an OFX file with 10 transactions to their import review
      When I navigate to the Import Review page
      Then I should not see User B's import review
      And the Import Review page should be empty for me

    @pri:2
    Scenario: Accepted transactions become visible to all workspace members
      Given I am logged in as User A with Editor role in Workspace "Family"
      And User B is also a member of Workspace "Family"
      And I have uploaded an OFX file with 10 transactions
      And I am on the Import Review page
      When I accept all 10 transactions
      Then the 10 transactions should appear in the shared workspace
      And User B should be able to see the 10 transactions in the Transactions list
