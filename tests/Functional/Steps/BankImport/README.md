# Bank Import Step Classes

This directory contains bank import-related step definition classes organized by domain responsibility.

## Architecture

All bank import step classes inherit from [`BankImportStepsBase`](BankImportStepsBase.cs), which provides:
- Common helper methods (object store access, workspace key resolution)
- Direct access to test context (`_context`)

## Step Classes

### BankImportNavigationSteps
**Purpose:** Navigation to import pages and workspace selection

**Responsibilities:**
- Navigating to the import review page
- Selecting workspaces in the import context
- Attempting navigation to import page (via nav bar)

**Example Steps:**
- `[Given("I am on the import review page")]`
- `[When("I am on the Import Review page")]`
- `[When("I navigate to the Import page")]`
- `[When("I attempt to navigate to the Import page")]`

**Current Implementation:** [`BankImportNavigationSteps.cs`](BankImportNavigationSteps.cs)

### BankImportUploadSteps
**Purpose:** OFX file generation, upload, and upload completion

**Responsibilities:**
- Generating OFX files with specified transaction counts
- Uploading OFX files through the UI
- Waiting for upload completion
- Storing uploaded transaction payees for verification

**Example Steps:**
- `[Given("I have uploaded an OFX file with {count} new transactions")]`
- `[Given("I have a valid OFX file with {count} transactions")]`
- `[When("I upload OFX file {filename}")]`
- `[When("I upload the OFX file")]`
- `[When("I upload the same OFX file again")]`

**Current Implementation:** [`BankImportUploadSteps.cs`](BankImportUploadSteps.cs)

### BankImportReviewSteps
**Purpose:** Import review operations (selection, import confirmation)

**Responsibilities:**
- Selecting/deselecting transactions for import
- Confirming import operations
- Tracking affected transactions

**Example Steps:**
- `[Given("I have deselected {count} transactions")]`
- `[Given("I have imported these transactions")]`
- `[When("I import the selected transactions")]`

**Current Implementation:** [`BankImportReviewSteps.cs`](BankImportReviewSteps.cs)

### BankImportDataSteps
**Purpose:** Test data seeding via Test Control API

**Responsibilities:**
- Seeding import review transactions for faster test setup
- Configuring selected/deselected transaction counts
- Bypassing UI for test data creation

**Example Steps:**
- `[Given("There are {count} transactions ready for import review, with {selectedCount} selected")]`

**Current Implementation:** [`BankImportDataSteps.cs`](BankImportDataSteps.cs)

**Note:** This approach is faster and more reliable than uploading OFX files through the UI for test setup.

### BankImportAssertionSteps
**Purpose:** Verification operations (Then steps)

**Responsibilities:**
- Verifying transaction counts in review list
- Checking selection/deselection state
- Verifying import queue state (empty/cleared)
- Checking for warnings (duplicates, permissions)
- Verifying upload permissions
- Transaction highlight verification

**Example Steps:**
- `[Then("page should display {count} transactions")]`
- `[Then("I should see {count} transactions in the review list")]`
- `[Then("{count} transactions should be selected by default")]`
- `[Then("{count} transactions should be deselected by default")]`
- `[Then("import review queue should be completely cleared")]`
- `[Then("the review list contains the transactions uploaded earlier")]`
- `[Then("no transactions should be highlighted for further review")]`
- `[Then("I should see a permission error message")]`
- `[Then("I should see a warning about potential duplicates")]`
- `[Then("I should be able to complete the import review")]`
- `[Then("the transactions deselected earlier should remain deselected")]`

**Current Implementation:** [`BankImportAssertionSteps.cs`](BankImportAssertionSteps.cs)

## Design Principles

1. **Single Responsibility** - Each class handles one aspect of bank import functionality
2. **Direct Context Access** - Step classes use `_context` directly without unnecessary wrapper methods
3. **DRY** - Common functionality lives in BankImportStepsBase
4. **Testability** - Clear separation makes it easy to test individual concerns
5. **Maintainability** - Smaller, focused classes are easier to understand and modify

## Object Store Keys

All bank import-related operations use the standard object store keys from [`ObjectStoreKeys`](../../Infrastructure/ObjectStoreKeys.cs):

- `ObjectStoreKeys.OfxFilePath` - Path to generated/uploaded OFX file
- `ObjectStoreKeys.UploadedTransactionPayees` - List of payee names from uploaded transactions
- `ObjectStoreKeys.AffectedTransactionKeys` - Keys of transactions affected by operations (e.g., deselected)
- `ObjectStoreKeys.CurrentWorkspace` - Currently selected workspace name
- `ObjectStoreKeys.LoggedInAs` - Logged in username
- `ObjectStoreKeys.ExistingTransactions` - Existing transactions for duplicate testing

## Usage Pattern

The bank import step classes are used directly by the source-generated test classes. The Gherkin step definitions map to these step methods via attributes.

**Example Test Flow:**
```gherkin
Feature: Bank Import

Scenario: User uploads OFX file and imports transactions
    Given I am logged in as "alice"
    And I have an active workspace "Personal Budget"
    And I am on the import review page
    When I upload an OFX file with 10 transactions
    And I import the selected transactions
    Then import review queue should be completely cleared
```

**Generated Test Code:**
The source generator creates test methods that call these step classes directly, matching the Gherkin steps to the appropriate step methods via attributes.

## File Upload Approaches

Bank import supports two approaches for test data:

1. **UI Upload** (BankImportUploadSteps) - Generates OFX files and uploads through the UI
   - More realistic user flow
   - Tests the complete upload pipeline
   - Slower execution time

2. **API Seeding** (BankImportDataSteps) - Seeds transactions via Test Control API
   - Faster test setup
   - Deterministic test data
   - Bypasses UI for speed

Choose the approach based on what you're testing:
- Use **UI upload** when testing the upload functionality itself
- Use **API seeding** when testing review/import operations (faster)

## Cross-Step Dependencies

Bank import steps often depend on:
- **Auth state** - User must be logged in (via AuthSteps)
- **Workspace context** - Import operations require an active workspace (via WorkspaceDataSteps)
- **Transaction verification** - Import results may be verified in TransactionsPage (via TransactionListSteps)

## Migration Notes

This refactored structure replaces the monolithic [`BankImportSteps.cs`](../BankImportSteps.cs) (552 lines) with focused, single-responsibility classes:

- **BankImportNavigationSteps** - 2 step methods
- **BankImportUploadSteps** - 5 step methods
- **BankImportReviewSteps** - 2 step methods
- **BankImportDataSteps** - 1 step method
- **BankImportAssertionSteps** - 14 step methods

All existing tests continue to work without modification, as the step attributes remain unchanged.
