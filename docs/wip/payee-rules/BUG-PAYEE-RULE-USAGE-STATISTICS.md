---
status: Open
issue_type: Bug
severity: Medium
related_docs:
  - PRD-PAYEE-RULES.md
  - DESIGN-PAYEE-RULES-STORIES-1-2.md
---

# Bug Report: Payee Rule Usage Statistics Can Be Attributed to the Wrong Rule

## Summary

When matching a payee, the application records usage statistics by looking up the first rule whose category equals the matched category. It does not retain the identity of the rule that actually matched. If multiple rules assign the same category, the wrong rule can have its `MatchCount` and `LastUsedAt` updated.

## Affected Behavior

Story 1 includes sorting the rule list by `LastUsedAt`. The rules page also displays usage information. Incorrect attribution can therefore make the displayed statistics and last-used sort inaccurate.

## Reproduction

1. Create two rules in the same tenant:
   - Pattern `Coffee`, category `Dining`
   - Pattern `Dunkin`, category `Dining`
2. Ensure the `Coffee` rule is ordered before the `Dunkin` rule by `ModifiedAt` (for example, make `Coffee` newer).
3. Import or match the payee `Dunkin Donuts`.
4. Inspect both rules' `MatchCount` and `LastUsedAt`.

## Expected Behavior

Only the rule that actually matched `Dunkin Donuts` (`Dunkin`) should receive usage statistics for that match. Rules sharing its category should not be treated as having matched unless they independently matched.

## Actual Behavior

`MatchPayeesAsync` obtains the winning category from `PayeeMatchingHelper.FindBestMatch`, then selects the first rule with that category to update usage statistics. Since rules are sorted by `ModifiedAt` descending, a newer, unrelated rule with the same category can receive the update instead of the matching rule.

## Root Cause

The matching helper returns only a category, not the matched rule. `MatchPayeesAsync` then attempts to infer the matching rule from the category, which is not unique. The relevant logic is in `src/Application/Features/PayeeMatchingRuleFeature.cs`, in `MatchPayeesAsync`.

## Suggested Fix

Preserve the identity of the winning rule during matching (for example, return the matched rule or its key along with the category), and use that identity when recording usage statistics. Keep the category result behavior unchanged for the import workflow.

## Regression Test

Add a test with two rules having the same category where only the later-ordered rule matches the payee. Verify that only the matching rule's `MatchCount` and `LastUsedAt` are updated.
