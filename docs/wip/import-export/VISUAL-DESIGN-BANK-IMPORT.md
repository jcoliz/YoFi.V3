---
references:
- PRD-BANK-IMPORT.md
- MOCKUP-BANK-IMPORT.md
status: Approved
---

# Visual Design for Transaction Bank Import

> **See [`MOCKUP-BANK-IMPORT.md`](MOCKUP-BANK-IMPORT.md) for visual mockups of all page states.**

## Page Structure

**Navigation**: Primary navigation item "Import"

**Layout Sections**:
1. File picker (top)
2. Upload status pane (below file picker, closable)
3. Transaction review table with action buttons
4. Pagination control (below table)

## Navigation Integration

- **Location**: Primary nav item "Import"
- **Badge indicator**: Not implemented (too complex for initial release)
- **Post-import navigation**: After successful import, automatically navigate to transactions page

## File Picker

**Pattern**: Traditional file input with multi-select
- Text above: "Choose bank files to upload"
- Accepts `.ofx` and `.qfx` file extensions
- No drag-and-drop (deferred to future enhancement)
- No preview of queued files
- No pre-upload validation (validation happens during processing)

## Upload Status Pane

**Visibility**: Shown when files are being processed, closable by user

**Content**:
- During processing: "Importing..." with spinner
- After completion: "File X imported: 195 transactions added"
- Multiple files show multiple status lines
- Can be dismissed by user

**Upload behavior**:
- Sequential or parallel processing (whichever is easier to implement)
- Users can interact with review table during upload if implementation allows

## Transaction Review Table

**Columns**:
1. Selection checkbox (changeable by user)
2. Date
3. Payee
4. Matched Category (placeholder for future Payee Matching rules feature)
5. Amount

**Duplicate Status Display**:
- **New transactions**: Checkbox selected by default, normal row styling
- **Exact duplicates**: Checkbox deselected by default, normal row styling
- **Potential duplicates**: Checkbox deselected by default, row highlighted (yellow/warning background)

**Potential Duplicate Alert**:
When any potential duplicates exist, show alert box above table:
> "Note: Potential duplicates detected and highlighted. Transactions have the same identifier as another transaction, but differ in payee or amount."

**Display strategy**:
- Single unified table (no collapsible sections or grouping)
- All transaction types shown together
- Visual distinction via row highlighting only

**Comparison view**: Not implemented (deferred to future enhancement)

## Action Buttons

**Placement**: Above table, right side

**Import Button**:
- Text: "Import" (no transaction count)
- Behavior: Imports all selected transactions, removes them from review table, navigates to transactions page

**Delete Button**:
- Text: "Delete All"
- Behavior: Shows confirmation modal, then deletes all transactions from review table (selected and unselected)
- Confirmation message: "Are you sure you want to delete all pending import transactions? This cannot be undone."

**Selection controls**: No "Select All" or "Deselect All" buttons (new transactions are pre-selected by default)

## Pagination

**Standard pagination control below table**
- Hidden when table is empty
- Standard page size (matches transactions page pattern)

## Empty States

**No files uploaded yet**:
- Show file picker with "Choose bank files to upload" text
- No table, no pagination, no action buttons

**All transactions accepted/deleted**:
- Table removed, returns to empty state
- File picker remains visible for next import

## Future Enhancements (Deferred)

- Drag-and-drop file upload
- Side-by-side comparison view for potential duplicates
- Badge indicator in navigation showing pending import count
- Help text/tooltips explaining workflow
- Responsive design for mobile/tablet (will be addressed after desktop version complete)
