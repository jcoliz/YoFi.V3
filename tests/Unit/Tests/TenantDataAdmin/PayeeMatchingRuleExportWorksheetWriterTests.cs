using System.Collections;
using System.Reflection;
using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class PayeeMatchingRuleExportWorksheetWriterTests
{
    [Test]
    public void PropertiesDescribeNativeWorksheet()
    {
        var writer = new PayeeMatchingRuleExportWorksheetWriter(Mock.Of<IDataProvider>());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(writer.DataType, Is.EqualTo("PayeeMatchingRules"));
            Assert.That(writer.WorksheetName, Is.EqualTo("PayeeMatchingRules"));
            Assert.That(writer.FileNamePart, Is.EqualTo("payee-matching-rules"));
            Assert.That(writer.SupportsDateFilter, Is.False);
        }
    }

    [Test]
    public async Task WriteAsync_CancelledBeforeQueryThrows()
    {
        var data = new Mock<IDataProvider>();
        var writer = new PayeeMatchingRuleExportWorksheetWriter(data.Object);
        using var workbook = new XLWorkbook();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => writer.WriteAsync(
            workbook, 42, null, null, source.Token));

        data.Verify(item => item.Get<PayeeMatchingRule>(), Times.Never);
    }

    [Test]
    public async Task WriteAsync_EmptyRulesWritesHeadersAndZeroCount()
    {
        var data = new Mock<IDataProvider>();
        data.Setup(item => item.Get<PayeeMatchingRule>())
            .Returns(Array.Empty<PayeeMatchingRule>().AsQueryable());
        SetProjectionResult(data, []);
        var writer = new PayeeMatchingRuleExportWorksheetWriter(data.Object);
        using var workbook = new XLWorkbook();

        var counts = await writer.WriteAsync(workbook, 42, null, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(counts, Is.EqualTo(new[]
            {
                new TenantDataExportCountDto("PayeeMatchingRules", 0)
            }));
            Assert.That(workbook.Worksheet("PayeeMatchingRules").Row(1).CellsUsed()
                .Select(cell => cell.GetString()), Is.EqualTo(new[]
                {
                    "Key", "PayeePattern", "PayeeIsRegex", "Category",
                    "CreatedAt", "ModifiedAt", "LastUsedAt", "MatchCount"
                }));
        }
    }

    [Test]
    public async Task WriteAsync_WithRulesWritesAllColumnsAndCount()
    {
        var key = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var rules = new[]
        {
            new PayeeMatchingRule
            {
                TenantId = 42,
                Key = key,
                PayeePattern = "Coffee",
                PayeeIsRegex = true,
                Category = "Food:Dining",
                CreatedAt = new DateTimeOffset(2025, 1, 1, 1, 2, 3, TimeSpan.Zero),
                ModifiedAt = new DateTimeOffset(2025, 1, 2, 4, 5, 6, TimeSpan.Zero),
                LastUsedAt = new DateTimeOffset(2025, 1, 3, 7, 8, 9, TimeSpan.Zero),
                MatchCount = 4
            }
        };
        var data = new Mock<IDataProvider>();
        data.Setup(item => item.Get<PayeeMatchingRule>()).Returns(rules.AsQueryable());
        SetProjectionResult(data, rules);
        var writer = new PayeeMatchingRuleExportWorksheetWriter(data.Object);
        using var workbook = new XLWorkbook();

        var counts = await writer.WriteAsync(workbook, 42, null, null, CancellationToken.None);

        var worksheet = workbook.Worksheet("PayeeMatchingRules");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(counts.Single().RecordCount, Is.EqualTo(1));
            Assert.That(worksheet.Cell(2, 1).GetString(), Is.EqualTo(key.ToString("D")));
            Assert.That(worksheet.Cell(2, 2).GetString(), Is.EqualTo("Coffee"));
            Assert.That(worksheet.Cell(2, 3).GetString(), Is.EqualTo("TRUE"));
            Assert.That(worksheet.Cell(2, 4).GetString(), Is.EqualTo("Food:Dining"));
            Assert.That(worksheet.Cell(2, 5).GetString(), Is.EqualTo("2025-01-01T01:02:03.0000000+00:00"));
            Assert.That(worksheet.Cell(2, 6).GetString(), Is.EqualTo("2025-01-02T04:05:06.0000000+00:00"));
            Assert.That(worksheet.Cell(2, 7).GetString(), Is.EqualTo("2025-01-03T07:08:09.0000000+00:00"));
            Assert.That(worksheet.Cell(2, 8).GetValue<int>(), Is.EqualTo(4));
        }
    }

    [Test]
    public async Task WriteAsync_RuleWithoutLastUseWritesBlankAndFiltersTenant()
    {
        var rules = new[]
        {
            new PayeeMatchingRule
            {
                TenantId = 42,
                Key = Guid.NewGuid(),
                PayeePattern = "Other",
                PayeeIsRegex = false,
                Category = "Other",
                CreatedAt = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ModifiedAt = new(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
                LastUsedAt = null,
                MatchCount = 0
            },
            new PayeeMatchingRule
            {
                TenantId = 99,
                Key = Guid.NewGuid(),
                PayeePattern = "Excluded",
                PayeeIsRegex = false,
                Category = "Excluded",
                CreatedAt = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                ModifiedAt = new(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
                MatchCount = 0
            }
        };
        var data = new Mock<IDataProvider>();
        data.Setup(item => item.Get<PayeeMatchingRule>()).Returns(rules.AsQueryable());
        SetProjectionResult(data, rules);
        var writer = new PayeeMatchingRuleExportWorksheetWriter(data.Object);
        using var workbook = new XLWorkbook();

        await writer.WriteAsync(workbook, 42, null, null, CancellationToken.None);

        var worksheet = workbook.Worksheet("PayeeMatchingRules");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(worksheet.LastRowUsed()!.RowNumber(), Is.EqualTo(2));
            Assert.That(worksheet.Cell(2, 2).GetString(), Is.EqualTo("Other"));
            Assert.That(worksheet.Cell(2, 7).GetString(), Is.Empty);
        }
    }

    private static void SetProjectionResult(Mock<IDataProvider> data, IEnumerable<PayeeMatchingRule> rules)
    {
        var rowType = typeof(PayeeMatchingRuleExportWorksheetWriter).GetNestedType("RuleExportRow", BindingFlags.NonPublic)!;
        var listType = typeof(List<>).MakeGenericType(rowType);
        var rows = (IList)Activator.CreateInstance(listType)!;
        foreach (var rule in rules.Where(rule => rule.TenantId == 42)
                     .OrderBy(rule => rule.PayeePattern)
                     .ThenBy(rule => rule.Key))
        {
            rows.Add(Activator.CreateInstance(rowType,
                rule.Key,
                rule.PayeePattern,
                rule.PayeeIsRegex,
                rule.Category,
                rule.CreatedAt,
                rule.ModifiedAt,
                rule.LastUsedAt,
                rule.MatchCount)!);
        }

        var fromResult = typeof(Task).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == "FromResult" && method.IsGenericMethodDefinition);
        var task = fromResult.MakeGenericMethod(listType).Invoke(null, [rows])!;
        var setReturnsDefault = typeof(Mock<IDataProvider>).GetMethod("SetReturnsDefault")!;
        setReturnsDefault.MakeGenericMethod(task.GetType()).Invoke(data, [task]);
    }
}
