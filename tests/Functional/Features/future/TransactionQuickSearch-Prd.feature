Feature: Transaction Quick Text Search
  As a YoFi user
  I want to search transactions with one text input
  So that I can find transactions without choosing a field

  These future scenarios cover only Story 2213 and its focused design.
  They are specifications for later implementation, not currently executable tests.
  Priorities contain roughly equal numbers of scenarios:
  1 = core acceptance, 2 = common workflows, 3 = regression coverage,
  4 = less common edge cases and resilience.
  Scenario IDs are prefixed with the story number to avoid collisions with other features.
  Empty memo cells represent absent memos. Transaction fixtures contain no other
  transactions unless a scenario explicitly adds them.
  Regular transaction tables contain one row per transaction.
  Split transaction tables contain one row per split, grouped by Payee within
  the table. Repeated Amount and Memo values describe the parent transaction;
  Split Amount, Category, and Split Memo describe each split. Order is zero-based.
  Each fixture step creates its transactions rather than modifying earlier fixtures.
  PostgreSQL-tagged scenarios require the intended production locale/collation.

  Background:
    Given the application is running
    And I have an existing account
    And I have an active workspace "My Finances"
    And I am logged into my existing account

  @story:2213
  Rule: One search matches any supported transaction or split text field

    @pri:1
    @id:2213-001
    Scenario: User finds a payee with a case-insensitive substring
      Given I have the following transactions:
        | Payee                    | Amount | Category | Memo |
        | STARBUCKS STORE Downtown | -5.50  | Drinks   |      |
        | Grocery Store            | -25.00 | Food     |      |
      When I search transactions for "bucks"
      Then the search results should contain exactly these payees:
        | Payee                    |
        | STARBUCKS STORE Downtown |

    @pri:1
    @id:2213-002
    Scenario: User finds a transaction by its memo
      Given I have the following transactions:
        | Payee       | Amount | Category | Memo                   |
        | Office Shop | -18.00 | Supplies | Reimbursable stationery |
        | Other Shop  | -19.00 | Supplies |                        |
      When I search transactions for "reimbursable"
      Then the search results should contain exactly these payees:
        | Payee       |
        | Office Shop |

    @pri:1
    @id:2213-003
    Scenario: User finds a transaction by its split category
      Given I have the following transactions:
        | Payee       | Amount | Category         | Memo |
        | Supermarket | -60.00 | Food:Groceries   |      |
        | Fuel Stop   | -40.00 | Automotive:Fuel  |      |
      When I search transactions for "grocer"
      Then the search results should contain exactly these payees:
        | Payee       |
        | Supermarket |

    @pri:1
    @id:2213-004
    Scenario: User finds a transaction by a memo on a non-first split
      Given I have the following transactions:
        | Payee       | Amount |
        | Other Store | -20.00 |
      And I have the following split transactions:
        | Payee            | Amount  | Memo | Order | Split Amount | Category | Split Memo       |
        | Department Store | -100.00 |      | 0     | -60.00       | Home     |                  |
        | Department Store | -100.00 |      | 1     | -40.00       | Gifts    | Birthday present |
      When I search transactions for "birthday"
      Then the search results should contain exactly these payees:
        | Payee            |
        | Department Store |

    @pri:2
    @id:2213-005
    Scenario: Several matching splits return one transaction
      Given I have the following split transactions:
        | Payee        | Amount  | Memo | Order | Split Amount | Category       | Split Memo |
        | Mixed Basket | -100.00 |      | 0     | -20.00       | Home           |            |
        | Mixed Basket | -100.00 |      | 1     | -50.00       | Food:Groceries |            |
        | Mixed Basket | -100.00 |      | 2     | -30.00       | Food:Dining    |            |
      When I search transactions for "food"
      Then the search results should contain exactly these payees:
        | Payee        |
        | Mixed Basket |
      And the matching transaction total should be 1

    @pri:2
    @id:2213-006
    Scenario: Search combines text fields with OR rather than AND
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo          |
        | Coffee House | -5.00  | Drinks   |               |
        | Market       | -6.00  | Coffee   |               |
        | Office       | -7.00  | Supplies | Coffee meeting |
        | Fuel Stop    | -8.00  | Fuel     |               |
      When I search transactions for "coffee"
      Then the search results should contain exactly these payees:
        | Payee        |
        | Coffee House |
        | Market       |
        | Office       |

    @pri:3
    @id:2213-007
    Scenario: Search treats a multiword term as one literal phrase
      Given I have the following transactions:
        | Payee               | Amount | Category | Memo |
        | Downtown Coffee Shop | -5.00 | Drinks   |      |
        | Coffee Supply Shop  | -6.00  | Drinks   |      |
      When I search transactions for "coffee shop"
      Then the search results should contain exactly these payees:
        | Payee                |
        | Downtown Coffee Shop |

    @pri:3
    @id:2213-008
    Scenario: Search wildcard punctuation and escape characters are literal
      Given I have the following transactions:
        | Payee          | Amount | Category | Memo |
        | Deal%_*\Outlet | -5.00  | Offers   |      |
        | DealXYZOutlet  | -6.00  | Offers   |      |
        | c=food         | -7.00  | Other    |      |
        | Market         | -8.00  | Food     |      |
      When I search transactions for "%_*\\"
      Then the search results should contain exactly these payees:
        | Payee          |
        | Deal%_*\Outlet |

    @pri:3
    @id:2213-033
    Scenario: Filter-like text is searched literally
      Given I have the following transactions:
        | Payee  | Amount | Category | Memo |
        | c=food | -7.00  | Other    |      |
        | Market | -8.00  | Food     |      |
      When I search transactions for "c=food"
      Then the search results should contain exactly these payees:
        | Payee  |
        | c=food |

    @pri:4
    @id:2213-009
    @database:postgresql
    Scenario: Production search matches accented letter case without removing accents
      Given I have the following transactions:
        | Payee         | Amount | Category | Memo |
        | Corner café   | -5.00  | Drinks   |      |
        | Corner cafe   | -6.00  | Drinks   |      |
      When I search transactions for "CAFÉ"
      Then the search results should contain exactly these payees:
        | Payee       |
        | Corner café |

    @pri:4
    @id:2213-010
    Scenario: Search does not include source or external ID fields
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo | Source  | ExternalId | Date       |
        | Source Only  | -5.00  | Other    |      | needle  | bank-1     | 2026-04-01 |
        | ID Only      | -6.00  | Other    |      | Bank    | needle     | 2026-04-02 |
        | Text Match   | -7.00  | Other    | needle | Bank  | bank-3     | 2026-04-03 |
      When I search transactions for "needle"
      Then the search results should contain exactly these payees:
        | Payee      |
        | Text Match |

    @pri:4
    @id:2213-034
    Scenario: Transaction dates are not searchable text fields
      Given I have the following transactions:
        | Payee       | Amount | Date       |
        | Source Only | -5.00  | 2026-04-01 |
      When I search transactions for "2026-04-01"
      Then the matching transaction total should be 0

  @story:2213
  Rule: Amount searches accept familiar formatting and respect explicit signs

    @pri:1
    @id:2213-011
    Scenario: Unsigned amount search matches either sign but not partial amounts or split amounts
      Given I have the following transactions:
        | Payee       | Amount  | Category | Memo |
        | Positive    | 42.00   | Other    |      |
        | Negative    | -42.00  | Other    |      |
        | Larger      | 142.00  | Other    |      |
        | Fraction    | 42.50   | Other    |      |
      And I have the following split transactions:
        | Payee      | Amount  | Memo | Order | Split Amount | Category | Split Memo |
        | Split Only | -100.00 |      | 0     | -42.00       | Home     |            |
        | Split Only | -100.00 |      | 1     | -58.00       | Gifts    |            |
      When I search transactions for "42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |
        | Negative |

    @pri:2
    @id:2213-012
    Scenario: An explicit negative sign excludes positive amounts
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "-42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Negative |

    @pri:2
    @id:2213-035
    Scenario: An explicit positive sign excludes negative amounts
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "+42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |

    @pri:1
    @id:2213-013
    Scenario: An unsigned dollar-prefixed amount matches either sign
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "$42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |
        | Negative |

    @pri:2
    @id:2213-036
    Scenario: A negative sign before the dollar prefix restricts the amount match
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "-$42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Negative |

    @pri:2
    @id:2213-037
    Scenario: A negative sign after the dollar prefix restricts the amount match
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "$-42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Negative |

    @pri:2
    @id:2213-038
    Scenario: A positive sign after the dollar prefix restricts the amount match
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "$+42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |

    @pri:2
    @id:2213-039
    Scenario: A positive sign before the dollar prefix restricts the amount match
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo |
        | Positive | 42.00  | Other    |      |
        | Negative | -42.00 | Other    |      |
      When I search transactions for "+$42"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |

    @pri:1
    @id:2213-014
    Scenario: Comma-grouped amounts behave like ungrouped amounts
      Given I have the following transactions:
        | Payee    | Amount   | Category | Memo |
        | Positive | 1234.56  | Other    |      |
        | Negative | -1234.56 | Other    |      |
        | Other    | 1234.50  | Other    |      |
      When I search transactions for "1,234.56"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |
        | Negative |

    @pri:1
    @id:2213-040
    Scenario: A dollar-prefixed comma-grouped amount matches either sign
      Given I have the following transactions:
        | Payee    | Amount   | Category | Memo |
        | Positive | 1234.56  | Other    |      |
        | Negative | -1234.56 | Other    |      |
        | Other    | 1234.50  | Other    |      |
      When I search transactions for "$1,234.56"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Positive |
        | Negative |

    @pri:2
    @id:2213-041
    Scenario: An explicitly signed dollar-prefixed grouped amount restricts the sign
      Given I have the following transactions:
        | Payee    | Amount   | Category | Memo |
        | Positive | 1234.56  | Other    |      |
        | Negative | -1234.56 | Other    |      |
      When I search transactions for "$-1,234.56"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Negative |

    @pri:3
    @id:2213-015
    Scenario: Numeric search still matches the original text independently of amount sign
      Given I have the following transactions:
        | Payee       | Amount | Category | Memo                |
        | Amount Match | -42.00 | Other   |                     |
        | Opposite Sign | 42.00 | Other   |                     |
        | Text Match  | 10.00  | Other    | Refund reference -$42 |
        | Other       | 20.00  | Other    |                     |
      When I search transactions for "-$42"
      Then the search results should contain exactly these payees:
        | Payee        |
        | Amount Match |
        | Text Match   |

    @pri:3
    @id:2213-016
    Scenario: A repeated dollar prefix stays text-only
      Given I have the following transactions:
        | Payee          | Amount  | Category | Memo |
        | $$42 voucher   | 10.00   | Other    |      |
        | 1,,234 voucher | 11.00   | Other    |      |
        | 42,50 voucher  | 12.00   | Other    |      |
        | Forty Two      | 42.00   | Other    |      |
        | Thousand       | 1234.00 | Other    |      |
        | Four Thousand  | 4250.00 | Other    |      |
      When I search transactions for "$$42"
      Then the search results should contain exactly these payees:
        | Payee        |
        | $$42 voucher |
      And I should not see a search validation error

    @pri:3
    @id:2213-042
    Scenario: Malformed thousands grouping stays text-only
      Given I have the following transactions:
        | Payee          | Amount  | Category | Memo |
        | 1,,234 voucher | 11.00   | Other    |      |
        | Thousand       | 1234.00 | Other    |      |
      When I search transactions for "1,,234"
      Then the search results should contain exactly these payees:
        | Payee          |
        | 1,,234 voucher |
      And I should not see a search validation error

    @pri:3
    @id:2213-043
    Scenario: A decimal comma is not interpreted as a thousands separator
      Given I have the following transactions:
        | Payee         | Amount  | Category | Memo |
        | 42,50 voucher | 12.00   | Other    |      |
        | Four Thousand | 4250.00 | Other    |      |
      When I search transactions for "42,50"
      Then the search results should contain exactly these payees:
        | Payee         |
        | 42,50 voucher |
      And I should not see a search validation error

    @pri:4
    @id:2213-017
    Scenario: An overflowing number remains searchable text
      Given I have the following transactions:
        | Payee      | Amount | Category | Memo                                     |
        | Large Code | 10.00  | Other    | 9999999999999999999999999999999999999999 |
        | Exponent   | 11.00  | Other    | Code 1e2                                 |
        | Hundred    | 100.00 | Other    |                                          |
      When I search transactions for "9999999999999999999999999999999999999999"
      Then the search results should contain exactly these payees:
        | Payee      |
        | Large Code |
      And I should not see a search validation error

    @pri:4
    @id:2213-044
    Scenario: An exponent is searchable text rather than an amount
      Given I have the following transactions:
        | Payee    | Amount | Category | Memo     |
        | Exponent | 11.00  | Other    | Code 1e2 |
        | Hundred  | 100.00 | Other    |          |
      When I search transactions for "1e2"
      Then the search results should contain exactly these payees:
        | Payee    |
        | Exponent |
      And I should not see a search validation error

    @pri:4
    @id:2213-018
    Scenario: Zero amount matches without treating search as empty
      Given I have the following transactions:
        | Payee      | Amount | Category | Memo |
        | Adjustment | 0.00   | Other    |      |
        | Purchase   | -5.00  | Other    |      |
      When I search transactions for "$0.00"
      Then the search results should contain exactly these payees:
        | Payee      |
        | Adjustment |
      And the search input should contain "$0.00"
      And the clear search button should be visible

  @story:2213
  Rule: Search remains visible responsive and clearable

    @pri:1
    @id:2213-019
    Scenario: Search is available above the list in an empty workspace
      Given my workspace has no transactions
      When I view my transactions
      Then I should see one input labelled "Search transactions" above the date controls
      And the search input should be enabled once the page is ready
      And the clear search button should not be visible

    @pri:1
    @id:2213-045
    Scenario: An empty workspace with an active search shows search-specific guidance
      Given my workspace has no transactions
      And I am viewing my transactions
      When I search transactions for "missing"
      Then I should see "No transactions match your search."
      And I should see "Try another search or clear the search."
      And I should not see "Create your first transaction"
      And the search input should remain visible

    @pri:1
    @id:2213-020
    Scenario: Typing does not send a request before the debounce expires
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And I am viewing my transactions
      When I type "cof" into the search input without pausing for 300 milliseconds
      Then no search request should have been sent for that typing

    @pri:1
    @id:2213-046
    Scenario: Pausing after typing sends one search request for the final term
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And I am viewing my transactions
      And I have typed "cof" into the search input without pausing for 300 milliseconds
      When I finish typing "coffee" into the search input
      And I pause for 300 milliseconds
      Then exactly one search request should have been sent for "coffee"
      And the search results should contain exactly these payees:
        | Payee        |
        | Coffee House |
      And I should not have needed to submit the search

    @pri:1
    @id:2213-021
    Scenario: Clearing search immediately restores results and input focus
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo |
        | Coffee House | -5.00  | Drinks   |      |
        | Grocery Store | -20.00 | Food    |      |
      And I have searched transactions for "coffee"
      When I click the clear search button
      Then the search input should be empty and focused
      And the clear search button should not be visible
      And an unfiltered search request should be sent without waiting for the debounce
      And the search results should contain exactly these payees:
        | Payee         |
        | Coffee House  |
        | Grocery Store |

    @pri:2
    @id:2213-022
    Scenario: Enter immediately applies a pending search once
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And I am viewing my transactions
      When I type "coffee" into the search input
      And I press Enter before the debounce expires
      Then exactly one search request should be sent immediately for "coffee"
      And the search results should contain exactly these payees:
        | Payee        |
        | Coffee House |
      And no additional search request should be sent when the original debounce would expire
      And the page should not navigate or submit a form

    @pri:3
    @id:2213-023
    Scenario: Outer whitespace is ignored for matching but retained in the input
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo |
        | Coffee House | -5.00  | Drinks   |      |
        | Grocery Store | -20.00 | Food    |      |
      When I enter the search text between delimiters "|  coffee  |"
      And I pause for 300 milliseconds
      Then the search input should retain the text between those delimiters
      And the search results should contain exactly these payees:
        | Payee        |
        | Coffee House |

    @pri:3
    @id:2213-047
    Scenario: Whitespace-only search has no restriction
      Given I have the following transactions:
        | Payee         | Amount | Category | Memo |
        | Coffee House  | -5.00  | Drinks   |      |
        | Grocery Store | -20.00 | Food     |      |
      When I enter the search text between delimiters "|   |"
      And I pause for 300 milliseconds
      Then the matching transaction total should be 2
      And the clear search button should be visible
      And I should not see a search validation error

    @pri:3
    @id:2213-024
    Scenario: Manually deleting search text hides clear without requesting before debounce
      Given I have the following transactions:
        | Payee         | Amount |
        | Coffee House  | -5.00  |
        | Grocery Store | -20.00 |
      And I have searched transactions for "coffee"
      When I delete all text from the search input
      Then the clear search button should not be visible
      And no new search request should be sent before the debounce expires

    @pri:3
    @id:2213-048
    Scenario: Manually deleted search text restores all matches when the debounce expires
      Given I have the following transactions:
        | Payee         | Amount |
        | Coffee House  | -5.00  |
        | Grocery Store | -20.00 |
      And I have searched transactions for "coffee"
      And I have just deleted all text from the search input
      When I pause for 300 milliseconds
      Then the matching transaction total should be 2

    @pri:4
    @id:2213-025
    Scenario: Search remains usable at mobile width during loading
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And I am viewing my transactions at a viewport width of 375 pixels
      And the next search response is delayed
      When I search transactions for "coffee"
      Then the search input should remain visible and editable while loading
      And the search row should fit without horizontal scrolling
      And no filter modal should open

    @pri:4
    @id:2213-049
    Scenario: User can edit the search during loading at mobile width
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And I am viewing my transactions at a viewport width of 375 pixels
      And a search request for "coffee" is still loading
      When I replace the search input with "grocery" while loading
      Then the search input should contain "grocery"

    @pri:4
    @id:2213-026
    Scenario: An old response during the debounce cannot overwrite a newer search
      Given I have the following transactions:
        | Payee         | Amount |
        | Coffee House  | -5.00  |
        | Grocery Store | -20.00 |
      And the search response for "coffee" is held pending
      When I search transactions for "coffee"
      And I replace the search input with "grocery"
      And the old "coffee" response arrives before the new debounce expires
      Then the coffee results should not be presented as results for "grocery"

    @pri:4
    @id:2213-050
    Scenario: The newer search settles correctly after an old response arrives during debounce
      Given I have the following transactions:
        | Payee         | Amount |
        | Coffee House  | -5.00  |
        | Grocery Store | -20.00 |
      And a search response for "coffee" was held pending
      And I replaced the search input with "grocery"
      And the old "coffee" response arrived before the new debounce expired
      When I pause for 300 milliseconds
      And the search response for "grocery" succeeds
      Then the search results should contain exactly these payees:
        | Payee         |
        | Grocery Store |
      And the loading indicator should no longer be visible

  @story:2213
  Rule: Search preserves existing date pagination workspace and error behavior

    @pri:2
    @id:2213-027
    Scenario: Search and existing inclusive date bounds work together
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo | Date       |
        | Coffee Start | -5.00  | Drinks   |      | 2026-04-01 |
        | Coffee End   | -6.00  | Drinks   |      | 2026-04-30 |
        | Coffee Old   | -7.00  | Drinks   |      | 2026-03-31 |
        | Grocery      | -8.00  | Food     |      | 2026-04-15 |
      And my date range is from "2026-04-01" to "2026-04-30"
      When I search transactions for "coffee"
      Then the search results should contain exactly these payees:
        | Payee        |
        | Coffee Start |
        | Coffee End   |

    @pri:2
    @id:2213-051
    Scenario: Clearing search preserves the existing date range
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo | Date       |
        | Coffee Start | -5.00  | Drinks   |      | 2026-04-01 |
        | Coffee End   | -6.00  | Drinks   |      | 2026-04-30 |
        | Coffee Old   | -7.00  | Drinks   |      | 2026-03-31 |
        | Grocery      | -8.00  | Food     |      | 2026-04-15 |
      And my date range is from "2026-04-01" to "2026-04-30"
      And I have searched transactions for "coffee"
      When I click the clear search button
      Then my date range should remain from "2026-04-01" to "2026-04-30"
      And the matching transaction total should be 3

    @pri:2
    @id:2213-052
    Scenario: Clearing date filters preserves the active search
      Given I have the following transactions:
        | Payee        | Amount | Category | Memo | Date       |
        | Coffee Start | -5.00  | Drinks   |      | 2026-04-01 |
        | Coffee End   | -6.00  | Drinks   |      | 2026-04-30 |
        | Coffee Old   | -7.00  | Drinks   |      | 2026-03-31 |
        | Grocery      | -8.00  | Food     |      | 2026-04-15 |
      And my date range is from "2026-04-01" to "2026-04-30"
      And I have searched transactions for "coffee"
      When I click the existing Clear Filters button
      Then both date bounds should be empty
      And the search input should contain "coffee"
      And the matching transaction total should be 3

    @pri:1
    @id:2213-028
    Scenario: Search covers all pages and starts its results on page one
      Given my workspace has two full pages of matching "Coffee" transactions
      And those transactions are older than one full page of nonmatching transactions
      And I am viewing page 2 of my unfiltered transactions
      When I search transactions for "coffee"
      Then I should be on page 1 of the search results
      And the matching transaction total should equal two full pages
      And the results should be ordered by date descending then creation ID descending

    @pri:2
    @id:2213-053
    Scenario: Navigating search results retains the applied term
      Given my workspace has two full pages of matching "Coffee" transactions
      And those transactions are older than one full page of nonmatching transactions
      And I have searched transactions for "coffee"
      When I navigate to page 2 of the search results
      Then every displayed transaction should match "coffee"
      And the search input should contain "coffee"

    @pri:2
    @id:2213-054
    Scenario: Changing search resets the result page
      Given my workspace has two full pages of matching "Coffee" transactions
      And I have searched transactions for "coffee"
      And I am viewing page 2 of the search results
      When I replace the search with "missing"
      Then I should be on page 1 of the search results
      And the matching transaction total should be 0

    @pri:3
    @id:2213-029
    Scenario: Search does not expose matching transactions from another workspace
      Given I have the following transactions:
        | Payee     | Amount |
        | My Coffee | -5.00  |
      And "bob" owns a workspace called "Bob's Finances"
      And "Bob's Finances" contains the following transactions:
        | Payee        | Amount |
        | Bob's Coffee | -6.00  |
      When I search transactions for "coffee" in "My Finances"
      Then the search results should contain exactly these payees:
        | Payee     |
        | My Coffee |
      And the matching transaction total should be 1

    @pri:3
    @id:2213-030
    Scenario: A failed search shows an error rather than a successful empty result
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And the next search request will fail
      When I search transactions for "coffee"
      Then I should see an error explaining that transactions could not be loaded
      And I should not see "No transactions match your search."
      And the search input should remain editable with text "coffee"

    @pri:3
    @id:2213-055
    Scenario: Retrying a failed search replaces its error with matching results
      Given I have the following transactions:
        | Payee        | Amount |
        | Coffee House | -5.00  |
      And my search for "coffee" failed with a visible load error
      When I press Enter and the retry succeeds
      Then the search results should contain exactly these payees:
        | Payee        |
        | Coffee House |
      And the search error should no longer be visible

    @pri:4
    @id:2213-031
    Scenario: Switching workspace clears old results and retains search and dates
      Given I have the following transactions:
        | Payee        | Amount |
        | First Coffee | -5.00  |
      And I also have an accessible workspace "Other Finances"
      And "Other Finances" contains the following transactions:
        | Payee         | Amount |
        | Second Coffee | -6.00  |
      And my date range is from "2026-01-01" to "2026-12-31"
      And both transactions are dated "2026-04-01"
      And the search response for "My Finances" is held pending
      When I search transactions for "coffee"
      And I switch to workspace "Other Finances"
      Then the old workspace results should be cleared
      And the search input should contain "coffee"
      And my date range should remain from "2026-01-01" to "2026-12-31"
      And the new workspace search should start on page 1

    @pri:4
    @id:2213-056
    Scenario: A late old-workspace response cannot overwrite new-workspace search results
      Given I have the following transactions:
        | Payee        | Amount |
        | First Coffee | -5.00  |
      And I also have an accessible workspace "Other Finances"
      And "Other Finances" contains the following transactions:
        | Payee         | Amount |
        | Second Coffee | -6.00  |
      And both transactions are dated "2026-04-01"
      And my date range is from "2026-01-01" to "2026-12-31"
      And a search response for "coffee" in "My Finances" is still pending
      And I have switched to workspace "Other Finances" with search text "coffee"
      When the new workspace response succeeds
      And the old workspace response arrives afterward
      Then the search results should contain exactly these payees:
        | Payee         |
        | Second Coffee |
      And the matching transaction total should be 1

    @pri:4
    @id:2213-032
    Scenario: A viewer can search using the keyboard with accessible result feedback
      Given "charlie" can view data in "My Finances"
      And "My Finances" has two full pages of transactions matching "coffee"
      And I switched to user "charlie"
      When I view my transactions
      And I tab to the input labelled "Search transactions"
      And I search transactions for "coffee" using the keyboard
      Then the matching total should be announced in a polite status region
      And the announced total should include matches on every result page

    @pri:4
    @id:2213-057
    Scenario: A viewer can clear search using the keyboard and regain input focus
      Given "charlie" can view data in "My Finances"
      And "My Finances" has two full pages of transactions matching "coffee"
      And I switched to user "charlie"
      And I am viewing my transactions with search text "coffee"
      When I tab to the button named "Clear search"
      And I activate it using the keyboard
      Then the search input should be empty and focused
      And I should not have needed permission to edit transactions
