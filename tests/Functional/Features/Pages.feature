@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:FunctionalTest
@template:Features/FunctionalTest.mustache
@before-then:SaveScreenshot
Feature: (Pages) All pages load and display successfully

The idea here is one test per site page. We are not testing functionality. 
We just want it to load, and take a nice screen shot. In the future, this could be 
turned into an image-compare tests where we make sure the screen shots don't change.

Scenario: Root loads OK
    When user launches site
    Then page loaded ok

Scenario Outline: Every page loads OK
    Given user has launched site
    When user selects option <page> in nav bar
    Then page loaded ok
    And page heading is <page>
    And page title contains <page>

    Examples:
        | Home |
        | Counter |
        | Weather |
        | About |
