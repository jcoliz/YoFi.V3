@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:FunctionalTest
@template:Features/FunctionalTest.mustache
@hook:before-first-then:SaveScreenshot
Feature: About Page
    As a site administrator
    I want to view application version and configuration information
    So that I can verify the deployment and troubleshoot issues

Background:
    Given the application is running
    And I am logged in as an administrator

Scenario: Administrator views version information
    When I navigate to the About page
    Then I should see the version information card
    And I should see the front end version
    And I should see the back end version
    And front end and back end versions should match

Scenario: Administrator views API configuration
    Given I am on the About page
    Then I should see the API base URL
    And the API base URL should be properly configured

Scenario: User without admin role cannot access About page
    Given I am logged in as a regular user
    When I attempt to navigate to the About page
    Then I should see an access denied message
    And I should be redirected to the home page
