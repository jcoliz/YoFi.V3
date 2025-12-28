# OFX Test Files

This directory contains real-world OFX file examples for testing the bank import functionality. Each file demonstrates different format variations, edge cases, and parsing challenges.

## File Overview

| File | Format | Account Type | Transactions | Key Features |
|------|--------|--------------|--------------|--------------|
| [`Bank1.ofx`](Bank1.ofx) | OFX 1.x (SGML) | Multiple accounts | 11 | Multi-account file, empty transaction list in first account |
| [`CC2.OFX`](CC2.OFX) | OFX 1.x (SGML) | Credit card | 9 | Credit/debit transactions, sequential FITIDs |
| [`creditcard.ofx`](creditcard.ofx) | OFX 1.x (SGML) | Credit card | 4 | Credit card message format (`CREDITCARDMSGSRSV1`) |
| [`bank-banking-xml.ofx`](bank-banking-xml.ofx) | OFX 2.x (XML) | Checking | 7 | XML format with proper XML declaration |
| [`issue-17.ofx`](issue-17.ofx) | OFX 1.x (SGML) | Checking | 3 | Brazilian bank (BRL), timezone handling |
| [`itau.ofx`](itau.ofx) | OFX 1.x (SGML) | Checking | 3 | Brazilian bank (BRL), **identical data to issue-17.ofx** |

**Total:** 6 files, ~37 unique transactions

## Format Variations

### OFX Version Differences

**OFX 1.x (SGML-like)** - 5 files
- No XML declaration
- Tag-based but not well-formed XML
- Example: `<TRNAMT>-87.69` (no closing tag)
- Headers in key-value format: `OFXHEADER:100`

**OFX 2.x (XML)** - 1 file
- Proper XML with declaration: `<?xml version="1.0" encoding="utf-8"?>`
- Well-formed XML with closing tags
- Example: `<TRNAMT>-10</TRNAMT>`
- Headers in XML processing instruction: `<?OFX OFXHEADER="200" ...?>`

### Message Type Variations

**Bank Account Messages** (`BANKMSGSRSV1`) - 5 files
- Used for checking accounts, savings accounts, credit lines
- Contains `<BANKACCTFROM>` with `<BANKID>`, `<ACCTID>`, `<ACCTTYPE>`

**Credit Card Messages** (`CREDITCARDMSGSRSV1`) - 1 file
- Used specifically for credit card accounts
- Contains `<CCACCTFROM>` with just `<ACCTID>` (no BANKID)
- See: [`creditcard.ofx`](creditcard.ofx)

### Transaction Field Variations

Different banks populate fields differently:

| Field | Bank1.ofx | CC2.OFX | creditcard.ofx | bank-banking-xml.ofx |
|-------|-----------|---------|----------------|---------------------|
| `FITID` | ✅ | ✅ | ✅ | ✅ |
| `NAME` | ✅ | ✅ | ✅ | ❌ |
| `MEMO` | ✅ | ❌ | ❌ | ✅ |
| `CHECKNUM` | ✅ (for checks) | ❌ | ❌ | ❌ |

**Implications for parsing:**
- If `NAME` is missing, use `MEMO` as payee
- `CHECKNUM` only present for check transactions
- `FITID` is consistently present (used for duplicate detection)

## Test Scenarios

### Multi-Account Handling
**File:** [`Bank1.ofx`](Bank1.ofx)

Contains 3 separate accounts in one file:
1. Checking account (empty transaction list)
2. Credit line with 8 transactions
3. Checking account with 3 transactions

**Parser must:** Correctly extract transactions from all accounts, handle empty transaction lists without errors.

### Empty Transaction Lists
**File:** [`Bank1.ofx`](Bank1.ofx) (first account)

The first `<STMTTRNRS>` block has:
```
<BANKTRANLIST>
    <DTSTART>20220324
    <DTEND>20220307
</BANKTRANLIST>
```

**Parser must:** Handle empty `<BANKTRANLIST>` without failing.

### Duplicate Detection Testing
**Files:** [`itau.ofx`](itau.ofx) + [`issue-17.ofx`](issue-17.ofx)

These files contain **identical transaction data**:
- Same FITIDs: `20131209001`, `20131209002`, `20131210001`
- Same dates, amounts, and memos
- Same account information

**Test workflow:**
1. Import [`itau.ofx`](itau.ofx) → All 3 transactions should be new
2. Import [`issue-17.ofx`](issue-17.ofx) → All 3 transactions should be flagged as exact duplicates

### International Format Support
**Files:** [`issue-17.ofx`](issue-17.ofx), [`itau.ofx`](itau.ofx)

Features:
- Currency: `BRL` (Brazilian Real) instead of `USD`
- Timezone: `[-03:EST]` format
- Language: `POR` (Portuguese) in itau.ofx
- Different bank identifier format: `<BANKID>0341`

**Parser must:** Handle non-USD currencies, various timezone formats, international character encodings.

### Credit Card Format
**File:** [`creditcard.ofx`](creditcard.ofx)

Uses `<CREDITCARDMSGSRSV1>` instead of `<BANKMSGSRSV1>`:
```xml
<CREDITCARDMSGSRSV1>
  <CCSTMTTRNRS>
    <CCSTMTRS>
      <CCACCTFROM>
        <ACCTID>creditcard78X90X1234X5765</ACCTID>
      </CCACCTFROM>
```

**Parser must:** Support both bank and credit card message formats.

### XML vs SGML Parsing
**XML:** [`bank-banking-xml.ofx`](bank-banking-xml.ofx)
**SGML:** All other files

**Key differences:**
- XML has proper closing tags, SGML does not
- XML has `<?xml?>` declaration, SGML has `OFXHEADER:` lines
- XML requires XML parser, SGML requires lenient SGML/tag-soup parser

### FITID Format Variations
Different banks use different FITID formats:

| Bank | FITID Example | Pattern |
|------|---------------|---------|
| MegaBankCorp | `20220221 469976 8,769 2,022,022,018,019` | Date + ID + Amount + Timestamp |
| Bank of Banking | `202208086798` | Numeric sequence |
| Credit Card Co | `FITID2022081870.2PL22P` | Date + Amount + Random string |
| Brazilian Bank | `20131209001` | Date + Sequence number |

**Parser must:** Accept any string as FITID without format assumptions.

## Parsing Strategy

Based on these examples, the parser should:

1. **Detect format** - Check for XML declaration to determine OFX 1.x vs 2.x
2. **Parse leniently** - Many files are not well-formed XML
3. **Support both message types** - `BANKMSGSRSV1` and `CREDITCARDMSGSRSV1`
4. **Handle multiple accounts** - Iterate through all `<STMTTRNRS>` blocks
5. **Gracefully handle missing fields** - Use fallbacks (NAME → MEMO for payee)
6. **Preserve FITID** - Use as-is for duplicate detection (don't parse or validate format)
7. **Support international data** - Non-USD currencies, various timezones, character encodings

## Usage in Tests

### Unit Tests (Parsing)
- Test each file individually to verify parsing
- Verify transaction count matches expected
- Check field extraction (date, amount, payee, memo, FITID)

### Integration Tests (Duplicate Detection)
- Import [`itau.ofx`](itau.ofx) into clean database
- Import [`issue-17.ofx`](issue-17.ofx) into same tenant
- Verify all transactions flagged as exact duplicates

### End-to-End Tests (Workflow)
- Use [`Bank1.ofx`](Bank1.ofx) for multi-account testing
- Use [`CC2.OFX`](CC2.OFX) for standard import workflow
- Use [`creditcard.ofx`](creditcard.ofx) for credit card format testing

## References

- [OFX Specification](https://financialdataexchange.org/FDX/About/OFX-Work-Group.aspx)
- [PRD: Bank Import](../PRD-BANK-IMPORT.md)
- YoFi V1 OFX Parser: [GitHub](https://github.com/jcoliz/yofi/tree/main/YoFi.Core/Importers)
