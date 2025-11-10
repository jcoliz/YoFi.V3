# Page Object Models (POMs)

Here we keep one class for each of the major pages on the client.

## Exclusive Use of Locators

The page objects are the only places where locators can be generated.
This allows us to have a single place where changes have to be kept up
when the page changes.

## Single Locator

Within the page view, there should only be a single place where a locator
definition needs to change, e.g. if a data-test-id changes on the client,
it only has to be changed on **one line** in the POM.

## All waiting

Because waiting on load conditions is often quite page-specific, and changes
based on design, all waiting is done in POMs.