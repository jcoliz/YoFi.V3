using System.Text;
using YoFi.V3.Tests.Functional.Generated;

namespace YoFi.V3.Tests.Functional.Helpers;

/// <summary>
/// Helper class for generating OFX (Open Financial Exchange) files for testing.
/// </summary>
/// <remarks>
/// Generates valid OFX 2.0 XML format files with banking transactions for use in functional tests.
/// All generated files use a consistent structure with configurable transaction data.
/// </remarks>
public static class OfxFileGenerator
{
    /// <summary>
    /// Generates an OFX file with the specified number of new transactions.
    /// </summary>
    /// <param name="transactionCount">Number of transactions to include in the OFX file.</param>
    /// <returns>Path to the generated OFX file in the system temporary directory.</returns>
    /// <remarks>
    /// Creates a temporary OFX file with unique transaction IDs (FITIDs) based on date and index.
    /// Transactions are spaced one day apart starting from (now - transactionCount days).
    /// Each transaction has a varying amount: -(10 + i * 5.5) and payee name "Uploaded {i}".
    /// The caller is responsible for deleting the temporary file after use.
    /// </remarks>
    public static string GenerateOfxFile(int transactionCount)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var filename = Path.Combine(Path.GetTempPath(), $"test-import-{timestamp}.ofx");

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<?OFX OFXHEADER=\"200\" VERSION=\"202\" SECURITY=\"NONE\" OLDFILEUID=\"NONE\" NEWFILEUID=\"NONE\"?>");
        sb.AppendLine("<OFX>");
        sb.AppendLine("  <SIGNONMSGSRSV1>");
        sb.AppendLine("    <SONRS>");
        sb.AppendLine("      <STATUS>");
        sb.AppendLine("        <CODE>0</CODE>");
        sb.AppendLine("        <SEVERITY>INFO</SEVERITY>");
        sb.AppendLine("      </STATUS>");
        sb.AppendLine($"      <DTSERVER>{DateTime.UtcNow:yyyyMMddHHmmss}.000</DTSERVER>");
        sb.AppendLine("      <LANGUAGE>ENG</LANGUAGE>");
        sb.AppendLine("      <FI>");
        sb.AppendLine("        <ORG>Test Bank</ORG>");
        sb.AppendLine("        <FID>9999</FID>");
        sb.AppendLine("      </FI>");
        sb.AppendLine("    </SONRS>");
        sb.AppendLine("  </SIGNONMSGSRSV1>");
        sb.AppendLine("  <BANKMSGSRSV1>");
        sb.AppendLine("    <STMTTRNRS>");
        sb.AppendLine("      <TRNUID>0</TRNUID>");
        sb.AppendLine("      <STATUS>");
        sb.AppendLine("        <CODE>0</CODE>");
        sb.AppendLine("        <SEVERITY>INFO</SEVERITY>");
        sb.AppendLine("      </STATUS>");
        sb.AppendLine("      <STMTRS>");
        sb.AppendLine("        <CURDEF>USD</CURDEF>");
        sb.AppendLine("        <BANKACCTFROM>");
        sb.AppendLine("          <BANKID>111000025</BANKID>");
        sb.AppendLine("          <ACCTID>123456789</ACCTID>");
        sb.AppendLine("          <ACCTTYPE>CHECKING</ACCTTYPE>");
        sb.AppendLine("        </BANKACCTFROM>");
        sb.AppendLine("        <BANKTRANLIST>");
        sb.AppendLine($"          <DTSTART>{DateTime.UtcNow.AddDays(-30):yyyyMMdd}040000.000</DTSTART>");
        sb.AppendLine($"          <DTEND>{DateTime.UtcNow:yyyyMMdd}040000.000</DTEND>");

        // Generate transactions
        var baseDate = DateTime.UtcNow.AddDays(-transactionCount);

        for (int i = 0; i < transactionCount; i++)
        {
            var date = baseDate.AddDays(i);
            var amount = -(10 + (i * 5.5)); // Varying amounts
            var payee = $"Uploaded {i}";
            var fitId = $"TEST{date:yyyyMMdd}{i:D3}"; // Unique FITID

            sb.AppendLine($"          <!-- Transaction {i + 1} - NEW -->");
            sb.AppendLine("          <STMTTRN>");
            sb.AppendLine("            <TRNTYPE>DEBIT</TRNTYPE>");
            sb.AppendLine($"            <DTPOSTED>{date:yyyyMMdd}040000.000</DTPOSTED>");
            sb.AppendLine($"            <TRNAMT>{amount:F2}</TRNAMT>");
            sb.AppendLine($"            <FITID>{fitId}</FITID>");
            sb.AppendLine($"            <NAME>{payee}</NAME>");
            sb.AppendLine($"            <MEMO>Test transaction {i + 1}</MEMO>");
            sb.AppendLine("          </STMTTRN>");
        }

        sb.AppendLine("        </BANKTRANLIST>");
        sb.AppendLine("        <LEDGERBAL>");
        sb.AppendLine("          <BALAMT>1000.00</BALAMT>");
        sb.AppendLine($"          <DTASOF>{DateTime.UtcNow:yyyyMMdd}120000.000</DTASOF>");
        sb.AppendLine("        </LEDGERBAL>");
        sb.AppendLine("      </STMTRS>");
        sb.AppendLine("    </STMTTRNRS>");
        sb.AppendLine("  </BANKMSGSRSV1>");
        sb.AppendLine("</OFX>");

        File.WriteAllText(filename, sb.ToString());
        return filename;
    }

    /// <summary>
    /// Generates an OFX file from a collection of existing transactions.
    /// </summary>
    /// <param name="transactions">Collection of transactions to include in the OFX file.</param>
    /// <returns>Path to the generated OFX file in the system temporary directory.</returns>
    /// <remarks>
    /// Creates a temporary OFX file with transaction data from the provided TransactionResultDto collection.
    /// Uses the transaction's Key as the FITID to ensure transactions are recognized as duplicates.
    /// Transactions are ordered by date in the OFX file.
    /// The caller is responsible for deleting the temporary file after use.
    /// </remarks>
    public static string GenerateOfxFileFromTransactions(IReadOnlyCollection<TransactionResultDto> transactions)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var filename = Path.Combine(Path.GetTempPath(), $"test-import-{timestamp}.ofx");

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<?OFX OFXHEADER=\"200\" VERSION=\"202\" SECURITY=\"NONE\" OLDFILEUID=\"NONE\" NEWFILEUID=\"NONE\"?>");
        sb.AppendLine("<OFX>");
        sb.AppendLine("  <SIGNONMSGSRSV1>");
        sb.AppendLine("    <SONRS>");
        sb.AppendLine("      <STATUS>");
        sb.AppendLine("        <CODE>0</CODE>");
        sb.AppendLine("        <SEVERITY>INFO</SEVERITY>");
        sb.AppendLine("      </STATUS>");
        sb.AppendLine($"      <DTSERVER>{DateTime.UtcNow:yyyyMMddHHmmss}.000</DTSERVER>");
        sb.AppendLine("      <LANGUAGE>ENG</LANGUAGE>");
        sb.AppendLine("      <FI>");
        sb.AppendLine("        <ORG>Test Bank</ORG>");
        sb.AppendLine("        <FID>9999</FID>");
        sb.AppendLine("      </FI>");
        sb.AppendLine("    </SONRS>");
        sb.AppendLine("  </SIGNONMSGSRSV1>");
        sb.AppendLine("  <BANKMSGSRSV1>");
        sb.AppendLine("    <STMTTRNRS>");
        sb.AppendLine("      <TRNUID>0</TRNUID>");
        sb.AppendLine("      <STATUS>");
        sb.AppendLine("        <CODE>0</CODE>");
        sb.AppendLine("        <SEVERITY>INFO</SEVERITY>");
        sb.AppendLine("      </STATUS>");
        sb.AppendLine("      <STMTRS>");
        sb.AppendLine("        <CURDEF>USD</CURDEF>");
        sb.AppendLine("        <BANKACCTFROM>");
        sb.AppendLine("          <BANKID>111000025</BANKID>");
        sb.AppendLine("          <ACCTID>123456789</ACCTID>");
        sb.AppendLine("          <ACCTTYPE>CHECKING</ACCTTYPE>");
        sb.AppendLine("        </BANKACCTFROM>");
        sb.AppendLine("        <BANKTRANLIST>");

        // Calculate date range from transactions
        var orderedTransactions = transactions.OrderBy(t => t.Date).ToList();
        var startDate = orderedTransactions.First().Date;
        var endDate = orderedTransactions.Last().Date;

        sb.AppendLine($"          <DTSTART>{startDate:yyyyMMdd}040000.000</DTSTART>");
        sb.AppendLine($"          <DTEND>{endDate:yyyyMMdd}040000.000</DTEND>");

        // Generate transaction entries
        foreach (var transaction in orderedTransactions)
        {
            var trnType = transaction.Amount < 0 ? "DEBIT" : "CREDIT";
            var fitId = transaction.Key.ToString(); // Use transaction Key as FITID for duplicate detection

            sb.AppendLine($"          <!-- Transaction: {transaction.Payee} -->");
            sb.AppendLine("          <STMTTRN>");
            sb.AppendLine($"            <TRNTYPE>{trnType}</TRNTYPE>");
            sb.AppendLine($"            <DTPOSTED>{transaction.Date:yyyyMMdd}040000.000</DTPOSTED>");
            sb.AppendLine($"            <TRNAMT>{transaction.Amount:F2}</TRNAMT>");
            sb.AppendLine($"            <FITID>{fitId}</FITID>");
            sb.AppendLine($"            <NAME>{transaction.Payee}</NAME>");

            if (!string.IsNullOrEmpty(transaction.Memo))
            {
                sb.AppendLine($"            <MEMO>{transaction.Memo}</MEMO>");
            }

            sb.AppendLine("          </STMTTRN>");
        }

        sb.AppendLine("        </BANKTRANLIST>");
        sb.AppendLine("        <LEDGERBAL>");
        sb.AppendLine("          <BALAMT>1000.00</BALAMT>");
        sb.AppendLine($"          <DTASOF>{DateTime.UtcNow:yyyyMMdd}120000.000</DTASOF>");
        sb.AppendLine("        </LEDGERBAL>");
        sb.AppendLine("      </STMTRS>");
        sb.AppendLine("    </STMTTRNRS>");
        sb.AppendLine("  </BANKMSGSRSV1>");
        sb.AppendLine("</OFX>");

        File.WriteAllText(filename, sb.ToString());
        return filename;
    }
}
