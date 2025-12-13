@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:WorkspaceTenancySteps
@template:Features/FunctionalTest.mustache
@hook:before-first-then:SaveScreenshot
Feature: Workspace Collaboration and Sharing
    As a YoFi user
    I want to share my workspaces with family members and collaborators
    So that we can work together on our finances with appropriate permissions

    NOTE: This feature file describes PLANNED functionality that is not yet implemented.

Background:
    Given the application is running
    And these users exist:
        | Username |
        | alice    |
        | bob      |
        | charlie  |

Rule: Inviting People to Workspaces
    Workspace owners can invite others to collaborate

    Scenario: Owner invites family member to help manage finances
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        When I invite "bob" to help edit "Family Budget"
        Then "bob" should receive an invitation
        And when "bob" accepts the invitation
        Then "bob" should be able to work with "Family Budget"
        And "bob" should see "Family Budget" in their workspace list

    Scenario: Owner invites consultant for read-only access
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        When I invite "charlie" to view "Family Budget" without editing
        And "charlie" accepts the invitation
        Then "charlie" can view all transactions and reports
        But "charlie" cannot add or change any data
        And "charlie" cannot see workspace management options

    Scenario: Invitation expires if not accepted within reasonable time
        Given I am logged in as "alice"
        And I invited "charlie" to "Family Budget"
        But "charlie" has not responded for 30 days
        When the invitation expires
        Then "charlie" can no longer accept that invitation
        And I can send a new invitation if needed

Rule: Removing Workspace Access
    Workspace owners can remove people when collaboration ends

    Scenario: Owner removes someone's access to workspace
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        And "bob" can edit "Family Budget"
        When I remove "bob" from "Family Budget"
        Then "bob" should no longer see "Family Budget" in their list
        And "bob" should no longer be able to access that workspace's data
        And "bob" should be notified about the change

    Scenario: User decides to stop using a shared workspace
        Given I am logged in as "bob"
        And I can edit "Family Budget"
        But I don't own "Family Budget"
        When I choose to leave "Family Budget"
        Then I should no longer see it in my workspace list
        And the owner should be notified

Rule: Adjusting Permission Levels
    Workspace owners can change what collaborators are allowed to do

    Scenario: Owner gives someone more control over workspace
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        And "bob" can edit "Family Budget"
        When I give "bob" full ownership of "Family Budget"
        Then "bob" should be able to manage workspace users
        And "bob" should have all the same capabilities I have

    Scenario: Owner reduces someone's permissions
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        And "bob" can edit "Family Budget"
        When I change "bob" to view-only access
        Then "bob" can still see the data
        But "bob" can no longer make changes

Rule: Transferring Workspace Ownership
    Workspace owners can hand off ownership to someone else

    Scenario: Owner transfers full control to another person
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        And "bob" can edit "Family Budget"
        When I transfer ownership of "Family Budget" to "bob"
        Then "bob" should become the owner
        And I should still have editing access
        And "bob" should be able to manage users
        And I should no longer be able to add or remove users

Rule: Setting Preferred Workspace
    Users can choose which workspace to use by default

    Scenario: User sets their preferred starting workspace
        Given I am logged in as "bob"
        And I have access to these workspaces:
            | Workspace Name   | My Role |
            | Bob's Personal   | Owner   |
            | Family Budget    | Editor  |
            | Shared Vacation  | Viewer  |
        And "Bob's Personal" is currently my default
        When I set "Family Budget" as my preferred workspace
        Then "Family Budget" should be selected the next time I log in
        And my preferences should show "Family Budget" as default

Rule: Switching Between Workspaces
    Users can easily move between their different workspaces

    Scenario: User switches between workspaces they have access to
        Given I am logged in as "bob"
        And I have access to these workspaces:
            | Workspace Name   | My Role |
            | Bob's Personal   | Owner   |
            | Family Budget    | Editor  |
            | Shared Vacation  | Viewer  |
        When I open the workspace selector
        Then I should see all my workspaces with my role in each
        And when I select "Family Budget"
        Then I should be working in "Family Budget"
        And I should see options appropriate for an editor
        And I should only see "Family Budget" transactions

Rule: Viewing Workspace Members
    Workspace owners can see who has access and manage their permissions

    Scenario: Owner views everyone who has access
        Given I am logged in as "alice"
        And I own a workspace called "Family Budget"
        And these people have access:
            | Username | Role   |
            | alice    | Owner  |
            | bob      | Editor |
            | charlie  | Viewer |
        When I view who has access to "Family Budget"
        Then I should see all 3 people
        And I should see what each person can do
        And I should see options to manage their access

    Scenario: Editor cannot see or manage workspace members
        Given I am logged in as "bob"
        And I can edit "Family Budget"
        When I try to view the workspace members
        Then I should not see member management options

Rule: Understanding Permission Capabilities
    Different permission levels allow different actions

    Scenario: Editor understands what they can and cannot do
        Given I am logged in as "bob"
        And I can edit "Family Budget"
        When I work with "Family Budget"
        Then I can add new transactions
        And I can change existing transactions
        And I can organize with budget categories
        But I cannot invite others
        And I cannot change anyone's permissions
        And I cannot delete the workspace

Rule: Deleting Shared Workspaces
    When an owner deletes a workspace, all collaborators are affected

    Scenario: Owner deletes workspace that others are using
        Given I am logged in as "alice"
        And I own a workspace called "Old Workspace"
        And "bob" can edit "Old Workspace"
        And "charlie" can view "Old Workspace"
        And "Old Workspace" has financial data in it
        When I request to delete "Old Workspace"
        Then I should be warned that all data will be lost permanently
        And I should be warned that others will lose access
        And when I confirm the deletion
        Then "Old Workspace" should be completely removed
        And "bob" and "charlie" should lose access
        And all the financial data should be deleted
        And everyone affected should be notified
