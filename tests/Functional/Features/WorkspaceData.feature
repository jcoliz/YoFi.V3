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
    @explicit:wip
    Scenario: Import original YoFi Payees into workspace
      When I upload a file containing original YoFi Payees
      And I preview and confirm the upload
      Then I can see many payee matching rules
