@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:AuthenticationSteps
@template:Features/FunctionalTest.mustache
Feature: User Authentication
    As a user of YoFi
    I want to register, login, and manage my account
    So that I can securely access my financial data

Background:
    Given the application is running
    And I am not logged in

Rule: User Registration
    Users can create new accounts with valid credentials

    Scenario: User registers for a new account
        Given I am on the registration page
        When I enter valid registration details
        And I submit the registration form
        Then My registration request should be acknowledged

    Scenario: User registration fails with weak password
        Given I am on the registration page
        When I enter registration details with a weak password
        And I submit the registration form
        Then I should see an error message containing "Passwords must be"
        And I should remain on the registration page
        And I should not be registered

    Scenario: User registration fails with mismatched passwords
        Given I am on the registration page
        When I enter registration details with mismatched passwords
        And I submit the registration form (for validation)
        Then I should see an error message containing "Passwords do not match"
        And I should remain on the registration page
        And I should not be registered

    Scenario: User registration fails with existing email
        Given I have an existing account
        And I am on the registration page
        When I enter registration details with the existing email
        And I submit the registration form
        Then I should see an error message containing "is already taken"
        And I should remain on the registration page
        And I should not be registered

Rule: User Login and Logout
    Users can authenticate and end their sessions

    Scenario: User logs into an existing account
        Given I have an existing account
        And I am on the login page
        When I enter my credentials
        And I click the login button
        Then I should see the home page
        And I should be successfully logged in

    Scenario: User login fails with invalid credentials
        Given I am on the login page
        When I enter invalid credentials
        And I click the login button
        Then I should see an error message containing "Invalid credentials"
        And I should remain on the login page

    Scenario: User login fails with missing password
        Given I am on the login page
        When I enter only a username
        And I leave the password field empty
        And I click the login button (for validation)
        Then I should see a validation error
        And I should not be logged in

    Scenario: User logs out successfully
        Given I am logged in
        And I am viewing my profile page
        When I click the logout button
        Then I should be logged out
        And I should be redirected to the home page
        And I should see the login option in the navigation
        And I should not see any personal information

Rule: Account Management
    Authenticated users can view their profile

    Scenario: User views their account details
        Given I am logged in
        And I am on any page in the application
        When I navigate to my profile page
        Then I should see my account information

Rule: Access Control
    The system enforces authentication requirements for protected resources

    Scenario: Logged in user cannot access login page
        Given I am logged in
        When I try to navigate directly to the login page
        Then I should be redirected to my profile page
        And I should not see the login form

    Scenario Outline: Anonymous user cannot access protected pages
        Given I am not logged in
        When I try to navigate directly to a protected page like <page>
        Then I should be redirected to the login page
        And I should see a message indicating I need to log in
        And after logging in, I should be redirected to the originally requested page

        Examples:
            | page     |
            | /weather |
            | /counter |
            | /about   |
            | /profile |
