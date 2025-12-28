---
references:
- PRD-BANK-IMPORT.md
---

# Visual Design for Transaction Bank Import

From the navigation bar, user picks "Import". This navigates to the "Import" page.

This page has two sections: A file picker, and a table of import review transactions, with action buttons.

From file importer, user can multi-select files to import. These are immediately added to the review transactions list.

If there are already transactions there, the new ones are added.

Actions: Import or Delete

Import action imports all selected transactions in, and then removes all transactions from this list.

Delete removes all (with a confirmation)

Transaction review table columns:

1. Selection status (checkbox that is changeable)
1. Date
2. Payee
4. Matched Category (when Payee Matching rules is working)
5. Amount

Below transaction review table is a pagination control (will use a standard pagination control).

## Duplicate Status Display Strategy

The PRD defines three categories (New, ExactDuplicate, PotentialDuplicate), but the visual design doesn't specify:

- How to visually distinguish these three types (colors, icons, badges)?
- Should they be grouped into collapsible sections as described in Story 2?
- Or should they be displayed in a single unified table with visual indicators?
- What's the selection default for each type (New=selected, Exact/Potential=deselected)?

**A**

New: Selected
ExactDuplicate: Deselected
PotentialDuplicate: Deselected, row is highlighted. Alert box above table if there are any of these: Note: potential duplicates detected and highlihted. Transaction have the same identifier as another transaction, but differ in payee or amount.

Don't put them in collapsable sections

## Multi-File Upload Behavior

The design says "multi-select files to import" and "new ones are added," but needs clarification:

- Should files be uploaded sequentially or in parallel? **A** Whichever is easier
- Should there be a progress indicator during file processing? **A** Sure. Add a closable status pane below import. Shows that we're importing (don't need % complete progress), and shows results. "File X imported: 195 transactions added"
Should there be a summary after each file upload (X transactions added, Y duplicates detected)? **A** Add to the status pane
Can users continue interacting with the review table while files are uploading? **A** Ideally, but OK if they can't because of implementation complexity.

## Action Button Placement and Behavior

- Where exactly are Import/Delete buttons placed (above table, below table, both)? **A** Let's go with above table to the right side.
- Should "Import" button show count of selected transactions ("Import 12 transactions")? **A** No
- Should there be "Select All" / "Deselect All" actions per category or globally? **A** NO
- Should there be a "Select All New" shortcut? **A** No. New are selected by default

##  Navigation Integration
- Where does "Import" appear in the navigation bar? (Primary nav item or sub-menu under Transactions?) **A** Primary
- Should there be a badge/indicator showing pending import count in the nav? **A** Too complex
- How do users navigate back to regular transactions after import? **A** After import, navigate to transactions page

## File Picker UI Pattern

- Which component/pattern to use: drag-and-drop zone, traditional file input, or both? **A** Traditional file input is fine for now. Can consider drag/drop in future.
- Should there be file format/size validation feedback before upload? **A** No, let importing errors handle that
- Should there be a preview of files queued for upload? **A** No

## Empty States

- What does the page look like when no files have been uploaded yet? **A** Table is blank, no pagination control, no buttons. Just shows the file picker and text "Choose bank files to upload"
- Should there be help text explaining the import workflow? **A** Not now, should be self-explanatory
- What does the table look like when all pending transactions are accepted/deleted? **A** Removed and empty.

## Comparison View for Potential Duplicates
Story 2 mentions "Potential duplicates show comparison view (imported vs. existing data)," but this isn't detailed:

- Inline expandable row showing side-by-side comparison?
- Modal dialog with comparison when clicking on potential duplicate?
- What fields are compared (Date, Amount, Payee, Memo)?

**A** Ignore for now, consider for future improvement
