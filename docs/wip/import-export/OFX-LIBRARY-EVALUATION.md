---
status: Draft
created: 2025-12-28
related_prd: PRD-BANK-IMPORT.md
---

# OFX Parsing Library Evaluation

## Overview

This document evaluates options for parsing OFX/QFX files in YoFi V3, comparing existing libraries against custom parsing implementations.

## Requirements

Based on the [test file analysis](examples/README.md), the parser must:

1. **Format Support**
   - OFX 1.x (SGML-like format with no closing tags)
   - OFX 2.x (Well-formed XML)
   - QFX (Quicken format, essentially OFX 1.x)

2. **Message Type Support**
   - Bank account transactions (`BANKMSGSRSV1`)
   - Credit card transactions (`CREDITCARDMSGSRSV1`)

3. **Robustness**
   - Handle malformed/lenient SGML (OFX 1.x is not well-formed XML)
   - Support multiple accounts in single file
   - Handle empty transaction lists gracefully
   - Accept various FITID formats without validation

4. **International Support**
   - Non-USD currencies (BRL, EUR, etc.)
   - Various timezone formats
   - Multiple character encodings (UTF-8, Windows-1252, ISO-8859-1)

5. **Field Flexibility**
   - Optional fields (NAME, MEMO, CHECKNUM)
   - Fallback strategies (use MEMO when NAME missing)

## Option 1: OFXSharp (Existing Library)

**Repository:** https://github.com/jcoliz/OFXSharp
**NuGet:** https://www.nuget.org/packages/OFXSharp
**Status:** Used in YoFi V1, proven to work with real-world bank files

### Pros

✅ **Battle-tested** - Already used successfully in YoFi V1
✅ **Known compatibility** - Handles the formats we need (OFX 1.x, 2.x)
✅ **Maintained by project owner** - You control the library and can fix issues
✅ **Lenient parsing** - Designed to handle real-world bank file variations
✅ **Zero learning curve** - You already understand its API and behavior
✅ **Proven with test files** - Works with the exact bank formats you encounter

### Cons

❌ **Limited community adoption** - Not widely used outside YoFi
❌ **Maintenance burden** - Any bugs require you to fix the library separately
❌ **Documentation may be sparse** - If it lacks comprehensive docs
❌ **Unknown test coverage** - May not have comprehensive test suite

### API Example (Estimated)

```csharp
using OFXSharp;

var parser = new OFXParser();
var document = parser.Parse(fileStream);

foreach (var account in document.Accounts)
{
    // Build Source field from account-level metadata
    // e.g., "Bank of Banking - Checking (1234)"
    var source = BuildSourceString(account.BankId, account.AccountType, account.AccountId);

    foreach (var transaction in account.Transactions)
    {
        var dto = new TransactionEditDto
        {
            Date = transaction.Date,
            Amount = transaction.Amount,
            Payee = transaction.Name ?? transaction.Memo,
            Memo = transaction.Memo,
            Source = source, // Account-level metadata
            // Use FITID directly as string for duplicate detection
            // Note: FITID format varies by bank - no assumptions about format
        };
    }
}
```

**Note:** The `Source` field requires account-level metadata (bank name, account type, account ID) which exists in the OFX file structure above individual transactions. The parser must preserve this context when extracting transactions.

### Evaluation Criteria

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Format Coverage | ⭐⭐⭐⭐⭐ | Handles OFX 1.x and 2.x |
| Robustness | ⭐⭐⭐⭐ | Proven with real bank files |
| Maintenance | ⭐⭐⭐ | Requires maintaining separate repo |
| Documentation | ⭐⭐⭐ | Unknown, needs verification |
| Community Support | ⭐⭐ | Limited outside your projects |
| Learning Curve | ⭐⭐⭐⭐⭐ | Already familiar |

**Overall Score: 22/30 (73%)**

## Option 2: OFX.NET (Third-Party Library)

**Repository:** https://github.com/matiasgodoy/OFX.NET (example, verify actual options)
**NuGet:** Various OFX parsing packages exist

### Pros

✅ **Community maintained** - Bugs may be fixed by others
✅ **Potentially more features** - May support additional OFX features
✅ **Broader testing** - More users = more edge cases discovered

### Cons

❌ **Unknown quality** - Would need evaluation of available libraries
❌ **Dependency risk** - Abandonment, breaking changes, security issues
❌ **Learning curve** - New API to learn
❌ **May not handle YoFi's exact use cases** - Might prioritize different scenarios
❌ **Limited .NET options** - OFX libraries more common in other ecosystems

### Research Needed

- Search NuGet for "OFX" and evaluate top packages
- Check last update date, download count, GitHub activity
- Verify support for OFX 1.x SGML parsing (many libraries only support OFX 2.x XML)
- Test against our example files

### Evaluation Criteria (Estimated)

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Format Coverage | ⭐⭐⭐ | Many libraries only support OFX 2.x |
| Robustness | ⭐⭐⭐ | Unknown without testing |
| Maintenance | ⭐⭐⭐⭐ | Community maintained |
| Documentation | ⭐⭐⭐ | Varies by library |
| Community Support | ⭐⭐⭐⭐ | Potentially better |
| Learning Curve | ⭐⭐⭐ | New API to learn |

**Overall Score: 19/30 (63%) - Estimated, requires research**

## Option 3: Custom Parser

**Approach:** Build custom OFX parser using .NET XML libraries (XDocument, XmlReader) or regex

### Pros

✅ **Complete control** - Full control over parsing logic and error handling
✅ **Minimal dependencies** - No external library risk
✅ **Tailored to needs** - Parse only what YoFi needs, ignore rest
✅ **Direct integration** - Can map directly to YoFi DTOs
✅ **Performance optimization** - Can optimize for YoFi's specific use cases

### Cons

❌ **High development cost** - Significant time investment to build and test
❌ **Reinventing the wheel** - OFX parsing is a solved problem
❌ **Format complexity** - OFX 1.x SGML is notoriously difficult to parse
❌ **Edge case handling** - Will encounter many bank-specific quirks
❌ **Ongoing maintenance** - You own all bugs and compatibility issues
❌ **Testing burden** - Need comprehensive test suite for all format variations

### Implementation Approaches

#### 3a. XML-Based Parser (OFX 2.x only)

```csharp
using System.Xml.Linq;

public class OFXParser
{
    public IEnumerable<TransactionEditDto> Parse(Stream stream)
    {
        var doc = XDocument.Load(stream);
        var transactions = doc.Descendants("STMTTRN");

        foreach (var txn in transactions)
        {
            yield return new TransactionEditDto
            {
                Date = ParseDate(txn.Element("DTPOSTED")?.Value),
                Amount = decimal.Parse(txn.Element("TRNAMT")?.Value ?? "0"),
                Payee = txn.Element("NAME")?.Value ?? txn.Element("MEMO")?.Value,
                Memo = txn.Element("MEMO")?.Value
            };
        }
    }
}
```

**Problem:** Only works for OFX 2.x XML. Cannot handle OFX 1.x SGML (which is majority of our test files).

#### 3b. Regex/Tag-Based Parser (OFX 1.x support)

```csharp
public class OFXSGMLParser
{
    public IEnumerable<Transaction> Parse(string content)
    {
        // Find transaction blocks
        var pattern = @"<STMTTRN>(.*?)</STMTTRN>";
        var matches = Regex.Matches(content, pattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var block = match.Groups[1].Value;
            yield return new Transaction
            {
                Amount = ExtractTag(block, "TRNAMT"),
                Date = ExtractTag(block, "DTPOSTED"),
                // ... more fields
            };
        }
    }

    private string ExtractTag(string content, string tagName)
    {
        var pattern = $@"<{tagName}>(.*?)(?=\n<|$)";
        var match = Regex.Match(content, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
```

**Challenges:**
- SGML has no closing tags, hard to know where values end
- Nested structures (account -> transaction list -> transaction)
- Whitespace handling
- Multiple accounts per file
- Credit card vs bank message format differences

### Evaluation Criteria

| Criterion | Rating | Notes |
|-----------|--------|-------|
| Format Coverage | ⭐⭐⭐ | Hard to support OFX 1.x robustly |
| Robustness | ⭐⭐ | Will hit edge cases over time |
| Maintenance | ⭐⭐ | Ongoing burden |
| Documentation | ⭐⭐⭐⭐⭐ | You write it |
| Community Support | ⭐ | None |
| Learning Curve | ⭐⭐⭐ | OFX format is complex |

**Overall Score: 13/30 (43%)**

## Recommendation

### Primary Recommendation: Use OFXSharp

**Rationale:**

1. **Proven track record** - Already works in YoFi V1 with real-world bank files
2. **Known compatibility** - Handles the exact formats in our test files
3. **No learning curve** - You're already familiar with it
4. **Control when needed** - You can fix bugs since you own the library
5. **Low risk** - Not starting from scratch, not depending on unknown third party

**Implementation Plan:**

```yaml
Phase 1: Integrate OFXSharp
  - Add NuGet package reference
  - Create wrapper service: IOFXParsingService
  - Test against all 6 example files
  - Document any library limitations discovered

Phase 2: Enhancement (if needed)
  - Fix any bugs discovered in OFXSharp
  - Add missing features if required
  - Improve error messages for user-facing errors
```

### Alternative Recommendation: Evaluate Third-Party Libraries First

**Only if** you want to reduce maintenance burden:

1. Research available .NET OFX libraries on NuGet
2. Test top 2-3 candidates against all 6 example files
3. Compare feature completeness vs OFXSharp
4. If a library is clearly superior AND actively maintained, consider switching

**Decision criteria:**
- Must handle OFX 1.x SGML (most libraries don't)
- Must parse all 6 example files correctly
- Last updated within 12 months
- 1000+ downloads or active GitHub

### Not Recommended: Custom Parser

**Reasons:**
- High development cost (2-4 weeks)
- OFX 1.x SGML is notoriously difficult to parse correctly
- Will encounter edge cases that OFXSharp already handles
- Testing burden is significant
- Ongoing maintenance overhead

**Only build custom parser if:**
- OFXSharp fails on critical files AND cannot be fixed
- No third-party alternatives exist AND OFXSharp is abandoned
- Parser needs to be embedded in performance-critical context

## Implementation Approach

### Recommended Architecture (Using OFXSharp)

```
User uploads file
    ↓
[TransactionImportController]
    ↓
[TransactionImportFeature]
    ↓
[IOFXParsingService] ← OFXSharp wrapper
    ↓
[OFXSharp Library]
    ↓
Returns: IEnumerable<TransactionEditDto>
    ↓
[TransactionImportFeature]
- Duplicate detection (compare Keys/hashes)
- Store in import review state
    ↓
[ImportReviewRepository]
```

### Service Interface

```csharp
public interface IOFXParsingService
{
    /// <summary>
    /// Parses OFX/QFX file and returns transaction data.
    /// </summary>
    /// <param name="fileStream">OFX/QFX file stream</param>
    /// <param name="fileName">Original filename for error messages</param>
    /// <returns>Parsed transactions with parsing metadata</returns>
    /// <exception cref="OFXParsingException">When file cannot be parsed</exception>
    Task<OFXParsingResult> ParseAsync(Stream fileStream, string fileName);
}

public class OFXParsingResult
{
    public IReadOnlyCollection<TransactionEditDto> Transactions { get; init; }
    public IReadOnlyCollection<OFXParsingError> Errors { get; init; }
    public OFXFileMetadata Metadata { get; init; }
}

public class OFXParsingError
{
    public int TransactionIndex { get; init; }
    public string Message { get; init; }
    public string Detail { get; init; }
}

public class OFXFileMetadata
{
    public string BankId { get; init; }
    public string AccountId { get; init; }
    public string Currency { get; init; }
    public DateTime FileDate { get; init; }
}
```

**Important:** The `Source` field in [`TransactionEditDto`](../../../src/Application/Dto/TransactionEditDto.cs) must be populated from account-level metadata (bank name, account type, account ID), not just transaction-level fields. The parser service must preserve account context when iterating through transactions.

### Error Handling Strategy

```csharp
public class OFXParsingService : IOFXParsingService
{
    public async Task<OFXParsingResult> ParseAsync(Stream fileStream, string fileName)
    {
        try
        {
            var parser = new OFXParser();
            var document = await parser.ParseAsync(fileStream);

            var transactions = new List<TransactionEditDto>();
            var errors = new List<OFXParsingError>();

            int index = 0;
            foreach (var account in document.Accounts)
            {
                foreach (var txn in account.Transactions)
                {
                    try
                    {
                        transactions.Add(MapToDto(txn));
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new OFXParsingError
                        {
                            TransactionIndex = index,
                            Message = "Failed to parse transaction",
                            Detail = ex.Message
                        });
                    }
                    index++;
                }
            }

            return new OFXParsingResult
            {
                Transactions = transactions,
                Errors = errors,
                Metadata = ExtractMetadata(document)
            };
        }
        catch (OFXException ex)
        {
            throw new OFXParsingException(
                $"Failed to parse OFX file '{fileName}': {ex.Message}",
                fileName,
                ex
            );
        }
    }
}
```

## Testing Strategy

### Unit Tests (Against Example Files)

```csharp
[TestFixture]
public class OFXParsingServiceTests
{
    [Test]
    public async Task ParseAsync_Bank1Ofx_Parses11Transactions()
    {
        // Given: Bank1.ofx file with 11 transactions across 3 accounts
        var fileStream = File.OpenRead("examples/Bank1.ofx");

        // When: File is parsed
        var result = await _parsingService.ParseAsync(fileStream, "Bank1.ofx");

        // Then: All 11 transactions are extracted
        Assert.That(result.Transactions, Has.Count.EqualTo(11));

        // And: No parsing errors
        Assert.That(result.Errors, Is.Empty);

        // And: First transaction has expected values
        var first = result.Transactions.First();
        Assert.That(first.Amount, Is.EqualTo(-87.69m));
        Assert.That(first.Date, Is.EqualTo(new DateTime(2022, 2, 21)));
        Assert.That(first.Payee, Does.Contain("SPRING GLE"));
    }

    [Test]
    public async Task ParseAsync_XmlFormat_ParsesCorrectly()
    {
        // Given: OFX 2.x XML format file
        var fileStream = File.OpenRead("examples/bank-banking-xml.ofx");

        // When: File is parsed
        var result = await _parsingService.ParseAsync(fileStream, "bank-banking-xml.ofx");

        // Then: All transactions are extracted
        Assert.That(result.Transactions, Has.Count.EqualTo(7));
    }

    [Test]
    public async Task ParseAsync_CreditCardFormat_ParsesCorrectly()
    {
        // Given: Credit card format (CREDITCARDMSGSRSV1)
        var fileStream = File.OpenRead("examples/creditcard.ofx");

        // When: File is parsed
        var result = await _parsingService.ParseAsync(fileStream, "creditcard.ofx");

        // Then: All transactions are extracted
        Assert.That(result.Transactions, Has.Count.EqualTo(4));
    }
}
```

### Integration Tests (Full Import Workflow)

Test the complete flow from file upload through duplicate detection to import review state.

## Open Questions

- [ ] Does OFXSharp require any updates for .NET 8 compatibility?
- [ ] What is the exact API of OFXSharp? (Need to review repo)
- [ ] Are there known bugs in OFXSharp that need fixing?
- [ ] Should we vendor OFXSharp into YoFi V3 repo instead of using NuGet?
- [ ] What license is OFXSharp under? (Verify compatibility)

## Next Steps

1. Review OFXSharp repository and documentation
2. Verify OFXSharp NuGet package works with .NET 8
3. Create proof-of-concept parsing one example file
4. Test against all 6 example files
5. Document any issues or required library fixes
6. Proceed with data model design for import review state

## References

- [OFXSharp Repository](https://github.com/jcoliz/OFXSharp)
- [PRD: Bank Import](PRD-BANK-IMPORT.md)
- [Test File Analysis](examples/README.md)
- [OFX Specification](https://www.ofx.net/)
- YoFi V1 OFX Usage: https://github.com/jcoliz/yofi/tree/main/YoFi.Core/Importers
