---
status: Approved (Stories 1-5) Additional stories still in draft
owner: James Coliz
target_release: Beta 3
ado: "[Link to ADO Item]"
references:
   - docs\wip\payee-rules\PRD-PAYEE-RULES-REVIEW.md
---

# Product Requirements Document: Payee Matching Rules

## Problem Statement

Manually adding categories to transactions is tedious. At high volumes,
it's so much work that users are not even likely to do it, defeating the
purpose of a financial tracking app. It would be much better if the app
could have some degree of intelligence to know what categories to assign
to what transactions.

---

## Goals & Non-Goals

### Goals
- [ ] Automatically categorize transactions based on their payee
- [ ] User controls the payee matching rules
- [ ] It should be easy to add new rules
- [ ] Ideally a very high percentage of transactions should match immediately on import

### Non-Goals
- Initial implementation does not affect splits. Payee matching sets the whole transaction category. Future stories (6,7) expand beyond this
- Tracking of payees is out of scope. Actual used payees are not collected, stored, or analyzed. "Payees" in our app are purely used as part of matching rules to assign categories.
- Any modifications to transactions outside of category, e.g. memo, date
- Automatic creation of rules, or suggestions of rules to create. Interesting as a future feature enhancement.
- AL/ML-driven category matching (also interesting for future!)

---

## User Stories

### Story 1: User - Establish payee matching rules
**As a** user
**I want** to set up payee matching rules
**So that** my transactions are automatically categorized when I import them

**Acceptance Criteria**:
- [ ] Payee matching rule includes a category, which matching transactions will be assigned when matched
- [ ] Rule includes a free-form payee name snippet (required)
- [ ] User can alternately describe the rule with a regular expression
- [ ] User can fully manage payee matching rules (CRUD)
- [ ] User can sort listing of rules by: Payee name (default), category, or LastUsedAt
- [ ] User can quickly edit most important fields (payee, regex flag, and category) while viewing the list of payee rules.
- [ ] From Transactions page, user can create a new rule based upon a chosen transaction. The new rule will take its payee name from the transaction, and if the transaction already has a category, it will use that. User can review the new rule before it's saved, and will typically trim down the payee to a smaller substring
- [ ] Rules are scoped to the tenant of which they are a member
- [ ] Category terms will be automatically normalized using established transaction category normalization practices
- [ ] Empty category is not allowed (validation error)
- [ ] Regex patterns are validated on save with user-friendly error messages
- [ ] Invalid regex displays the .NET Regex error message to help users fix it
- [ ] Regex patterns are tested for ReDoS vulnerabilities (timeout after 100ms on test string)

### Story 2: User - Sees transactions automatically categorized on bank import
**As a** User
**I want** my transactions to be automatically categorized when I import a downloaded bank file
**So that** I don't have to categorize them myself

**Acceptance Criteria**:
- [ ] Payee matching rules are applied when user imports a bank file
- [ ] Matched category is shown to user on the import review page
- [ ] User cannot change the category in import review screen. It is for information only.
- [ ] User cannot interact with matching rules on the import review page

**Matching Rules**:
- [ ] If a transaction contains the payee name snippet from the rule within its payee field, that's a match
- [ ] In case of conflict between two substring-only rules, the rule with the longer payee name snippet wins. In case where the rules have equal length, the most recently added or edited rule takes priority.
- [ ] If a transaction exactly matches the regular expression, in case of a rule with regular expression, then it's a match
- [ ] In case of conflict, a matching regular expression rule takes precedence over a substring match, regardless of length

### Story 3: User - Manually triggers rule matching
**As a** User
**I want** to trigger automatic categorization on a specific transaction
**So that** I can apply recent rule changes to that transaction

**Acceptance Criteria**:
- [ ] On transactions display page, user can identify a specific transaction and apply rules to it
- [ ] The updated category is immediately shown on transactions display page
- [ ] If transaction already has a category, it is overwritten and a message shown to user
- [ ] If transaction already has splits, no change is made to the transaction. Friendly error message is shown to the user explaining why we didn't do what they asked.

**Limitations**:
- [ ] Manual matching is for a specific transaction only. In future we can consider a bulk re-match, but I haven't found that super useful.

Regarding splits, if user wants to remove splits and apply rule matching, this is something of a corner case. They can use existing tools
for this. They can edit the transaction, remove the splits, and then trigger manual matching.

### Story 4: User - Advanced matching rules beyond payees
**As a** user
**I want** to describe matching rules with more dimensions than only payee
**So that** I have more fine-grained control over which transactions are matched

**Acceptance Criteria**:
- [ ] User can modify more advanced matching rules in a context dedicated to editing a single payee rules
- [ ] User can set a source rule, again as a substring or a regular expression, eg. "MegaBankCorp" matches all sources with "MegaBankCorp"
- [ ] User can set an fixed amount value to match, in which case must match the amount directly.
- [ ] Amounts are expressed in absolute value, e.g. "200+" matches greater than or equal to 200 *and* less than or equal to -200.
- [ ] User can set an amount range, which can be unbounded on either side. Range is inclusive of the specified bound.
- [ ] User cannot distinguish between expenses or income in a matching rule. (I have not seen a need for this. If it comes up, we can consider it as a future enhancement.)
- [ ] All aspects of a rule must be met for a match to be made
- [ ] In case where multiple aspects of rule are set (e.g. payee name and amount), and there is a conflict where multiple rules match, the rule which has more aspects is considered the better match.
- [ ] Help text explains sign treatment. "Absolute Value" or "Amount (regardless of direction)", "100 matches both +100 (income) and -100 (expense)"

**Limitations**:
- YoFi is single-currency, so currency is not considered.
- Whether transaction is negative or positive is generally a technical concern not a user concern. In my experience, users think in absolute value. Sign of amount is mentally translated as "I got this money" or "I spent this money"

### Story 5: User - Rule cleanup
**As a** user
**I want** to remove unused rules from my rule set
**So that** matching happens more quickly, and it's easier for me to manage the rules I do care about

**Acceptance Criteria**:
- [ ] User can view when rule was last used and how frequently its been used
- [ ] User can filter and sort by these fields
- [ ] User can bulk delete old rules. User can specify how old is "old", and 12 months will be the intelligent default.

### Story 6: User - Split based on amortization [NEW] [FUTURE]
**As an** advanced user with loan payments
**I want** to split my loan payments transactions into principal and interest according to their amortization rules
**So that** I can precisely track how much of my money goes to principal or interest

**Acceptance Criteria**:
- [ ] User can assign loan details to a payee matching rule
- [ ] When a transaction matches based on matching criteria, the resulting transaction is split based on loan details

**Example**

Rule
- PayeePattern: "Mortgage"
- Category: Mortgage Principal
- Loan Rule: Category: Mortgage Interest, ...

Imported Transaction
- Payee: "Mega Bortgage Bank Inc"
- Amount: $1500
- Date: 1/1/2026

Resulting Splits:
- Mortgage Principal: xxx
- Mortgage Interest: xxx

### Story 7: User - Split based on amounts [NEW] [FUTURE]
**As an** advanced user with complex recurring transactions
**I want** to split matched transactions into different categories by amount
**So that** I can precisely track my money flows

**Acceptance Criteria**:
- [ ] User can assign multiple category splits to payee matching rules, containing category and amount
- [ ] When a transaction matches based on matching criteria, the resulting transaction is split into those categories and amounts
- [ ] The primary category on the matching rule recieves remaining amount, so it is entered as a balanced transaction
- [ ] User cannot have *both* a loan-based split and an amount-based split

**Example**

Rule
- PayeePattern: "Landlord"
- Category: Utilities
- Split Rules: (Category: Rent, Amount $1000), (Category: Parking, Amount $100)

Imported Transaction
- Payee: "Mega Landlord LLC"
- Amount: $1475

Resulting splits:
- Rent: $1000
- Utilities: $375
- Parking: $100

---

## Technical Approach

Payee matching rules will be implemented with a new `PayeeMatchingRule` entity that applies pattern matching to categorize transactions automatically.

### Layers Affected

- [x] Frontend (Vue/Nuxt) - Payee rule CRUD page, transaction page actions, import review display
- [x] Controllers (API endpoints) - Full CRUD controller for payee rules, trigger matching endpoint
- [x] Application (Features/Business logic) - Rule CRUD capabilities, matching logic
- [x] Entities (Domain models) - PayeeMatchingRule entity
- [x] Database (Schema changes) - Store and index rules

### High-Level Entity Concepts

**PayeeMatchingRule Entity** (new):
- PayeePattern (required - substring or regex pattern to match transaction payee)
- PayeeIsRegex (flag indicating pattern type)
- SourcePattern (optional - match transaction source/bank)
- SourceIsRegex (flag for source pattern type)
- AmountExact (optional - match specific amount)
- AmountMin / AmountMax (optional - match amount range)
- Category (required - category to assign when matched)
- CreatedAt / ModifiedAt (audit timestamps)
- LastUsedAt / MatchCount (usage statistics for cleanup)

**Q** How to handle split and loan rules? **A** Left open for now, still considering best approach

### Key Business Rules

1. **Conflict Resolution** - When multiple rules match, apply precedence:
   - More matching aspects wins (e.g., payee + amount beats payee only)
   - Regex pattern beats substring pattern
   - Longer pattern beats shorter pattern
   - Most recently modified wins (tie-breaker)

2. **Pattern Matching**:
   - Case-insensitive matching (both substring and regex)
   - Substring uses `StringComparison.OrdinalIgnoreCase`
   - Regex uses `RegexOptions.IgnoreCase`
   - ReDoS protection: 100ms timeout on regex evaluation

3. **Category Normalization**:
   - Standard practices as implemented by Transactions

4. **Amount Matching**:
   - Amounts are absolute value (matches both positive and negative)
   - Ranges are inclusive of specified bounds
   - Cannot distinguish income vs expense (by design)

5. **Rules Scoped to Tenant** - Each rule belongs to a tenant via `BaseTenantModel`

6. **No Active/Inactive Flag** - Rules are either present or deleted (simplicity)

7. **Amortization Rules** - Information that is attached to a payee for amortization rules
   - Loan origination date
   - Number of periods
   - Interest rate
   - Category for interest (primary rule category would be for principal)

### UX Considerations

1. Main payee rule management screen will allow editing of payee, regex flag, and category. ("Quick edit", like transactions main screen)
2. More detailed editing available on a details screen
3. Main screen will show indicators if advanced matching aspects are also set (stories 4,5,6)

### Code Patterns to Follow

- Entity pattern: [`BaseTenantModel`](../../src/Entities/Models/BaseTenantModel.cs)
- CRUD operations: [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing transaction endpoints
- Tenant data administration: Payee Matching rules are included in all tenant data administration operations.
- Testing: Standard unit/integration/functional tests with Gherkin comments or steps (Given/When/Then)

### Performance Considerations

- In-memory rule caching recommended (typical rule sets < 200KB)
- Standard indexes don't help substring matching (`LIKE '%pattern%'`)
- If rule sets exceed 1000+ rules, evaluate PostgreSQL trigram indexes or full-text search
- Target performance: < 100ms for typical rule set evaluation

---

## Success Metrics

- **Primary**: % of imported transactions automatically categorized (Target: >80%)
- **User Efficiency**: Average time to categorize 100 transactions (Target: <20 seconds)
- **Rule Coverage**: Average rules per active user (Target: >50 rules)
- **Accuracy**: % of auto-categorized transactions that users don't change (Target: >90%)
- **Adoption**: % of active users who create at least one rule within first month (Target: >90%)

---

## Dependencies & Constraints

**Dependencies**:
- Bank Import feature (PRD-BANK-IMPORT.md) - Rules apply during import
- Transaction Splits (PRD-TRANSACTION-SPLITS.md) - Rules should not override splits
- NOT Category management - Note that YoFi does not requires established category taxonomy. **all categories are ad-hoc** in YoFi

**Constraints**:
- Rules must execute quickly (<100ms for typical rule set) to avoid import delays
- Regex patterns must be validated when created to prevent regex denial-of-service (ReDoS)
- Rule count per tenant may need practical limits subject to real-world performace testing. That said, hundreds of rules in YoFi is not uncommon.

---

## Notes & Context

**Historical Context**:
This feature existed in YoFi v1 and was heavily used. User feedback indicated it was one of the most valuable time-saving features.

**Related Documents**:
- [`PRD-BANK-IMPORT.md`](../import-export/PRD-BANK-IMPORT.md) - Import flow integration
- [`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md) - Interaction with splits
- [`TRANSACTION-RECORD-DESIGN.md`](../transactions/TRANSACTION-RECORD-DESIGN.md) - Transaction data model

---

## Handoff Checklist (for AI implementation)

The [`PRD-PAYEE-RULES.md`](PRD-PAYEE-RULES.md) is **ready for AI implementation** with excellent requirements coverage and appropriate scope.

**All four handoff checklist items are satisfied**:
- ✅ User stories have clear acceptance criteria (5 stories, 50+ criteria)
- ✅ Open questions are resolved (8 questions fully answered)
- ✅ Technical approach indicates affected layers (all 5 layers identified with appropriate entity schema)
- ✅ Code patterns are referenced (related PRDs, entity base class)

**The document correctly focuses on requirements (WHAT/WHY) and appropriately leaves implementation details (HOW) for the implementation phase.** This is the proper scope for a PRD.

**Recommendation**: Proceed to implementation with current PRD. Optionally add explicit code pattern references (5-minute enhancement). A companion Design Document is **optional** for this feature - the codebase patterns provide sufficient guidance.

**Overall Grade**: A (95/100)
- Deduction for minor opportunity to strengthen code pattern references
- Exemplary requirements definition and scope discipline
- Better scoped than the Transaction Splits PRD comparison
