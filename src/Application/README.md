# Application Logic

The goal is that all code implementing actual application behaviour happens here.
Ideally, the entities should be code-free, and the controllers just pass through
immediately to the Application Features.

This layer is tested by the [Unit Tests](../../tests/Unit/).

## Features

### TransactionsFeature

Provides transaction management functionality including create, read, update, delete operations for transactions within the current tenant context.

**Transaction Splits Alpha-1 Support:**
- Every transaction has a single split at Order=0 containing the category
- Split amount automatically matches transaction amount
- Category is sanitized before saving using [`CategoryHelper.SanitizeCategory()`](Helpers/CategoryHelper.cs)
- See [`docs/wip/transactions/PRD-TRANSACTION-SPLITS.md`](../../docs/wip/transactions/PRD-TRANSACTION-SPLITS.md) for Alpha-1 design details

## Helpers

### CategoryHelper

Static helper class for category processing and sanitization.

**Sanitization Rules:**
- Trims leading/trailing whitespace
- Consolidates multiple spaces to single space
- Capitalizes all words (first letter of each word)
- Removes whitespace around ':' separator (for hierarchical categories)
- Removes empty terms after splitting by ':'
- Returns empty string for null/whitespace input

**Examples:**
- `"homeAndGarden"` → `"HomeAndGarden"`
- `"Home    and Garden"` → `"Home And Garden"`
- `"Home :Garden"` → `"Home:Garden"`
- `"Home: "` → `"Home"`
- `"  "` → `""`

Categories are free-text strings that can optionally use ':' separators to define hierarchies. All categories are sanitized before saving to ensure consistent formatting.

See [`docs/wip/transactions/PRD-TRANSACTION-SPLITS.md`](../../docs/wip/transactions/PRD-TRANSACTION-SPLITS.md) (lines 155-200) for complete sanitization specification.
