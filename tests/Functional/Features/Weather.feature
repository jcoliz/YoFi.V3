@using:YoFi.V3.Tests.Functional.Helpers
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:YoFi.V3.Tests.Functional.Infrastructure.FunctionalTestBase
Feature: Weather Forecasts
    As a user planning my activities
    I want to view upcoming weather forecasts
    So that I can plan accordingly

Background:
    Given the application is running
    And I am logged in

Scenario: User views the weather forecast
    When I navigate to view the weather forecast
    Then I should see upcoming weather predictions
    And each forecast should show the date, temperature, and conditions

Scenario: Forecasts show both Celsius and Fahrenheit
    Given I am viewing weather forecasts
    Then each forecast should display temperature in both Celsius and Fahrenheit
    And the temperature conversions should be accurate

Scenario: Multi-day forecast is available
    Given I am viewing weather forecasts
    Then I should see forecasts for at least the next 5 days
    And forecasts should be ordered chronologically
