@category:workspace-data
@category:feature-2206
Feature: Workspace Data Administration

  Background:
    Given the application is running
    And I have an existing account
    And I own a workspace called "My Finances"
    And I am logged into my existing account

  @category:story-2223
  Rule: Sample Data

    @id:1
    @category:smoke
    Scenario: Explore a workspace with realistic sample data
      When I load the "Personal Finance" sample data
      Then I see confirmation of the transactions and rules added
      And I can see many pages of transactions
      And I can see many payee matching rules

    @id:2
    Scenario: Load sample data into the workspace I select
      Given I also own a workspace called "Practice"
      And I am viewing transactions in "My Finances"
      When I open the manage page for "Practice"
      And I load the "Personal Finance" sample data
      Then I can see many pages of transactions in "Practice"
      And the transactions for "My Finances" is empty

    @id:3
    Scenario: Keep my existing data while trying sample data
      Given My workspace has existing transactions and payee matching rules
      When I load the "Personal Finance" sample data
      Then my existing transactions and rules remain unchanged
      And the sample transactions and rules are available alongside them

    @id:4
    Scenario: Categorize new sample bank transactions automatically
      Given I have loaded the "Personal Finance" sample data
      When I download sample OFX data with the default data range
      And I upload the OFX file
      Then I can see many transactions waiting for review
      And the first page of transactions are all selected
      And the first page of transactions have all been categorized

  @category:story-2222
  Rule: Delete all tenant data

    @id:5
    Scenario: Clear existing data after choosing sample import
      Given I have loaded the "Personal Finance" sample data
      And I have uploaded an OFX file with 10 transactions
      When I delete my workspace data
      Then I see confirmation of the data deleted
      And the transactions list is empty
      And the payee matching rules are empty
      And the import review queue should be empty

  @category:story-2221
  Rule: Import data into workspace

    @category:scenario-2233
    @id:1
    Scenario: Import original YoFi Payees into workspace
      When I upload a file containing original YoFi Payees
      And I preview and confirm the upload
      Then I can see many payee matching rules

  @category:story-2221
  Rule: Native YoFi workbooks are detected automatically

    @id:2
    Scenario: Import native payee matching rules
      When I upload a file containing 15 rules in native YoFi format
      Then I should see the detected format "YoFi.V3"
      And I should see the Payee worksheet preview
      And I should see 15 payee matching rules on the rules page

    @id:3
    @category:smoke
    Scenario: Import native transactions and their splits
      When I upload a file containing 10 transactions and 1 split transaction in native YoFi format
      Then I should see the detected format "YoFi.V3"
      And I should see the Transactions worksheet preview
      And I should see the Splits worksheet preview
      And I should see 10 transactions on the transactions page
      And I should see 1 transaction with a split indicator

  @category:story-2220
  Rule: Export workspace data

    @id:1
    @category:smoke
    Scenario: Export workspace data
      Given My workspace has existing data spanning multiple years
      When I export my entire workspace data
      Then I should receive a file containing my workspace data
      And the exported file should include all transactions from the workspace
      And the exported file should include all payee matching rules from the workspace

    @id:2
    Scenario: Export transactions for one year only
      Given My workspace has existing data spanning multiple years
      When I export transactions for one year only
      Then I should receive a file containing only that year's transactions
      And the exported file should not include transactions from previous years
      And the exported file should not include any payee matching rules
