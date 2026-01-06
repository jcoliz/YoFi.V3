@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:YoFi.V3.Tests.Functional.Infrastructure.FunctionalTestBase
Feature: Workspace Management
    As a YoFi user
    I want to create and manage separate financial workspaces
    So that I can organize my finances by purpose and share them with others

Background:
    Given the application is running
    And these users exist:
        | Username |
        | alice    |
        | bob      |
        | charlie  |

Rule: Getting Started
    New users automatically receive their own workspace to begin tracking finances

    Scenario: New user automatically has a personal workspace
        Given the application is running
        When a new user "david" registers and logs in
        Then user should have a workspace ready to use
        And the workspace should be personalized with the name "david"

Rule: Creating Workspaces
    Users can create multiple workspaces for different purposes

    Scenario: User creates a workspace for a specific purpose
        Given I am logged in as "alice"
        When I create a new workspace called "Alice's Finances" for "My personal finances"
        Then I should see "Alice's Finances" in my workspace list
        And I should be able to manage that workspace

    Scenario: User organizes finances across multiple workspaces
        Given I am logged in as "bob"
        When I create a workspace called "Personal Budget"
        And I create a workspace called "Side Business"
        Then I should have 2 workspaces available
        And I can work with either workspace independently

Rule: Viewing Workspaces
    Users can see all workspaces they have access to

    Scenario: User views all their workspaces
        Given "alice" has access to these workspaces:
            | Workspace Name | My Role |
            | Personal       | Owner   |
            | Family Budget  | Editor  |
            | Tax Records    | Viewer  |
        And I am logged in as "alice"
        When I view my workspace list
        Then I should see all 3 workspaces
        And I should see what I can do in each workspace

    Scenario: User views workspace details
        Given "bob" owns a workspace called "My Finances"
        And I am logged in as "bob"
        When I view the details of "My Finances"
        Then I should see the workspace information
        And I should see when it was created

Rule: Managing Workspace Settings
    Workspace owners can customize their workspace settings

    Scenario: Owner updates workspace information
        Given "alice" owns a workspace called "Old Name"
        And I am logged in as "alice"
        When I rename it to "New Name"
        And I update the description to "Updated description text"
        Then the workspace should reflect the changes
        And I should see "New Name" in my workspace list

    Scenario: Non-owner cannot change workspace settings
        Given "bob" can edit data in "Family Budget"
        And I am logged in as "bob"
        When I try to change the workspace name or description
        Then I should not be able to make those changes

Rule: Removing Workspaces
    Workspace owners can permanently delete workspaces they no longer need

    Scenario: Owner removes an unused workspace
        Given "alice" owns a workspace called "Test Workspace"
        And I am logged in as "alice"
        When I delete "Test Workspace"
        Then "Test Workspace" should no longer appear in my list

    Scenario: Non-owner cannot delete a workspace
        Given "charlie" can view data in "Shared Workspace"
        And I am logged in as "charlie"
        When I try to delete "Shared Workspace"
        Then the workspace should remain intact

Rule: Data Isolation Between Workspaces
    Each workspace maintains its own separate financial data

    Scenario: User views transactions in a specific workspace
        Given "alice" has access to these workspaces:
            | Workspace Name | My Role |
            | Personal       | Owner   |
            | Business       | Owner   |
        And "Personal" contains 5 transactions
        And "Business" contains 3 transactions
        And I am logged in as "alice"
        When I view transactions in "Personal"
        Then I should see exactly 5 transactions
        And they should all be from "Personal" workspace
        And I should not see any transactions from "Business"

    Scenario: User cannot access data in workspaces they don't have access to
        Given "bob" has access to "Family Budget"
        And there is a workspace called "Private Finances" that "bob" doesn't have access to
        And I am logged in as "bob"
        When I try to view transactions in "Private Finances"
        Then I should not be able to access that data

Rule: Permission Levels
    Different permission levels control what users can do in a workspace

    Scenario: Viewer can see but not change data
        Given "charlie" can view data in "Family Budget"
        And "Family Budget" contains 3 transactions
        And I am logged in as "charlie"
        When I view transactions in "Family Budget"
        Then I should see the transactions
        # Error: Should not have multitle When/Then logic in a scenario
        When I try to add or edit transactions
        Then I should not be able to make those changes

    Scenario: Editor can view and modify data
        Given "bob" can edit data in "Family Budget"
        And I am logged in as "bob"
        When I add a transaction to "Family Budget"
        Then the transaction should be saved successfully
        # Error: Should not have multitle When/Then logic in a scenario
        When I update that transaction
        Then my changes should be saved
        When I delete that transaction
        Then it should be removed

    Scenario: Owner can do everything including managing the workspace
        Given "alice" owns "My Workspace"
        And "My Workspace" contains 3 transactions
        And I am logged in as "alice"
        Then I can add, edit, and delete transactions
        And I can change workspace settings
        And I can remove the workspace if needed

Rule: Privacy and Security
    Users can only see and access workspaces they have permission to use

    Scenario: Workspace list shows only accessible workspaces
        Given "bob" has access to "Family Budget"
        And there are other workspaces in the system:
            | Workspace Name  | Owner   |
            | Private Data    | alice   |
            | Charlie's Taxes | charlie |
        And I am logged in as "bob"
        When I view my workspace list
        Then I should see only "Family Budget" in my list
        And I should not see "Private Data" in my list
        And I should not see "Charlie's Taxes" in my list
        And the workspace count should be 1
