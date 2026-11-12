@category:feature-2205
@category:payee-rules
Feature: Payee Matching Rules
  As a YoFi user
  I want to set up payee matching rules
  So that my transactions are automatically categorized when I import them

  Background:
    Given the application is running
    And I have an existing account
    And I have an active workspace "My Finances"
    And I am logged into my existing account

  @category:story-2211
  Rule: Users can establish and manage payee matching rules

    @pri:1
    @id:1
    Scenario: User creates a basic payee matching rule with category
      When I create a payee rule:
        | Payee     | Category |
        | Starbucks | Coffee   |
      Then I should see the rule for "Starbucks" with category "Coffee"

    @pri:1
    @id:3
    Scenario: User edits an existing payee rule
      Given I have a payee rule:
        | Payee       | Category  |
        | Coffee Shop | Beverages |
      When I edit the rule to:
        | Payee  | Category     |
        | Coffee | Coffee & Tea |
      Then I should see the rule for "Coffee" with category "Coffee & Tea"

    @pri:2
    @id:4
    Scenario: User deletes a payee matching rule
      Given I have a payee rule:
        | Payee      | Category |
        | Old Vendor | Misc     |
      When I delete the rule for "Old Vendor"
      Then the rule should no longer exist

    @pri:1
    @id:5
    Scenario: User views list of all payee matching rules
      Given I have the following payee rules:
        | Payee       | Category        | IsRegex |
        | Starbucks   | Coffee          | false   |
        | Shell       | Gas & Fuel      | false   |
        | ^AMZN.*MKTP | Online Shopping | true    |
      When I view my payee rules
      Then I should see the expected rules

    @pri:3
    @id:8
    Scenario: User searches across both payee and category
      Given I have the following payee rules:
        | Payee          | Category   |
        | Shell          | Gas        |
        | Gasoline Alley | Dining Out |
        | BP             | Fuel       |
      When I search for rules matching "Gas"
      Then the following rules should be shown:
        | Payee          |
        | Shell          |
        | Gasoline Alley |

    @pri:1
    @id:11
    Scenario: Cannot create rule with empty category
      When I attempt to create a payee rule:
        | Payee      | Category |
        | Test Payee |          |
      Then I should see an error message containing "Category is required"
      And the rule should not be created

    @category:story-2212
  Rule: Transactions are automatically categorized on bank import

    @pri:1
    @id:20
    Scenario: Transaction matches substring payee rule on import
      Given I have a payee rule:
        | Payee     | Category |
        | Starbucks | Coffee   |
      When I upload an OFX file with transactions:
        | Date       | Payee                 | Amount |
        | 2024-01-15 | STARBUCKS STORE #1234 | -5.50  |
      Then the imported transaction should have category "Coffee"
      And the category should be read-only in import review

    @pri:1
    @id:21
    Scenario: Transaction matches regex payee rule on import
      Given I have a regex payee rule:
        | Payee       | Category        |
        | ^AMZN.*MKTP | Online Shopping |
      When I upload an OFX file with transactions:
        | Date       | Payee                 | Amount |
        | 2024-01-20 | AMZN MKTP US*1Y3K8L9M | -49.99 |
      Then the imported transaction should have category "Online Shopping"

    @pri:1
    @id:22
    Scenario: Multiple transactions match different rules
      Given I have the following payee rules:
        | Payee     | Category |
        | Starbucks | Coffee   |
        | Shell     | Gas      |
      When I upload an OFX file with transactions:
        | Date       | Payee              | Amount |
        | 2024-01-15 | STARBUCKS DOWNTOWN | -6.50  |
        | 2024-01-16 | SHELL GAS STATION  | -45.00 |
        | 2024-01-17 | WHOLE FOODS MARKET | -89.99 |
      Then the categories should be:
        | Payee              | Category |
        | STARBUCKS DOWNTOWN | Coffee   |
        | SHELL GAS STATION  | Gas      |
        | WHOLE FOODS MARKET |          |

    @pri:1
    @id:25
    Scenario: Matched category persists after import
      Given I have a payee rule:
        | Payee | Category   |
        | Shell | Gas & Fuel |
      And I have uploaded an OFX file with transactions:
        | Date       | Payee             | Amount |
        | 2024-01-15 | SHELL GAS STATION | -50.00 |
      When I import the selected transactions
      And I view my transactions
      Then the transaction should have category "Gas & Fuel"

    @category:story-2212
  Rule: Matching rules handle conflicts with defined precedence

    @pri:2
    @id:26
    Scenario: Longer substring rule wins over shorter
      Given I have the following payee rules:
        | Payee             | Category       |
        | Starbucks         | Coffee         |
        | Starbucks Reserve | Premium Coffee |
      When I upload an OFX file with transactions:
        | Date       | Payee                      | Amount |
        | 2024-01-15 | STARBUCKS RESERVE ROASTERY | -8.50  |
      Then the imported transaction should have category "Premium Coffee"

    @pri:2
    @id:27
    @hidden
    # Requires us to SET the Modified date for the payee rules explicitly
    # which we don't have test control capability for.
    Scenario: Most recently modified rule wins for equal length
      Given I have the following payee rules created in this order:
        | Payee  | Category  | Modified   |
        | Coffee | Beverages | 2024-01-01 |
        | Coffee | Cafe      | 2024-01-15 |
      When I upload an OFX file with transactions:
        | Date       | Payee            | Amount |
        | 2024-01-20 | Coffee Bean Shop | -6.00  |
      Then the imported transaction should have category "Cafe"

    @pri:2
    @id:28
    Scenario: Regex rule wins over substring rule
      Given I have the following payee rules:
        | Payee   | Category        | IsRegex |
        | Amazon  | Shopping        | false   |
        | ^AMZN.* | Online Shopping | true    |
      When I upload an OFX file with transactions:
        | Date       | Payee            | Amount |
        | 2024-01-15 | AMZN MKTP US*123 | -29.99 |
      Then the imported transaction should have category "Online Shopping"

    # NOTE: We have to escape the backslash in the regex pattern so the generated C# string is valid.
    # Better would be for the generator to escape that automatically.

    @pri:3
    @id:29
    Scenario: Regex rule matches exact pattern
      Given I have a regex payee rule:
        | Payee           | Category |
        | ^SHELL #\\d{4}$ | Gas      |
      When I upload an OFX file with transactions:
        | Date       | Payee       | Amount |
        | 2024-01-15 | SHELL #1234 | -40.00 |
        | 2024-01-16 | SHELL STORE | -45.00 |
      Then the categories should be:
        | Payee       | Category |
        | SHELL #1234 | Gas      |
        | SHELL STORE |          |
