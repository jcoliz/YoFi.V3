# Transaction Filtering Design

## Filtering features

Transactions have the following properties which can be filtered
* Payee (string)
* Category (string)
* Memo (string)
* Source (string)
* Amount (decimal)
* Date (datetime)

User can filter by... (presented in order of commonality)
1. Single text search, which includes transactions having the text in the payee, category, memo, or amount (if is a decimal-converting string) <- Most common by far
2. Category is blank or whitespace
3. Specific year (most common, and combined with another field typically)
4. Substring search on a single text field
5. Exact amount on amount field
6. Specific Date range (less common)

By default, transactions are filtered by prior 12 months, unless another date option is included

## Front end

Let's start by designing the front end user experience of transaction filtering, before designing the feature and even the transactions schema.

I do want a search bar with at least a search icon present at all times. This covers the most common single text search.

The question is, what do we do with the other search components?

Historically, I've kept it to a text search and then use cryptic text to augment the search, e.g. "c=auto parts,y=2024" for "Category = auto parts, year = 2024".

That doesn't seem like a great user experience so I think we can do better. Perhaps the search bar also has an "expand" icon, which opens up a more detailed pane.

How do other financial sites deal with this, without cluttering the whole page with filtering options. MOST of the time, user is either not filtering, or is using simple text search filter.


