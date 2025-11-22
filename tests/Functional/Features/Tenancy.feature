@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:WorkspaceTenancySteps
@template:Features/FunctionalTest.mustache
@hook:before-first-then:SaveScreenshot
Feature: Workspace Tenancy and Sharing
    As a YoFi user
    I want to manage shared financial workspaces with different access levels
    So that I can collaborate with family members while maintaining appropriate permissions

Background:
    Given the application is running
    And these users exist:
        | Username |
        | alice    |
        | bob      |
        | charlie  |

Rule: Workspace Creation and Access Control
    Users must have appropriate permissions to access workspace data

    Scenario: User creates their first personal workspace
        Given I am logged in as "alice"
        And I have no existing workspaces
        When I create my first workspace named "Alice's Finances"
        Then I should be the owner of "Alice's Finances"
        And "Alice's Finances" should be set as my default workspace
        And I should see "Alice's Finances" in my workspace list

    Scenario: User cannot access workspace without permission
        Given I am logged in as "charlie"
        And a workspace named "Family Budget" exists
        But I do not have access to "Family Budget"
        When I try to directly access "Family Budget" data
        Then I should be denied access
        And I should see an appropriate error message
        And I should not see any financial data from that workspace

Rule: Role-Based Permissions
    Different user roles have different capabilities within workspaces

    Scenario: Editor can modify financial data but cannot manage users
        Given I am logged in as "bob"
        And I have editor access to "Family Budget"
        When I view "Family Budget"
        Then I can add new transactions
        And I can edit existing transactions
        And I can create budget categories
        But I cannot invite other users
        And I cannot change user permissions
        And I cannot delete the workspace

    Scenario: Workspace owner grants viewer-only access to external consultant
        Given I am logged in as "alice"
        And I own a workspace named "Family Budget"
        When I invite "charlie" to access "Family Budget" as a viewer
        And "charlie" accepts the invitation
        Then "charlie" should have viewer access to "Family Budget"
        And when "charlie" views "Family Budget"
        Then they can see all transactions and reports
        But they cannot add or modify any data
        And they cannot see user management options

Rule: Workspace Navigation and Context
    Users can switch between multiple workspaces and set preferences

    Scenario: User switches between multiple workspaces
        Given I am logged in as "bob"
        And I have access to multiple workspaces:
            | Workspace Name   | My Role |
            | Bob's Personal   | Owner   |
            | Family Budget    | Editor  |
            | Shared Vacation  | Viewer  |
        When I view my workspace selector
        Then I should see all accessible workspaces with my role indicated
        And when I switch to "Family Budget"
        Then my current workspace context should change to "Family Budget"
        And I should see editor-appropriate options

    Scenario: User sets preferred default workspace
        Given I am logged in as "bob"
        And I have access to multiple workspaces
        And my current default workspace is "Bob's Personal"
        When I change my default workspace to "Family Budget"
        Then "Family Budget" should be selected when I log in
        And "Family Budget" should be marked as default in my preferences

Rule: User Management and Invitations
    Workspace owners can invite users and manage their access levels

    Scenario: Workspace owner invites family member with editor access
        Given I am logged in as "alice"
        And I own a workspace named "Family Budget"
        When I invite "bob" to access "Family Budget" as an editor
        Then "bob" should receive an invitation
        And when "bob" accepts the invitation
        Then "bob" should have editor access to "Family Budget"
        And "bob" should see "Family Budget" in their workspace list

    Scenario: Workspace owner removes user access
        Given I am logged in as "alice"
        And I own a workspace named "Family Budget"
        And "bob" has editor access to "Family Budget"
        When I remove "bob" from "Family Budget"
        Then "bob" should no longer see "Family Budget" in their workspace list
        And "bob" should not be able to access "Family Budget" data
        And "bob" should be notified of the access removal

    Scenario: Workspace owner transfers ownership
        Given I am logged in as "alice"
        And I own a workspace named "Family Budget"
        And "bob" has editor access to "Family Budget"
        When I transfer ownership of "Family Budget" to "bob"
        Then "bob" should become the owner of "Family Budget"
        And I should have editor access to "Family Budget"
        And "bob" should be able to manage user permissions

    Scenario: Invitation expires after reasonable time
        Given I am logged in as "alice"
        And I invited "charlie" to access "Family Budget"
        But "charlie" has not responded for 30 days
        When the invitation expires
        Then "charlie" should no longer be able to accept the invitation
        And I should be able to send a new invitation if needed

Rule: Workspace Lifecycle Management
    Workspaces can be deleted or left by users with appropriate consequences

    Scenario: User deletes workspace they own
        Given I am logged in as "alice"
        And I own a workspace named "Old Workspace"
        And "Old Workspace" contains financial data
        When I request to delete "Old Workspace"
        Then I should be warned about permanent data loss
        And when I confirm the deletion
        Then "Old Workspace" should be permanently removed
        And all users should lose access to "Old Workspace"
        And all financial data in "Old Workspace" should be deleted

    Scenario: User leaves shared workspace
        Given I am logged in as "bob"
        And I have editor access to "Family Budget"
        But I am not the owner of "Family Budget"
        When I choose to leave "Family Budget"
        Then I should be removed from "Family Budget"
        And I should no longer see "Family Budget" in my workspace list
        And the workspace owner should be notified of my departure
