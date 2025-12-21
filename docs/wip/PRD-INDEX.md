# Product Requirements Index

This index provides a centralized view of all Product Requirements Documents (PRDs) across the project. PRDs are organized by feature area and co-located with their detailed technical design documents.

## Active Features (V3.0)

| Feature | Status | Priority | Owner | PRD | Technical Design |
|---------|--------|----------|-------|-----|------------------|
| Transaction Splits | Design Complete | High | James Coliz | [PRD](transactions/PRD-TRANSACTION-SPLITS.md) | [Design](transactions/TRANSACTION-SPLIT-DESIGN.md) |
| Transaction Filtering | Design Complete | High | James Coliz | [PRD](transactions/PRD-TRANSACTION-FILTERING.md) | [UI Recommendations](transactions/TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) |

## Planned Features (V3.1+)

| Feature | Status | Priority | Owner | PRD | Technical Design |
|---------|--------|----------|-------|-----|------------------|
| Transaction Bank Import | Draft | High | James Coliz | [PRD](import-export/PRD-BANK-IMPORT.md) | TBD |
| Tenant Data Administration | Draft | High | James Coliz | [PRD](import-export/PRD-TENANT-DATA-ADMIN.md) | TBD |

## Future Features

| Feature | Status | Priority | Owner | PRD | Notes |
|---------|--------|----------|-------|-----|-------|
| Category Autocomplete | Future | Medium | TBD | TBD | Listed in splits design as future enhancement |
| Split Templates | Future | Low | TBD | TBD | Save common split patterns |
| Bulk Categorization | Future | Medium | TBD | TBD | Apply category to multiple transactions |

## Status Definitions

- **Draft** - Initial requirements gathering, not yet reviewed
- **In Review** - Under stakeholder review, feedback pending
- **Approved** - Requirements approved, awaiting detailed design
- **Design Complete** - Detailed technical design finished, ready for implementation
- **In Progress** - Implementation underway
- **Implemented** - Code complete, in testing
- **Released** - Deployed to production

## Priority Levels

- **High** - Core functionality, blocking other features
- **Medium** - Important but not blocking
- **Low** - Nice to have, can be deferred

## Creating a New PRD

1. Copy the template: [`PRD-TEMPLATE.md`](PRD-TEMPLATE.md)
2. Create PRD in appropriate feature folder: `docs/wip/{feature-area}/PRD-{FEATURE-NAME}.md`
3. Fill out all required sections (Problem Statement, Goals, User Stories, Technical Approach)
4. Add entry to this index with "Draft" status
5. Request review and update status accordingly

## PRD Guidelines

- **Keep PRDs concise** - Under 200 lines preferred, link to separate technical design documents
- **Focus on what and why** - Save how (implementation details) for technical design docs
- **Include acceptance criteria** - Make user stories testable
- **Link to designs** - Reference detailed technical documents for implementation guidance
- **Update status** - Keep this index current as PRDs progress through lifecycle
