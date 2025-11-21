@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:AuthenticationSteps
@template:Features/FunctionalTest.mustache
@hook:before-first-then:SaveScreenshot
@explicit
Feature: User Authentication
    As a user of YoFi
    I want to register, login, and manage my account
    So that I can securely access my financial data

Background:
    Given the application is running
    And I am not logged in

Scenario: User registers for a new account
    Given I am on the registration page
    When I enter valid registration details
    And I submit the registration form
    Then My registration request should be acknowledged

Scenario: User logs into an existing account
    Given I have an existing account
    And I am on the login page
    When I enter my credentials
    And I click the login button
    Then I should see the home page
    And I should be successfully logged in

Scenario: User login fails with invalid credentials
    Given I am on the login page
    When I enter invalid credentials:
        | Field    | Value                 |
        | Email    | baduser@example.com   |
        | Password | WrongPassword123!     |
    And I click the login button
    Then I should see an error message "Invalid email or password"
    And I should remain on the login page
    And I should not be logged in

Scenario: User login fails with missing password
    Given I am on the login page
    When I enter only an email address "testuser@example.com"
    And I leave the password field empty
    And I click the login button
    Then I should see a validation error "Password is required"
    And I should not be logged in

Scenario: User views their account details
    Given I am logged in as "testuser@example.com"
    And I am on any page in the application
    When I navigate to my profile page
    Then I should see my account information:
        | Field    | Value                |
        | Email    | testuser@example.com |
        | Username | testuser             |
    And I should see options to update my profile
    And I should see my current workspace information

Scenario: User logs out successfully
    Given I am logged in as "testuser@example.com"
    And I am viewing my workspace dashboard
    When I click the logout button
    Then I should be logged out
    And I should be redirected to the home page
    And I should see the login option in the navigation
    And I should not see any personal information

Scenario: User registration fails with weak password
    Given I am on the registration page
    When I enter registration details with a weak password:
        | Field            | Value               |
        | Email            | newuser@example.com |
        | Password         | 123                 |
        | Confirm Password | 123                 |
    And I submit the registration form
    Then I should see a validation error about password requirements
    And I should remain on the registration page
    And I should not be registered

Scenario: User registration fails with mismatched passwords
    Given I am on the registration page
    When I enter registration details with mismatched passwords:
        | Field            | Value                    |
        | Email            | newuser@example.com      |
        | Password         | SecurePassword123!       |
        | Confirm Password | DifferentPassword123!    |
    And I submit the registration form
    Then I should see a validation error "Passwords do not match"
    And I should remain on the registration page
    And I should not be registered

Scenario: User registration fails with existing email
    Given an account already exists with email "existing@example.com"
    And I am on the registration page
    When I enter registration details:
        | Field            | Value                    |
        | Email            | existing@example.com     |
        | Password         | SecurePassword123!       |
        | Confirm Password | SecurePassword123!       |
    And I submit the registration form
    Then I should see an error message "An account with this email already exists"
    And I should remain on the registration page
    And I should not be registered

Scenario: Logged in user cannot access login page
    Given I am logged in as "testuser@example.com"
    When I try to navigate to the login page
    Then I should be automatically redirected to my workspace dashboard
    And I should not see the login form

Scenario: Anonymous user cannot access protected pages
    Given I am not logged in
    When I try to navigate to a protected page like "/workspace/dashboard"
    Then I should be redirected to the login page
    And I should see a message indicating I need to log in
    And after logging in, I should be redirected to the originally requested page
