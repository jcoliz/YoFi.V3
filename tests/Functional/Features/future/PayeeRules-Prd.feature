Feature: Payee Matching Rules
  As a YoFi user
  I want to set up payee matching rules
  So that my transactions are automatically categorized when I import them

  Background:
    Given the application is running
    And I have an existing account
    And I have an active workspace "My Finances"
    And I am logged into my existing account

  @story:1
  Rule: Users can establish and manage payee matching rules

    @pri:1
    @id:1
    Scenario: User creates a basic payee matching rule with category
      When I create a payee rule:
        | Payee     | Category |
        | Starbucks | Coffee   |
      Then I should see the rule for "Starbucks" with category "Coffee"

    @pri:2
    @id:2
    Scenario: User creates a regex-based payee matching rule
      When I create a regex payee rule:
        | Payee        | Category        |
        | ^AMZN.*MKTP  | Online Shopping |
      Then I should see the rule for "^AMZN.*MKTP" with category "Online Shopping"
      And the rule should be marked as using regex

    @pri:1
    @id:3
    Scenario: User edits an existing payee rule
      Given I have a payee rule:
        | Payee       | Category  |
        | Coffee Shop | Beverages |
      When I edit the rule to:
        | Payee  | Category      |
        | Coffee | Coffee & Tea  |
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
        | Payee        | Category        | IsRegex |
        | Starbucks    | Coffee          | false   |
        | Shell        | Gas & Fuel      | false   |
        | ^AMZN.*MKTP  | Online Shopping | true    |
      When I view my payee rules
      Then I should see the expected rules

    @pri:3
    @id:6
    Scenario: User sorts payee rules by category
      Given I have the following payee rules:
        | Payee      | Category   |
        | Target     | Shopping   |
        | Shell      | Gas        |
        | Starbucks  | Coffee     |
      When I view my payee rules sorted by category
      Then the rules should appear in this order:
        | Category |
        | Coffee   |
        | Gas      |
        | Shopping |

    @pri:2
    @id:7
    Scenario: User searches for payee rules
      Given I have the following payee rules:
        | Payee        | Category |
        | Starbucks    | Coffee   |
        | Shell        | Gas      |
        | Amazon       | Shopping |
      When I search for rules matching "Coffee"
      Then I should see 1 rule in the results
      And the result should be:
        | Payee     |
        | Starbucks |

    @pri:3
    @id:8
    Scenario: User searches across both payee and category
      Given I have the following payee rules:
        | Payee     | Category |
        | Shell     | Gas      |
        | Gas Mart  | Gas      |
        | BP        | Fuel     |
      When I search for rules matching "Gas"
      Then I should see 2 rules in the results
      And the results should be:
        | Payee    |
        | Shell    |
        | Gas Mart |

    @pri:2
    @id:9
    Scenario: User creates rule from existing transaction
      Given I have a transaction:
        | Payee                         | Category |
        | Whole Foods Market Downtown   |          |
      When I create a rule based on that transaction:
        | Payee       | Category  |
        | Whole Foods | Groceries |
      Then I should see the rule for "Whole Foods" with category "Groceries"

    @pri:3
    @id:10
    Scenario: User creates rule from categorized transaction
      Given I have a transaction:
        | Payee             | Category        |
        | Shell Gas Station | Automotive:Fuel |
      When I create a rule based on that transaction
      Then I should have a new rule:
        | Payee             | Category        |
        | Shell Gas Station | Automotive:Fuel |

    @pri:1
    @id:11
    Scenario: Cannot create rule with empty category
      When I attempt to create a payee rule:
        | Payee      | Category |
        | Test Payee |          |
      Then I should see a validation error "Category is required"
      And the rule should not be created

    @pri:2
    @id:12
    Scenario: Invalid regex pattern shows error message
      When I attempt to create a regex payee rule:
        | Payee      | Category |
        | [invalid(  | Test     |
      Then I should see a validation error containing "Invalid regular expression"
      And the error should include details about the regex error
      And the rule should not be created

    @pri:4
    @id:13
    Scenario: Regex pattern with ReDoS vulnerability is rejected
      When I attempt to create a regex payee rule:
        | Payee  | Category |
        | (a+)+  | Test     |
      Then I should see a validation error containing "timeout"
      And the rule should not be created

    @pri:3
    @id:14
    Scenario: Category is automatically normalized
      When I create a payee rule:
        | Payee       | Category                |
        | Coffee Shop | "  Coffee : Beverages  " |
      Then I should see the rule with category "Coffee:Beverages"

  @story:1
  Rule: Users with different roles have different permissions for payee rules

    @pri:2
    @id:15
    Scenario: Editor role can manage payee rules
      Given "bob" can edit data in "My Finances"
      And I switched to user "bob"
      When I view my payee rules
      Then I should be able to create new rules
      And I should be able to edit existing rules
      And I should be able to delete rules

    @pri:3
    @id:16
    Scenario: Viewer role can only view payee rules
      Given I have a payee rule:
        | Payee | Category      |
        | Test  | Test Category |
      And "charlie" can view data in "My Finances"
      And I switched to user "bob"
      When I view my payee rules
      Then I should see the rules list
      And I should not be able to create new rules
      And I should not be able to edit rules
      And I should not be able to delete rules

    @pri:4
    @id:17
    Scenario: Viewer role cannot access rule creation
      Given "charlie" can view data in "My Finances"
      And I switched to user "charlie"
      When I attempt to create a new rule
      Then I should see a permission error message
      And I should not be able to proceed

  @story:1
  Rule: Payee rules are scoped to the tenant

    @pri:2
    @id:18
    Scenario: Cannot access other tenants' payee rules
      Given I have a payee rule:
        | Payee   | Category    |
        | My Rule | My Category |
      And "bob" owns a workspace called "Bob's Finances"
      When I switch to user "bob"
      And I view my payee rules
      Then I should not see the rule for "My Rule"
      And the rules list should be empty

    @pri:3
    @id:19
    Scenario: Payee rules are shared within workspace
      Given I have a payee rule:
        | Payee       | Category |
        | Shared Rule | Shared   |
      And "bob" can edit data in "My Finances"
      When I switch to user "bob"
      And I view my payee rules
      Then I should see the rule for "Shared Rule"

  @story:2
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

    @pri:3
    @id:23
    Scenario: Transaction without matching rule has no category
      Given I have a payee rule:
        | Payee     | Category |
        | Starbucks | Coffee   |
      When I upload an OFX file with transactions:
        | Date       | Payee         | Amount |
        | 2024-01-15 | Random Vendor | -25.00 |
      Then the imported transaction should not have a category

    @pri:3
    @id:24
    Scenario: User cannot edit category in import review
      Given I have a payee rule:
        | Payee  | Category  |
        | Coffee | Beverages |
      And I have uploaded an OFX file with a transaction matching "Coffee Shop"
      When I view the import review
      Then the category field should be read-only
      And I should not be able to change the category

    @pri:1
    @id:25
    Scenario: Matched category persists after import
      Given I have a payee rule:
        | Payee | Category    |
        | Shell | Gas & Fuel  |
      And I have uploaded an OFX file with transactions:
        | Date       | Payee             | Amount |
        | 2024-01-15 | SHELL GAS STATION | -50.00 |
      When I import the selected transactions
      And I view my transactions
      Then the transaction should have category "Gas & Fuel"

  @story:2
  Rule: Matching rules handle conflicts with defined precedence

    @pri:2
    @id:26
    Scenario: Longer substring rule wins over shorter
      Given I have the following payee rules:
        | Payee             | Category       |
        | Starbucks         | Coffee         |
        | Starbucks Reserve | Premium Coffee |
      When I upload an OFX file with transactions:
        | Date       | Payee                       | Amount |
        | 2024-01-15 | STARBUCKS RESERVE ROASTERY  | -8.50  |
      Then the imported transaction should have category "Premium Coffee"

    @pri:2
    @id:27
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

    @pri:3
    @id:29
    Scenario: Regex rule matches exact pattern
      Given I have a regex payee rule:
        | Payee          | Category |
        | ^SHELL #\d{4}$ | Gas      |
      When I upload an OFX file with transactions:
        | Date       | Payee       | Amount |
        | 2024-01-15 | SHELL #1234 | -40.00 |
        | 2024-01-16 | SHELL STORE | -45.00 |
      Then the categories should be:
        | Payee       | Category |
        | SHELL #1234 | Gas      |
        | SHELL STORE |          |

  @story:2
  Rule: Payee matching is case-insensitive

    @pri:2
    @id:30
    Scenario: Substring matching is case-insensitive
      Given I have a payee rule:
        | Payee     | Category |
        | starbucks | Coffee   |
      When I upload an OFX file with transactions:
        | Date       | Payee              | Amount |
        | 2024-01-15 | STARBUCKS DOWNTOWN | -6.50  |
        | 2024-01-16 | Starbucks Store    | -5.75  |
        | 2024-01-17 | starbucks cafe     | -7.00  |
      Then all transactions should have category "Coffee"

    @pri:3
    @id:31
    Scenario: Regex matching is case-insensitive
      Given I have a regex payee rule:
        | Payee    | Category |
        | ^shell.* | Gas      |
      When I upload an OFX file with transactions:
        | Date       | Payee         | Amount |
        | 2024-01-15 | SHELL STATION | -40.00 |
        | 2024-01-16 | Shell Gas     | -42.00 |
        | 2024-01-17 | shell #1234   | -38.00 |
      Then all transactions should have category "Gas"

  @story:2
  Rule: Transactions already imported are not affected by new rules

    @pri:4
    @id:32
    Scenario: Existing transactions do not get categories from new rules
      Given I have 5 existing transactions:
        | Payee     | Category |
        | Starbucks |          |
      When I create a new payee rule:
        | Payee     | Category |
        | Starbucks | Coffee   |
      And I view my transactions
      Then the existing transactions should still have no category
      And only newly imported transactions should be categorized

  @story:2
  Rule: Import with payee rules respects tenant isolation

    @pri:3
    @id:33
    Scenario: Rules do not apply to other tenants' imports
      Given I have a payee rule:
        | Payee  | Category  |
        | Coffee | Beverages |
      And "bob" owns a workspace called "Bob's Finances"
      When I switch to user "bob"
      And I upload an OFX file with transactions:
        | Date       | Payee       | Amount |
        | 2024-01-15 | Coffee Shop | -5.50  |
      Then the imported transaction should not have a category
