Feature: Workspace Data Administration

  Background:
    Given the application is running
    And I have an existing account
    And I own a workspace called "My Finances"
    And I am logged into my existing account

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

@hidden
Scenario: Clear existing data after choosing sample import
  Given I am using a development or testing environment
  And I own a workspace with transactions, payee matching rules, and pending bank imports
  And I have chosen to load Personal Finance sample data
  And I have chosen to delete all existing workspace data first
  And I have been warned that deletion is irreversible even if sample loading fails
  When I confirm deleting all existing workspace data
  Then I see confirmation of the data deleted
  And my previous transactions, rules, and pending imports are gone
  And the workspace and its memberships remain
  And sample loading awaits my separate confirmation

@hidden
Scenario: Categorize new sample bank transactions automatically
  Given I own a workspace containing the Personal Finance sample dataset
  When I download sample OFX data for a later non-overlapping date range
  And I upload that file through Bank Import
  Then I see transactions within the requested dates ready for review
  And the sample payee matching rules have assigned the expected categories
