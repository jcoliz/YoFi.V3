# YoFi.V3 Demo Guide

## The story

**From a new account to useful financial insights, without categorizing every transaction by hand.**

This walkthrough follows one person's finances in a single workspace. First, load some sample history and see what it tells us. Then bring in a simulated bank download, discover how payee rules categorize it automatically, and see the reports catch up.

Allow about **15-20 minutes**, with the containers already downloaded. Each beat includes actions and suggested narration; use the narration as a conversation starter, not a script to read verbatim.

> **Evaluation only:** Alpha builds are not supported with real financial data. Use a disposable account, a password you do not use elsewhere, and the generated sample bank file rather than an actual bank statement. The evaluation deployment is not a production configuration.

## Before presenting

- Have Docker running, with a Compose version that supports published OCI Compose projects. No source checkout or local build should be needed for the published-project path.
- Check that localhost ports **5000** and **5001** are available.
- Rehearse against the published image version you intend to demonstrate. The roadmap and scenarios describe repository functionality; an older published image may differ.
- Use a fresh account and its personal workspace so the starting point is predictable. Load the sample data only once.
- Keep the same workspace selected throughout. In the UI, this is a **workspace**; some technical documentation calls it a **tenant**.
- During the first reports visit, note the report name, year, and a category total. Return to the same report after importing so the comparison is meaningful.

The published project uses the [CI Compose definition](../docker/docker-compose-ci.yml), which runs the frontend, backend, PostgreSQL, and an observability dashboard. PostgreSQL uses temporary storage in this configuration: stopping the database container loses its data. Plan to complete the walkthrough in one session.

## 1. Start YoFi from Docker Hub

**Steps**

1. In a terminal, start the published Compose project:

   ```powershell
   docker compose -f oci://docker.io/jcoliz/yofi-v3:latest up -d --wait
   ```

2. Wait for startup to finish, then open <http://localhost:5000>.
3. Confirm that the home page loads. Leave the containers running for the rest of the demo.

**Narrative**

> "We're running YoFi locally using published containers. We don't need to build the application to try it, and we can explore with synthetic financial data."

**Checkpoint:** The home page is available, not just a successful image download. If startup fails, resolve that before continuing; see the deployment caveat above.

## 2. Request an account

**Steps**

1. Open **Login**, then **Request one here**. Alternatively, visit <http://localhost:5000/register>.
2. Enter an email address, a unique username, a disposable password that meets the displayed requirements, and the password confirmation.
3. Select **Request Account** and wait for the acknowledgment.

**Narrative**

> "The account flow is phrased as a request. In this evaluation version, requests are automatically approved, so we can use the account immediately. The acknowledgment still mentions a team review; we don't need to wait for that today."

## 3. Sign in

**Steps**

1. Select **Continue** on the acknowledgment to reach the login page.
2. Enter the **username** and password you just registered, then select **Sign In**.
3. Confirm that you are signed in and back on the home page.

**Narrative**

> "This is now our own account. Next, we'll look at where its financial data lives."

## 4. Discover your personal workspace

**Steps**

1. Open **Workspaces**.
2. Find the personal workspace created for your new account. Its name should be personalized with your username.
3. Point out your **Owner** role and the **Open** and **Manage** actions. Keep this workspace for the demo rather than creating another.

**Narrative**

> "There's already a workspace ready for us; we don't have to configure one before getting started. Workspaces keep different sets of finances separate. We could use additional workspaces for other purposes, but today we'll stay in this personal one."

## 5. Tour workspace management

**Steps**

1. Select **Manage** on the personal workspace.
2. Look at **Workspace Settings**, including the name, description, key, and **Edit Workspace** action.
3. Find **Data Administration** and the **Sample Data** section.
4. Point out, but do not activate, **Delete All Workspace Data** and **Delete Workspace**.

**Narrative**

> "As the owner, we can change the workspace's name and description and manage its data. Clearing the data keeps the workspace and its members; deleting the workspace removes the workspace itself. Both are destructive, so we'll leave them alone and use the sample-data tools instead."

## 6. Give the workspace some history

**Steps**

1. In **Sample Data**, keep the **Personal Finance** scenario and the default dates under **Load sample data**.
2. Read the preview showing how many transactions and payee matching rules will be added.
3. Select **Load Sample Data**, then confirm with **Load Sample Data** in the dialog.
4. Wait for the confirmation showing the transactions and rules added.

**Narrative**

> "Instead of starting with an empty ledger, we'll explore realistic sample history. This adds both transactions and the rules that will categorize our next bank download. It adds to the workspace rather than replacing existing data."

**Checkpoint:** Both transaction and rule counts are reported. Do not load the sample data again to continue the walkthrough.

## 7. Explore transactions

**Steps**

1. Follow **View Transactions** from the sample-data confirmation, or open **Transactions**.
2. Check that the selected workspace is your personal workspace.
3. Browse the dates, payees, amounts, categories, and memos. Move to another page to show that there is more history.
4. Open a transaction row to see its details, then use **Back to Transactions**.

**Narrative**

> "This is the working ledger: individual transactions with enough context to understand what happened. Categories give that activity structure, while transaction details let us inspect an individual record."

## 8. Find something without scanning every page

**Steps**

1. Pick a recognizable word from a visible payee or category and enter it in the transaction search box. Submit the search.
2. Point out the matching results. Search covers payee, memo, category, and amount; it is not limited to the currently visible page.
3. Open a matching transaction, then select **Back to Transactions** to show that the search is retained.
4. Clear the search before continuing.

**Narrative**

> "We don't need to remember which page a purchase was on. One search can find a merchant, a note, or a category. We can inspect a result and come back without losing our place."

## 9. Establish the reporting baseline

**Steps**

1. Open **Reports**. On a first visit, **Expenses** is selected by default.
2. Look at the report year, category rows, and totals.
3. Switch to **Income**, then **Expenses Detail** to compare the available views.
4. In **Expenses Detail**, note a category total and the monthly columns, especially the most recent months. Take a screenshot or jot down one total for the final comparison.

**Narrative**

> "The ledger is useful for individual questions; reports make the overall picture easier to see. These built-in views summarize the same categorized transactions. The detail report shows the category hierarchy and monthly amounts. Right now, we're looking only at the history we loaded."

**Checkpoint:** You have a specific before-import report to compare against. Reports currently use the current year, and the UI has no year selector. Around the beginning of a year, the default sample history may fall entirely in the previous year, so an empty baseline can be expected; explain the dates rather than implying the data is missing.

## 10. Simulate downloading a bank statement

**Steps**

1. Return to **Workspaces** and select **Manage** on the same personal workspace.
2. Under **Sample Data**, find **Download sample bank data**.
3. Keep **Personal Finance** and the default download dates.
4. Select **Download Sample OFX** and note where your browser saves the file.

**Narrative**

> "In everyday use, we'd download an OFX or QFX file from our bank. For this demo, YoFi generates a synthetic OFX file. The default download covers a more recent period than the sample history, giving us new activity to bring in."

**Checkpoint:** The file is downloaded, but nothing has been imported yet. Downloading it does not change the workspace.

## 11. Upload the file for review

**Steps**

1. Open **Import** and verify the selected workspace.
2. Choose the OFX file you just downloaded, then select **Upload**.
3. Wait for **Review Transactions** to appear and browse the review pages.
4. Notice that new transactions are selected by default. Leave the selections unchanged for this first import.

**Narrative**

> "Uploading doesn't put these transactions straight into our ledger. It stages them for review first. We can choose what to accept, and duplicate detection helps avoid importing the same bank activity twice."

## 12. Notice the categories before accepting

**Steps**

1. Stay on the import review page and point out the populated **Category** column.
2. Pick a categorized row and remember its payee and category for the next beat.
3. Do not select **Import** yet.

**Narrative**

> "We haven't categorized this file by hand, but the sample bank transactions already have categories. That happened automatically during upload. Categories are read-only here; if a transaction needs a correction, we can edit it in Transactions after accepting it."

**Checkpoint:** The generated sample file is designed to match the sample rules. This does not mean every transaction from an arbitrary bank file will match; unmatched payees can remain uncategorized.

## 13. Reveal how payee rules work

**Steps**

1. Open **Rules** in the navigation, or visit <http://localhost:5000/payee-rules>.
2. Search for a recognizable part of the payee from the import review.
3. Find the rule's payee pattern and assigned category. Compare it with the category you saw in review.
4. Look at the available create, edit, and delete controls without changing the sample rules.

**Narrative**

> "This is the explanation for the automation: explicit payee matching rules, not a category we entered during review. A rule associates a payee pattern with a category. Rules can use substring matching or regular expressions, so a recognizable merchant name can match different bank descriptions. The sample load gave us a starting set; we can manage our own rules as our finances change."

**Checkpoint:** Leaving Import to inspect rules does not accept or discard the pending review. Editing a rule here is not a demonstration of re-categorizing an already-uploaded queue; manual re-matching is outside this walkthrough.

## 14. Accept the import

**Steps**

1. Return to **Import**. Confirm that the pending review is still there.
2. Select **Import**.
3. In **Confirm Import**, check the workspace name, the selected transaction count, and any discarded count.
4. Select **Import** in the dialog to accept the selected transactions.

**Narrative**

> "We've reviewed the activity and understand where the categories came from. Now we commit the selected transactions to the ledger. Completing review clears the queue, including any unselected rows, so this is the point to check the selection summary."

**Checkpoint:** The app confirms the import and returns to Transactions. Accepting applies to the selected transactions across the review queue, not just the visible page.

## 15. See the new activity in the ledger

**Steps**

1. On **Transactions**, clear any leftover search or date filters.
2. Find one of the payees from the import review; use search if helpful.
3. Open an imported transaction and check its date, amount, and category.
4. Return to the list and clear the search.

**Narrative**

> "The new bank activity is now part of our normal transaction history, and its automatically assigned categories came with it. If something needs adjusting, this is where we review and edit the accepted records."

## 16. Close the loop with reports

**Steps**

1. Return to **Reports** and select **Expenses Detail**, matching the baseline from beat 9.
2. Compare the same report year and category total. Point out the recent months that now include the imported expenses.
3. Optionally select **All Detail** to see income, expenses, and savings categories together.

**Narrative**

> "The reports now include the bank activity we just accepted. We went from an account with an empty workspace to history, a new bank download, automatic categorization, a deliberate review, and updated insights. That's the repeatable workflow: import, review, understand."

**Checkpoint:** Compare amounts and populated months, not the number of category rows. Importing more transactions into existing categories need not create new report rows.

## Optional detours

Keep these out of the main path unless the audience wants more depth:

- **Correct one transaction:** After accepting the import, use a transaction's **Edit** action to show quick editing of payee, category, and memo. Save a deliberate correction or cancel to keep the report comparison unchanged.
- **Demonstrate duplicate protection:** Upload the same OFX file again after accepting it. Exact duplicates should be deselected by default. Explain why this matters, but do not select them all and re-import them.
- **Separate another set of finances:** Create another workspace and show that its transactions are separate. Return to the original workspace before resuming the main story.

Avoid presenting budgets, charts, report drill-through, configurable reporting periods, Excel export, or manual re-matching as available features. They are not part of the implemented primary path documented by the roadmap.

## Finish or repeat

To stop the evaluation containers when finished:

```powershell
docker compose -f oci://docker.io/jcoliz/yofi-v3:latest down
```

**This loses the demo database.** The CI Compose configuration uses temporary PostgreSQL storage, so accounts and workspace data do not survive stopping the database container.

For another demo, use a fresh account, or deliberately use **Delete All Workspace Data** in the demo workspace and load the sample data once again. That action permanently removes transactions, payee matching rules, and pending imports while keeping the workspace and members. Never use it on a workspace whose data you want to keep.

## Scope and references

This guide covers the implemented primary path, not every planned capability:

- [Product roadmap](PRODUCT-ROADMAP.md) - Implemented stories versus future work.
- [Authentication](../tests/Functional/Features/Authentication.feature) and [workspaces](../tests/Functional/Features/Tenancy.feature) - Account and workspace journeys.
- [Workspace data](../tests/Functional/Features/WorkspaceData.feature) - Sample data and the matching OFX workflow.
- [Transaction search](../tests/Functional/Features/TransactionFiltering.feature) and [transaction records](../tests/Functional/Features/TransactionRecord.feature) - Finding and inspecting activity.
- [Bank import](../tests/Functional/Features/BankImport.feature) and [payee rules](../tests/Functional/Features/PayeeRules.feature) - Review, duplicate handling, and automatic categorization.
- [Reports](../tests/Functional/Features/Reports.feature) - Built-in report scenarios. These scenarios are currently marked work-in-progress; the roadmap marks the built-in report story implemented.
