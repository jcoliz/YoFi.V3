@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:FunctionalTest
@template:Features/FunctionalTest.mustache
@before-then:SaveScreenshot
Feature: (Weather) Forecasts load and displays successfully

Scenario: Forecasts load OK
    Given user has launched site
    And user selected option "Weather" in nav bar
    Then page contains 5 forecasts
