# Transaction split requirements

The initial prototype implementation include "amount" in the "transaction" model. That's not how it will be implemented. Transactions can be split across multiple categories.

Here's the basic idea

Transaction properties:
* Date
* Payee
* Memo
* Source
* Splits[]

Split properties:
* Amount
* Category
* Memo

Get Transactions endpoint will return a DTO that doesn't include splits. Most user interaction is at the transaction level. The amount will be the summed amount of all splits.

In the most common case where a transaction has only one split, this complexity is hidden from the user. They can edit the amount or category, and instead the single split will be edited. (Note they can't edit the SPLIT memo in this case)

Question: The UI will need to show some indicator in place of a category when displaying tabular transactions. Should we include a HasSplits flag in the DTO and then the UI can decide what to do? This will also be a cue to the UI that editing the category will not be allowed.

Next question: Should splits have a Guid key? We WILL need to edit a specific split, so this endpoint will be needed: /api/tenant/{key}/transactions/{key}/splits{something}. This is a place where it MIGHT be ok to expose the underlying bigint? Not sure.
