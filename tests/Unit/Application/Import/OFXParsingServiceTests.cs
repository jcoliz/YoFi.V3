namespace YoFi.V3.Tests.Unit.Application.Import;

using NUnit.Framework;
using YoFi.V3.Application.Import.Services;
using YoFi.V3.Application.Import.Dto;

/// <summary>
/// Unit tests for OFX parsing service.
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
        var service = new OfxParsingService();

        // When: Parsing the null stream
        var result = await service.ParseAsync(nullStream!, "test.ofx");

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
        var service = new OfxParsingService();

        // When: Parsing the empty stream
        var result = await service.ParseAsync(emptyStream, "empty.ofx");

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
        var service = new OfxParsingService();

        // When: Parsing the invalid OFX
        var result = await service.ParseAsync(invalidStream, "invalid.ofx");

        // Then: Should return result with error information
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Errors, Is.Not.Empty);
        Assert.That(result.Errors.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task ParseAsync_ValidOfxWithZeroTransactions_ReturnsEmptyTransactionList()
    {
        // Given: A valid OFX document with no transactions
        var ofxContent = """
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
            <FI><ORG>Test Bank<FID>12345</FI>
            </SONRS>
            </SIGNONMSGSRSV1>
            <BANKMSGSRSV1>
            <STMTTRNRS>
            <STMTRS>
            <CURDEF>USD
            <BANKACCTFROM>
            <BANKID>123456789
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            </BANKTRANLIST>
            </STMTRS>
            </STMTTRNRS>
            </BANKMSGSRSV1>
            </OFX>
            """;
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "zero-transactions.ofx");

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
        var ofxContent = $"""
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Test Payee
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "single-transaction.ofx");

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
        var ofxContent = """
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Test Payee
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "single-transaction.ofx");

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
        var ofxContent = $"""
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>{expectedPayee}
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "single-transaction.ofx");

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
        var ofxContent = $"""
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <MEMO>{expectedPayee}
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "memo-fallback.ofx");

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
        var ofxContent = $"""
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Test Payee
            <MEMO>{expectedMemo}
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with memo extracted
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Memo, Is.EqualTo(expectedMemo));
    }

    [Test]
    [Explicit("TDD: Test 9 - Implement source string construction from account info")]
    public async Task ParseAsync_SingleTransaction_BuildsSourceString()
    {
        // Given: An OFX document with bank name, account type, and account ID
        var expectedSource = "Test Bank - Checking (9876543210)";
        var ofxContent = """
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Test Payee
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "single-transaction.ofx");

        // Then: Should return transaction with source formatted as 'Bank - AccountType (ID)'
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.First().Source, Is.EqualTo(expectedSource));
    }

    [Test]
    [Explicit("TDD: Test 10 - Implement multiple transaction parsing")]
    public async Task ParseAsync_MultipleTransactions_ReturnsAll()
    {
        // Given: An OFX document with multiple transactions
        var ofxContent = """
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
            <ACCTID>9876543210
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Payee One
            </STMTTRN>
            <STMTTRN>
            <TRNTYPE>CREDIT
            <DTPOSTED>20231116120000
            <TRNAMT>100.00
            <FITID>TXN002
            <NAME>Payee Two
            </STMTTRN>
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231117120000
            <TRNAMT>-25.50
            <FITID>TXN003
            <NAME>Payee Three
            </STMTTRN>
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
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "multiple-transactions.ofx");

        // Then: Should return all transactions in result
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.Count, Is.EqualTo(3));
        Assert.That(result.Transactions.ElementAt(0).Payee, Is.EqualTo("Payee One"));
        Assert.That(result.Transactions.ElementAt(1).Payee, Is.EqualTo("Payee Two"));
        Assert.That(result.Transactions.ElementAt(2).Payee, Is.EqualTo("Payee Three"));
    }

    [Test]
    [Explicit("TDD: Test 11 - Implement multi-account statement handling")]
    public async Task ParseAsync_MultipleAccounts_HandlesEachSeparately()
    {
        // Given: An OFX document with multiple account statements
        var ofxContent = """
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
            <ACCTID>1111111111
            <ACCTTYPE>CHECKING
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>DEBIT
            <DTPOSTED>20231115120000
            <TRNAMT>-50.00
            <FITID>TXN001
            <NAME>Checking Transaction
            </STMTTRN>
            </BANKTRANLIST>
            <LEDGERBAL>
            <BALAMT>1000.00
            <DTASOF>20231130120000
            </LEDGERBAL>
            </STMTRS>
            </STMTTRNRS>
            <STMTTRNRS>
            <TRNUID>2
            <STATUS>
            <CODE>0
            <SEVERITY>INFO
            </STATUS>
            <STMTRS>
            <CURDEF>USD
            <BANKACCTFROM>
            <BANKID>123456789
            <ACCTID>2222222222
            <ACCTTYPE>SAVINGS
            </BANKACCTFROM>
            <BANKTRANLIST>
            <DTSTART>20231101120000
            <DTEND>20231130120000
            <STMTTRN>
            <TRNTYPE>CREDIT
            <DTPOSTED>20231116120000
            <TRNAMT>100.00
            <FITID>TXN002
            <NAME>Savings Transaction
            </STMTTRN>
            </BANKTRANLIST>
            <LEDGERBAL>
            <BALAMT>2000.00
            <DTASOF>20231130120000
            </LEDGERBAL>
            </STMTRS>
            </STMTTRNRS>
            </BANKMSGSRSV1>
            </OFX>
            """;
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(ofxContent));
        var service = new OfxParsingService();

        // When: Parsing the OFX document
        var result = await service.ParseAsync(stream, "multi-account.ofx");

        // Then: Should return transactions from all accounts with correct source for each
        Assert.That(result.Errors, Is.Empty, $"Expected no errors, but got: {string.Join(", ", result.Errors.Select(e => e.Message))}");
        Assert.That(result.Transactions, Is.Not.Empty);
        Assert.That(result.Transactions.Count, Is.EqualTo(2));

        var checkingTxn = result.Transactions.First(t => t.Payee == "Checking Transaction");
        Assert.That(checkingTxn.Source, Is.EqualTo("Test Bank - Checking (1111111111)"));

        var savingsTxn = result.Transactions.First(t => t.Payee == "Savings Transaction");
        Assert.That(savingsTxn.Source, Is.EqualTo("Test Bank - Savings (2222222222)"));
    }
}
