---
status: Draft
created: 2025-12-28
related_docs:
  - OFX-LIBRARY-EVALUATION.md
  - PRD-BANK-IMPORT.md
decision: Use git submodule temporarily during Bank Import feature development, then switch to NuGet
---

# OFXSharp Integration Decision: Submodule vs NuGet

## Context

OFXSharp library is locally available at `C:\Source\jcoliz\OFXSharp\source\OfxSharp\OfxSharp.csproj`. The YoFi V3 project already uses git submodules (see `submodules/NuxtIdentity`), so the pattern is established.

## Library Analysis

### Key Properties

**Technology:**
- Target Framework: `netstandard2.0` (compatible with .NET 8)
- License: MIT (no restrictions)
- NuGet Package: `jcoliz.OfxSharp.NetStandard`
- Dependencies:
  - `Microsoft.Xml.SgmlReader` (1.8.28) - Handles OFX 1.x SGML parsing
  - `System.Text.Encoding.CodePages` (7.0.0) - Character encoding support

### API Structure

**Models:**
- [`OfxDocument`](C:\Source\jcoliz\OFXSharp\source\OfxSharp\Model\OFXDocument.cs) - Root document with SignOn and Statements
- [`OfxStatementResponse`] - Contains account info and transactions
- [`Account`](C:\Source\jcoliz\OFXSharp\source\OfxSharp\Model\Account.cs) - Base class with `BankAccount` and `CreditAccount` subtypes
- [`Transaction`](C:\Source\jcoliz\OFXSharp\source\OfxSharp\Model\Transaction.cs) - Individual transaction with all OFX fields

**Key Transaction Properties:**
```csharp
public class Transaction
{
    public string TransactionId { get; }        // FITID
    public DateTimeOffset? Date { get; }        // DTPOSTED
    public decimal Amount { get; }              // TRNAMT
    public string Name { get; }                 // NAME (can be null)
    public string Memo { get; }                 // MEMO (can be null)
    public string CheckNum { get; }             // CHECKNUM (optional)
    public OfxTransactionType TransType { get; } // DEBIT/CREDIT/etc
    public string EffectiveCurrency { get; }    // Resolved currency
    // ... more fields
}
```

**Key Account Properties:**
```csharp
public abstract class Account
{
    public string AccountId { get; }   // ACCTID
    public AccountType AccountType { get; } // BANK or CC
}

public class BankAccount : Account
{
    public string BankId { get; }              // BANKID
    public BankAccountType BankAccountType { get; } // CHECKING, SAVINGS, CREDITLINE, etc.
}

public class CreditAccount : Account
{
    // CreditAccount has no additional fields beyond base Account
}
```

### Building Source Field

The `Source` field for YoFi transactions requires combining:
1. Bank name (from `SignOnResponse.FinancialInstitution`)
2. Account type (from `Account.BankAccountType` or "Credit Card")
3. Account ID (from `Account.AccountId`)

Example: `"Bank of Banking - Checking (1234)"`

## Integration Options

### Option 1: Git Submodule (Recommended)

**Approach:**
```bash
cd c:/Source/jcoliz/YoFi.V3
git submodule add https://github.com/jcoliz/OFXSharp.git submodules/OFXSharp
```

**Project Reference:**
```xml
<ItemGroup>
  <ProjectReference Include="..\..\submodules\OFXSharp\source\OfxSharp\OfxSharp.csproj" />
</ItemGroup>
```

#### Pros

✅ **Full control** - Can make changes immediately without separate library release
✅ **Consistent with project pattern** - Already using submodules for NuxtIdentity
✅ **No NuGet dependency** - No risk of package unavailability
✅ **Debugging ease** - Can step directly into library code
✅ **Rapid iteration** - Fix bugs and test in same development cycle
✅ **Version control** - Exact library version pinned via submodule commit

#### Cons

❌ **Submodule complexity** - Team members must `git submodule update --init`
❌ **Build overhead** - Library rebuilds when doing clean builds
❌ **No isolation** - Library changes affect YoFi immediately

#### Implementation Steps

1. Add OFXSharp as git submodule
2. Add project reference in `src/Application/YoFi.V3.Application.csproj`
3. Update CI/CD pipeline to initialize submodules
4. Document submodule usage in README

### Option 2: NuGet Package

**Package:** `jcoliz.OfxSharp.NetStandard`

**Project Reference:**
```xml
<ItemGroup>
  <PackageReference Include="jcoliz.OfxSharp.NetStandard" Version="2.0.0" />
</ItemGroup>
```

#### Pros

✅ **Simple dependency** - Standard NuGet workflow
✅ **Build performance** - No recompilation of library
✅ **Clear versioning** - Explicit package versions

#### Cons

❌ **Separate release cycle** - Must publish NuGet package before using fixes
❌ **Slower iteration** - Bug fixes require: fix → test → publish → update → test
❌ **Package management** - Need to maintain NuGet feed and versions
❌ **Less control** - Cannot make quick fixes during development

#### Implementation Steps

1. Verify latest NuGet package version
2. Add package reference to Application project
3. Test against example files
4. Update package as needed (requires publishing new versions)

### Option 3: Local NuGet Source

**Hybrid approach:** Build local NuGet packages from source.

#### Pros

✅ **Package workflow** - Maintains NuGet separation
✅ **Local control** - Can build packages locally

#### Cons

❌ **Extra complexity** - Adds local package management overhead
❌ **Slower than submodule** - Still requires package build step
❌ **No clear advantage** - Combines disadvantages of both approaches

## Recommendation: Use Git Submodule (Temporarily)

**Rationale:**

1. **Matches existing pattern** - YoFi V3 already uses submodules for NuxtIdentity
2. **Active development phase** - During Bank Import feature development (Beta 2), being able to fix OFXSharp bugs immediately is valuable
3. **You control both repos** - No dependency on external maintainers
4. **Faster iteration** - Fix bugs in OFXSharp and test in YoFi V3 in same session
5. **Better debugging** - Step through library code during development

**Migration Plan:**
1. **During Bank Import Development:** Use submodule for maximum flexibility
2. **After Feature Complete:** Once Bank Import feature is tested and stable, release updated OFXSharp to NuGet if changes were made
3. **Switch to NuGet:** Replace project reference with NuGet package reference
4. **Remove Submodule:** Clean up submodule from repository

This temporary approach provides maximum development flexibility while maintaining a clean long-term dependency structure.

## Implementation Plan

### Phase 1: Add Submodule

```bash
# Add OFXSharp as submodule
cd c:/Source/jcoliz/YoFi.V3
git submodule add https://github.com/jcoliz/OFXSharp.git submodules/OFXSharp

# Initialize submodule for first time
git submodule update --init --recursive
```

### Phase 2: Add Project Reference

Update `src/Application/YoFi.V3.Application.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\submodules\OFXSharp\source\OfxSharp\OfxSharp.csproj" />
</ItemGroup>
```

### Phase 3: Create Wrapper Service

Create `src/Application/Import/Services/OFXParsingService.cs`:

```csharp
using OfxSharp;

public class OFXParsingService : IOFXParsingService
{
    public async Task<OFXParsingResult> ParseAsync(Stream fileStream, string fileName)
    {
        // Parse OFX document
        var document = OfxDocumentReader.FromStream(fileStream);

        var transactions = new List<TransactionEditDto>();

        // Extract transactions from all statements
        foreach (var statement in document.Statements)
        {
            var account = statement.Account;
            var source = BuildSourceString(document.SignOn, account);

            foreach (var txn in statement.Transactions)
            {
                transactions.Add(new TransactionEditDto
                {
                    Date = txn.Date?.DateTime ?? DateTime.MinValue,
                    Amount = txn.Amount,
                    Payee = txn.Name ?? txn.Memo ?? "Unknown",
                    Memo = txn.Memo,
                    Source = source,
                    // Note: FITID format varies by bank - use as-is for duplicate detection
                });
            }
        }

        return new OFXParsingResult
        {
            Transactions = transactions,
            Errors = new List<OFXParsingError>(),
            Metadata = ExtractMetadata(document)
        };
    }

    private string BuildSourceString(SignOnResponse signOn, Account account)
    {
        var bankName = signOn.FinancialInstitution?.Name ?? "Unknown Bank";
        var accountType = account switch
        {
            Account.BankAccount ba => ba.BankAccountType.ToString(),
            Account.CreditAccount => "Credit Card",
            _ => "Unknown"
        };
        var accountId = account.AccountId ?? "Unknown";

        return $"{bankName} - {accountType} ({accountId})";
    }
}
```

### Phase 4: Update CI/CD

Ensure CI/CD pipelines initialize submodules:

```yaml
# GitHub Actions example
- name: Checkout code
  uses: actions/checkout@v4
  with:
    submodules: 'recursive'
```

### Phase 5: Document Usage

Update `README.md` with submodule instructions:

    ## Development Setup

    This project uses git submodules. After cloning, initialize them:

    ```bash
    git submodule update --init --recursive
    ```

    When pulling changes that update submodules:

    ```bash
    git pull
    git submodule update --recursive
    ```

> [!NOTE]: YoFi.V3 already uses NuxtIdentity as a submodule, so this is not new.

## Testing Strategy

1. Test parsing all 6 example files with OFXSharp
2. Verify `Source` field construction from various bank formats
3. Test FITID extraction and duplicate detection key generation
4. Verify handling of missing NAME field (fallback to MEMO)
5. Test multi-account files (Bank1.ofx has 3 accounts)

## OFXSharp Modification Strategy

**Important:** Since OFXSharp is included as a submodule during development, we should NOT feel constrained by the library's current limitations. Any needed changes to support Bank Import requirements should be made directly to OFXSharp.

**Examples of modifications we might make:**
- Add better error handling and recovery
- Improve parsing of specific OFX variations
- Add convenience methods for common operations
- Enhance support for edge cases found in test files
- Add detailed logging for debugging
- Improve performance for large files

**Process:**
1. Identify limitation or needed feature
2. Make changes directly in `submodules/OFXSharp`
3. Test changes against all example files
4. Document changes for future NuGet release
5. Continue with YoFi V3 development

This approach treats OFXSharp as part of the YoFi V3 codebase during development, providing maximum flexibility.

## Open Questions

- [ ] Does OFXSharp need any updates for better .NET 8 compatibility?
- [ ] How to use FITID for duplicate detection? (FITID is bank-specific string, not GUID - no format assumptions)
- [ ] Should we add structured logging to OFXSharp for better diagnostics?
- [ ] Do we need to enhance error messages for user-facing scenarios?

## Next Steps

1. Add OFXSharp as git submodule
2. Create IOFXParsingService interface in Application layer
3. Implement OFXParsingService wrapper
4. Write unit tests against all 6 example files
5. Verify Source field construction
6. Design duplicate detection key strategy (FITID vs hash)

## References

- [OFXSharp Repository](https://github.com/jcoliz/OFXSharp)
- [OFX Library Evaluation](OFX-LIBRARY-EVALUATION.md)
- [PRD: Bank Import](PRD-BANK-IMPORT.md)
- [Example Files](examples/README.md)
