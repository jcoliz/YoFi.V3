@category:2207
@explicit:wip
Feature: Reports
  As a YoFi user
  I want to view summaries of my financial activity
  So that I can understand my income, expenses, and savings

  Detailed aggregation, filtering, and ordering rules are covered by lower-level tests.
  These scenarios check representative reports and selection persistence end to end.

  Background:
    Given the application is running
    And I am logged in as a user with "Editor" role
    # The existing seeding step dates transactions today, keeping them in the default report year.
    And I have a workspace with a transaction:
      | Payee           | Amount  | Category                |
      | Employer        | 1000.00 | Income:Salary           |
      | Bank            | 50.00   | Income:Interest         |
      | Supermarket     | -60.00  | Food:Groceries          |
      | Restaurant      | -40.00  | Food:Dining             |
      | Utility         | -100.00 | Home:Utilities          |
      | Savings Deposit | -200.00 | Savings:Emergency:Bank  |
      | Retirement      | -300.00 | Savings:Retirement:Fund |

  @category:2248
  Rule: Users can run built-in reports

    @pri:1
    @id:1
    Scenario Outline: View a representative financial report
      When user visits the "Reports" page
      Then page heading is "Reports"
      And the selected report should be "Expenses"
      When I select the <Report> report
      Then the displayed report should be <Report>

      # Gherkin.Generator issue #39 to support dynamic category values
      #And the report should include these categories:
      #  | Category   |
      #  | <Category> |

      # Until that's done, we will work around it
      And the report should include the category <Category>

      And the report grand total should be <Total>
      And the report should show <Periods> amounts

      Examples:
        | Report          | Category  | Total   | Periods |
        | Income          | Salary    | 1050.00 | annual  |
        | Expenses Detail | Groceries | -200.00 | monthly |
        | Savings Detail  | Bank      | -500.00 | annual  |

    @pri:1
    @id:2
    Scenario: Remember the selected report after reloading the page
      When user visits the "Reports" page
      And I select the "Expenses Detail" report
      Then the displayed report should be "Expenses Detail"
      When I reload the reports page
      Then the selected report should be "Expenses Detail"
      And the displayed report should be "Expenses Detail"
      And the report should include these categories:
        | Category  |
        | Food      |
        | Groceries |
        | Dining    |
      And the report grand total should be -200.00

    @pri:1
    @id:3
    Scenario: All Detail includes every category family in the workspace
      When user visits the "Reports" page
      And I select the "All Detail" report
      Then the displayed report should be "All Detail"
      And the report should include these categories:
        | Category   |
        | Income     |
        | Salary     |
        | Interest   |
        | Food       |
        | Groceries  |
        | Dining     |
        | Home       |
        | Utilities  |
        | Savings    |
        | Emergency  |
        | Retirement |
      And the report grand total should be 350.00
      And the report should show monthly amounts
