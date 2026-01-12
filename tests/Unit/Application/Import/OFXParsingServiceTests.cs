namespace YoFi.V3.Tests.Unit.Application.Import;

using NUnit.Framework;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Dto;

/// <summary>
/// Unit tests for OFX parsing helper.
/// </summary>
/// <remarks>
/// These tests follow strict TDD approach, building incrementally from simplest to most complex scenarios.
/// All tests are marked [Explicit] initially and will be enabled one at a time during implementation.
/// </remarks>
[TestFixture]
public class OfxParsingServiceTests
{
    [Test]
    public async Task ParseAsync_NullStream_ReturnsEmptyResult()
    {
        // Given: A null stream
        Stream? nullStream = null;

        // When: Parsing the null stream
        var result = await OfxParsingHelper.ParseAsync(nullStream!, "test.ofx");

        // Then: Should return empty result (no transactions, no errors)
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Transactions, Is.Not.Null);
        Assert.That(result.Transactions, Is.Empty);
        Assert.That(result.Errors, Is.Not.Null);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public async Task ParseAsync_EmptyStream_ReturnsEmptyResult()
    {
        // Given: An empty stream
        var emptyStream = new MemoryStream();

        // When: Parsing the empty stream
        var result = await OfxParsingHelper.ParseAsync(emptyStream, "empty.ofx");

        // Then: Should return empty result (no transactions, no errors)
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Transactions, Is.Empty);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public async Task ParseAsync_InvalidOfx_ReturnsErrorResult()
    {
        // Given: A stream containing invalid OFX data
        var invalidData = "This is not valid OFX data"u8.ToArray();
        var invalidStream = new MemoryStream(invalidData);

        // When: Parsing the invalid OFX
        var result = await OfxParsingHelper.ParseAsync(invalidStream, "invalid.ofx");

        // Then: Should return result with error information
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Errors, Is.Not.Empty);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));

        // And: Error should include the file name
        Assert.That(result.Errors.First().FileName, Is.EqualTo("invalid.ofx"));
    }

    [Test]
    public async Task ParseAsync_ValidOfxWithZeroTransactions_ReturnsEmptyTransactionList()
    {
        // Given: A valid OFX document with no transactions
        var ofxContent = OfxTestDataBuilder.BuildBankStatement();
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "zero-transactions.ofx");

        // Then: Should return result with empty transaction list
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Transactions, Is.Not.Null);
        Assert.That(result.Transactions, Is.Empty);
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_ExtractsDate()
    {
        // Given: An OFX document with a single transaction containing a date
        var expectedDate = new DateOnly(2023, 11, 15);
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (expectedDate, -50.00m, "Test Payee", null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with correct date extracted
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.Count, Is.EqualTo(1));
        Assert.That(result.Transactions.First().Date, Is.EqualTo(expectedDate));
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_ExtractsAmount()
    {
        // Given: An OFX document with a single transaction containing an amount
        var expectedAmount = -50.00m;
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), expectedAmount, "Test Payee", null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));


        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with correct amount extracted
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Amount, Is.EqualTo(expectedAmount));
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_ExtractsPayee()
    {
        // Given: An OFX document with a single transaction containing NAME field
        var expectedPayee = "Test Payee Store";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, expectedPayee, null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with payee extracted from NAME field
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Payee, Is.EqualTo(expectedPayee));
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_UsesMemoAsFallbackWhenNameMissing()
    {
        // Given: An OFX document with a transaction that has MEMO but no NAME field
        var expectedPayee = "Transaction memo text";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, null, expectedPayee)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "memo-fallback.ofx");

        // Then: Should use MEMO as payee since NAME is missing
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Payee, Is.EqualTo(expectedPayee));
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_ExtractsMemo()
    {
        // Given: An OFX document with a single transaction containing MEMO field
        var expectedMemo = "Test transaction memo";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, "Test Payee", expectedMemo)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with memo extracted
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Memo, Is.EqualTo(expectedMemo));
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_UsesMemoWhenNameIsTruncated()
    {
        // Given: An OFX document where NAME is a truncated version of MEMO
        var truncatedName = "THIS IS A TE";
        var fullMemo = "THIS IS A TEST OF A LONG PAYEE IN A MEMO";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, truncatedName, fullMemo)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "truncated-name.ofx");

        // Then: Should use full MEMO as payee and leave memo blank (discard truncated NAME)
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Payee, Is.EqualTo(fullMemo));
        Assert.That(result.Transactions.First().Memo, Is.Null);
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_UsesMemoWhenNameIsTruncated_WithCollapsedWhitespace()
    {
        // Given: An OFX document where NAME is a truncated version of MEMO, with extra spaces
        var truncatedName = "THIS IS      A TE";
        var fullMemo = "THIS IS A TEST OF A LONG PAYEE IN A MEMO";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, truncatedName, fullMemo)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "truncated-name.ofx");

        // Then: Should use full MEMO as payee and leave memo blank (discard truncated NAME)
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Payee, Is.EqualTo(fullMemo));
        Assert.That(result.Transactions.First().Memo, Is.Null);
    }

    [Test]
    public async Task ParseAsync_SingleTransaction_BuildsSourceString()
    {
        // Given: An OFX document with bank name, account type, and account ID
        var expectedSource = "Test Bank - Checking (9876543210)";
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, "Test Payee", null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with source formatted as 'Bank - AccountType (ID)'
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Source, Is.EqualTo(expectedSource));
    }

    [Test]
    public async Task ParseAsync_MultipleTransactions_ReturnsAll()
    {
        // Given: An OFX document with multiple transactions
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            "Test Bank",
            "9876543210",
            "CHECKING",
            (new DateOnly(2023, 11, 15), -50.00m, "Payee One", null),
            (new DateOnly(2023, 11, 16), 100.00m, "Payee Two", null),
            (new DateOnly(2023, 11, 17), -25.50m, "Payee Three", null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));


        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "multiple-transactions.ofx");

        // Then: Should return all transactions in result
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.Count, Is.EqualTo(3));
        Assert.That(result.Transactions.ElementAt(0).Payee, Is.EqualTo("Payee One"));
        Assert.That(result.Transactions.ElementAt(1).Payee, Is.EqualTo("Payee Two"));
        Assert.That(result.Transactions.ElementAt(2).Payee, Is.EqualTo("Payee Three"));
    }

    [Test]
    public async Task ParseAsync_MultipleAccounts_HandlesEachSeparately()
    {
        // Given: An OFX document with multiple account statements
        var ofxContent = OfxTestDataBuilder.BuildMultiAccountStatement(
            "Test Bank",
            ("1111111111", "CHECKING", [(new DateOnly(2023, 11, 15), -50.00m, "Checking Transaction", null)]),
            ("2222222222", "SAVINGS", [(new DateOnly(2023, 11, 16), 100.00m, "Savings Transaction", null)])
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));


        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "multi-account.ofx");

        // Then: Should return transactions from all accounts with correct source for each
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.Count, Is.EqualTo(2));

        var checkingTxn = result.Transactions.First(t => t.Payee == "Checking Transaction");
        Assert.That(checkingTxn.Source, Is.EqualTo("Test Bank - Checking (1111111111)"));

        var savingsTxn = result.Transactions.First(t => t.Payee == "Savings Transaction");
        Assert.That(savingsTxn.Source, Is.EqualTo("Test Bank - Savings (2222222222)"));
    }

    #region Example File Tests

    /// <summary>
    /// Helper method to load embedded OFX sample files from the test project.
    /// </summary>
    /// <param name="fileName">Name of the OFX file (e.g., "Bank1.ofx")</param>
    /// <returns>Stream containing the file contents</returns>
    private static Stream GetEmbeddedOfxFile(string fileName)
    {
        var assembly = typeof(OfxParsingServiceTests).Assembly;
        var resourceName = $"YoFi.V3.Tests.Unit.SampleData.Ofx.{fileName}";
        var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
        }

        return stream;
    }

    [Test]
    public async Task ParseAsync_BankBankingXmlOfx_ParsesSuccessfully()
    {
        // Given: The bank-banking-xml.ofx example file (OFX 2.x XML format)
        using var stream = GetEmbeddedOfxFile("bank-banking-xml.ofx");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "bank-banking-xml.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (7 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(7), "Expected 7 transactions from bank-banking-xml.ofx");

        // And: Should extract bank name correctly
        Assert.That(result.Transactions.First().Source, Does.Contain("Bank of Banking"));
    }

    [Test]
    public async Task ParseAsync_Bank1Ofx_ParsesSuccessfully()
    {
        // Given: The Bank1.ofx example file (multiple accounts, 11 total transactions)
        using var stream = GetEmbeddedOfxFile("Bank1.ofx");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "Bank1.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (11 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(11), "Expected 11 transactions from Bank1.ofx");

        // And: Should handle multiple account types
        var sources = result.Transactions.Select(t => t.Source).Distinct().ToList();
        Assert.That(sources.Count, Is.GreaterThan(1), "Expected transactions from multiple accounts");
    }

    [Test]
    public async Task ParseAsync_CC2Ofx_ParsesSuccessfully()
    {
        // Given: The CC2.OFX example file (credit card, 9 transactions)
        using var stream = GetEmbeddedOfxFile("CC2.OFX");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "CC2.OFX");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (9 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(9), "Expected 9 transactions from CC2.OFX");

        // And: All transactions should have payees (NAME field present)
        Assert.That(result.Transactions.All(t => !string.IsNullOrWhiteSpace(t.Payee)), Is.True);
    }

    [Test]
    public async Task ParseAsync_CreditcardOfx_ParsesSuccessfully()
    {
        // Given: The creditcard.ofx example file (credit card message format, 4 transactions)
        using var stream = GetEmbeddedOfxFile("creditcard.ofx");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "creditcard.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (4 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(4), "Expected 4 transactions from creditcard.ofx");

        // And: Should handle CREDITCARDMSGSRSV1 format
        Assert.That(result.Transactions.All(t => !string.IsNullOrWhiteSpace(t.Payee)), Is.True);
    }

    [Test]
    public async Task ParseAsync_Issue17Ofx_ParsesSuccessfully()
    {
        // Given: The issue-17.ofx example file (Brazilian bank, BRL currency, 3 transactions)
        using var stream = GetEmbeddedOfxFile("issue-17.ofx");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "issue-17.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (3 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(3), "Expected 3 transactions from issue-17.ofx");

        // And: Should handle international format (BRL currency, timezone)
        Assert.That(result.Transactions.All(t => t.Amount != 0), Is.True);
    }

    [Test]
    public async Task ParseAsync_ItauOfx_ParsesSuccessfully()
    {
        // Given: The itau.ofx example file (Brazilian bank, identical data to issue-17.ofx)
        using var stream = GetEmbeddedOfxFile("itau.ofx");

        // When: Parsing the OFX file
        var result = await OfxParsingHelper.ParseAsync(stream, "itau.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");

        // And: Should contain expected number of transactions (3 according to README)
        Assert.That(result.Transactions.Count, Is.EqualTo(3), "Expected 3 transactions from itau.ofx");

        // And: Should handle Portuguese language and international format
        Assert.That(result.Transactions.All(t => t.Amount != 0), Is.True);
    }

    [Test]
    public async Task ParseAsync_TransactionsWithFitid_UsesFitidAsExternalId()
    {
        // Given: An OFX document with transactions that have valid FITIDs
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, "Test Payee", "Test memo")
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "with-fitid.ofx");

        // Then: Should successfully parse with no errors
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions.Count, Is.EqualTo(1));

        // And: Transaction should use FITID as ExternalId (not generate a hash)
        var txn = result.Transactions.First();
        Assert.That(txn.ExternalId, Is.EqualTo("TXN001"),
            "When FITID is present, it should be used as ExternalId");
    }

    [Test]
    public async Task ParseAsync_TransactionWithoutPayee_ErrorIncludesFileName()
    {
        // Given: An OFX document with a transaction that has no NAME and no MEMO
        var ofxContent = OfxTestDataBuilder.BuildBankStatement(
            transactions: (new DateOnly(2023, 11, 15), -50.00m, null, null)
        );
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));

        // When: Parsing the OFX document
        var result = await OfxParsingHelper.ParseAsync(stream, "missing-payee.ofx");

        // Then: Should have an error about missing payee
        Assert.That(result.Errors, Is.Not.Empty);
        Assert.That(result.Errors.First().Message, Does.Contain("has no payee"));

        // And: Error should include the file name
        Assert.That(result.Errors.First().FileName, Is.EqualTo("missing-payee.ofx"));

        // And: Transaction should be skipped
        Assert.That(result.Transactions, Is.Empty);
    }

    [Test]
    public async Task ParseAsync_TransactionWithoutFitid_FailsToParse()
    {
        // Given: OFX file with a transaction that has no FITID
        var ofxContent = BuildOfxWithoutFitid(new DateOnly(2024, 1, 15), -100.00m, "Test Payee", "Test Memo");

        // When: File is parsed
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var result = await OfxParsingHelper.ParseAsync(stream, "test.ofx");

        // Then: Should fail to parse due to missing FITID
        Assert.That(result.Errors, Is.Not.Empty, "Expected parsing error for missing FITID");
        Assert.That(result.Errors.First().Message, Does.Contain("FITID"),
            "Error message should mention missing FITID");

        // And: No transactions should be extracted
        Assert.That(result.Transactions, Is.Empty,
            "No transactions should be extracted when FITID is missing");
    }

    /// <summary>
    /// Builds OFX content with a single transaction without FITID.
    /// </summary>
    /// <remarks>
    /// This OFX will fail to parse due to missing FITID, demonstrating that
    /// GenerateTransactionHash cannot be reached through normal parsing.
    /// </remarks>
    private static string BuildOfxWithoutFitid(DateOnly date, decimal amount, string name, string? memo)
    {
        var nameTag = $"<NAME>{name}";
        var memoTag = !string.IsNullOrEmpty(memo) ? $"<MEMO>{memo}" : "";

        return $"""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRSV1>
            <SONRS>
            <STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <DTSERVER>20240115120000
            <LANGUAGE>ENG
            <FI><ORG>Test Bank<FID>12345</FI>
            </SONRS>
            </SIGNONMSGSRSV1>
            <BANKMSGSRSV1>
            <STMTTRNRS>
            <TRNUID>1
            <STATUS>
            <CODE>0
            <SEVERITY>INFO
            </STATUS>
            <STMTRS>
            <CURDEF>USD
            <BANKACCTFROM>
            <BANKID>123456789
            <ACCTID>123456
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20240101120000
            <DTEND>20240131120000
            <STMTTRN>
            <TRNTYPE>{(amount < 0 ? "DEBIT" : "CREDIT")}
            <DTPOSTED>{date:yyyyMMdd}120000
            <TRNAMT>{amount}
            {nameTag}
            {memoTag}
            </STMTTRN>
            </BANKTRANLIST>
            <LEDGERBAL>
            <BALAMT>1000.00
            <DTASOF>20240131120000
            </LEDGERBAL>
            </STMTRS>
            </STMTTRNRS>
            </BANKMSGSRSV1>
            </OFX>
            """;
    }

    #endregion
}

/// <summary>
/// Builder for creating test OFX documents with various configurations.
/// </summary>
/// <remarks>
/// This builder simplifies test data creation by parameterizing only the essential transaction fields
/// while maintaining valid OFX structure. Use this for synthetic test data; use embedded resources
/// for real-world OFX files from banks.
/// </remarks>
internal static class OfxTestDataBuilder
{
    /// <summary>
    /// Builds a single-account OFX bank statement with specified transactions.
    /// </summary>
    /// <param name="bankName">Name of the bank (default: "Test Bank")</param>
    /// <param name="accountId">Account identifier (default: "9876543210")</param>
    /// <param name="accountType">Account type (default: "CHECKING")</param>
    /// <param name="transactions">Array of transactions as (date, amount, name, memo) tuples</param>
    /// <returns>Complete OFX document as string</returns>
    public static string BuildBankStatement(
        string bankName = "Test Bank",
        string accountId = "9876543210",
        string accountType = "CHECKING",
        params (DateOnly date, decimal amount, string? name, string? memo)[] transactions)
    {
        var txnList = string.Join("\n", transactions.Select((t, i) =>
            BuildTransaction(i + 1, t.date, t.amount, t.name, t.memo)));

        return $"""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRSV1>
            <SONRS>
            <STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <DTSERVER>20231201120000
            <LANGUAGE>ENG
            <FI><ORG>{bankName}<FID>12345</FI>
            </SONRS>
            </SIGNONMSGSRSV1>
            <BANKMSGSRSV1>
            <STMTTRNRS>
            <TRNUID>1
            <STATUS>
            <CODE>0
            <SEVERITY>INFO
            </STATUS>
            <STMTRS>
            <CURDEF>USD
            <BANKACCTFROM>
            <BANKID>123456789
            <ACCTID>{accountId}
            <ACCTTYPE>{accountType}
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            {txnList}
            </BANKTRANLIST>
            <LEDGERBAL>
            <BALAMT>1000.00
            <DTASOF>20231130120000
            </LEDGERBAL>
            </STMTRS>
            </STMTTRNRS>
            </BANKMSGSRSV1>
            </OFX>
            """;
    }

    /// <summary>
    /// Builds a multi-account OFX bank statement with separate transactions per account.
    /// </summary>
    /// <param name="bankName">Name of the bank (default: "Test Bank")</param>
    /// <param name="accounts">Array of accounts as (accountId, accountType, transactions) tuples</param>
    /// <returns>Complete OFX document as string</returns>
    public static string BuildMultiAccountStatement(
        string bankName = "Test Bank",
        params (string accountId, string accountType, (DateOnly date, decimal amount, string? name, string? memo)[] transactions)[] accounts)
    {
        var accountStatements = string.Join("\n", accounts.Select((acc, i) =>
        {
            var txnList = string.Join("\n", acc.transactions.Select((t, j) =>
                BuildTransaction(i * 100 + j + 1, t.date, t.amount, t.name, t.memo)));

            return $"""
                <STMTTRNRS>
                <TRNUID>{i + 1}
                <STATUS>
                <CODE>0
                <SEVERITY>INFO
                </STATUS>
                <STMTRS>
                <CURDEF>USD
                <BANKACCTFROM>
                <BANKID>123456789
                <ACCTID>{acc.accountId}
                <ACCTTYPE>{acc.accountType}
                </BANKACCTFROM>
                <BANKTRANLIST>
                <DTSTART>20231101120000
                <DTEND>20231130120000
                {txnList}
                </BANKTRANLIST>
                <LEDGERBAL>
                <BALAMT>1000.00
                <DTASOF>20231130120000
                </LEDGERBAL>
                </STMTRS>
                </STMTTRNRS>
                """;
        }));

        return $"""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:102
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRSV1>
            <SONRS>
            <STATUS><CODE>0<SEVERITY>INFO</STATUS>
            <DTSERVER>20231201120000
            <LANGUAGE>ENG
            <FI><ORG>{bankName}<FID>12345</FI>
            </SONRS>
            </SIGNONMSGSRSV1>
            <BANKMSGSRSV1>
            {accountStatements}
            </BANKMSGSRSV1>
            </OFX>
            """;
    }

    /// <summary>
    /// Builds a single OFX transaction element.
    /// </summary>
    /// <param name="id">Transaction ID number (used for FITID)</param>
    /// <param name="date">Transaction date</param>
    /// <param name="amount">Transaction amount (negative for debit, positive for credit)</param>
    /// <param name="name">Payee name (optional)</param>
    /// <param name="memo">Transaction memo (optional)</param>
    /// <returns>OFX STMTTRN element as string</returns>
    private static string BuildTransaction(int id, DateOnly date, decimal amount, string? name, string? memo)
    {
        var nameTag = !string.IsNullOrEmpty(name) ? $"<NAME>{name}" : "";
        var memoTag = !string.IsNullOrEmpty(memo) ? $"<MEMO>{memo}" : "";

        return $"""
            <STMTTRN>
            <TRNTYPE>{(amount < 0 ? "DEBIT" : "CREDIT")}
            <DTPOSTED>{date:yyyyMMdd}120000
            <TRNAMT>{amount}
            <FITID>TXN{id:D3}
            {nameTag}
            {memoTag}
            </STMTTRN>
            """;
    }
}
