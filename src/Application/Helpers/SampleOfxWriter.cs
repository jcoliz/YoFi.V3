using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Helpers;

/// <summary>Writes synthetic checking statements with a zero opening balance for each requested range.</summary>
/// <remarks>
/// Writes OFX 2.02 XML because the installed reader library has no public writing API.
/// Uses synthetic checking account metadata shared with direct loading, complete escaped
/// merchant names, stable FITIDs, bank-style amount signs, and no categories.
/// NAME is limited to 32 Unicode scalar values; longer merchant text is preserved in MEMO
/// for <see cref="OfxParsingHelper"/> to reconstruct. Each statement assumes a zero
/// synthetic opening balance; closing balance is the sum of the requested range,
/// not a continuous or real bank balance.
/// </remarks>
public static class SampleOfxWriter
{
    /// <summary>Serializes supplied sample transactions into a synthetic checking statement.</summary>
    /// <param name="transactions">Previously validated, in-range records with stable non-null ExternalIds.</param>
    /// <param name="start">Inclusive statement start date, already validated by the caller.</param>
    /// <param name="end">Inclusive statement end date, already validated by the caller.</param>
    /// <returns>A readable, seekable, BOM-free UTF-8 stream positioned at zero. The consumer owns disposal.</returns>
    /// <remarks>
    /// Does not filter records or validate the date range. Empty collections produce an
    /// empty statement with zero closing balance. Categories and transaction memos are
    /// not serialized; OFX MEMO is reserved for preserving merchant names longer than NAME.
    /// </remarks>
    /// <exception cref="ArgumentException">A transaction has a null ExternalId.</exception>
    public static Stream Write(IReadOnlyCollection<TransactionEditDto> transactions, DateOnly start, DateOnly end)
    {
        static XElement Element(string name, object value) => new(name, value);
        static XElement Status() => new("STATUS", Element("CODE", 0), Element("SEVERITY", "INFO"));
        static string Date(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "120000";
        static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        static XElement Transaction(TransactionEditDto transaction)
        {
            var name = string.Concat(transaction.Payee.EnumerateRunes().Take(32).Select(r => r.ToString()));
            return new XElement("STMTTRN",
                Element("TRNTYPE", transaction.Amount < 0 ? "DEBIT" : "CREDIT"),
                Element("DTPOSTED", Date(transaction.Date)),
                Element("TRNAMT", Amount(transaction.Amount)),
                Element("FITID", transaction.ExternalId ?? throw new ArgumentException("Sample transactions require an ExternalId.", nameof(transactions))),
                Element("NAME", name),
                name != transaction.Payee ? Element("MEMO", transaction.Payee) : null);
        }

        var document = new XDocument(
            new XProcessingInstruction("OFX", "OFXHEADER=\"200\" VERSION=\"202\" SECURITY=\"NONE\" OLDFILEUID=\"NONE\" NEWFILEUID=\"NONE\""),
            new XElement("OFX",
                new XElement("SIGNONMSGSRSV1",
                    new XElement("SONRS", Status(), Element("DTSERVER", Date(end)), Element("LANGUAGE", "ENG"),
                        new XElement("FI", Element("ORG", "YoFi Sample Bank"), Element("FID", "000000000")))),
                new XElement("BANKMSGSRSV1",
                    new XElement("STMTTRNRS", Element("TRNUID", 0), Status(),
                        new XElement("STMTRS",
                            Element("CURDEF", "USD"),
                            new XElement("BANKACCTFROM", Element("BANKID", "000000000"), Element("ACCTID", "SAMPLE0001"), Element("ACCTTYPE", "CHECKING")),
                            new XElement("BANKTRANLIST", Element("DTSTART", Date(start)), Element("DTEND", Date(end)),
                                transactions.Select(Transaction)),
                            new XElement("LEDGERBAL", Element("BALAMT", Amount(transactions.Sum(t => t.Amount))), Element("DTASOF", Date(end))))))));

        var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
        {
            document.Save(writer);
        }
        stream.Position = 0;
        return stream;
    }
}
