using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantDataAdminControllerTests : AuthenticatedTestBase
{
    protected override TenantRole DefaultRole => TenantRole.Owner;
    private string DataUrl => $"/api/tenant/{_testTenantKey}/admin/data";

    [SetUp]
    public void SetUp() => SwitchToOwner();

    [Test]
    public async Task DeleteAll_AsOwner_DeletesAllTenantDataAndPreservesWorkspaceMembershipAndOtherTenant()
    {
        var (tenantId, otherTenantId) = await SeedDataAsync();

        var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<TenantDataDeletionResultDto>();
        Assert.That(result!.Deleted, Is.EqualTo(new TenantDataDeletionCountsDto(1, 1, 1, 1)));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.Splits.CountAsync(), Is.Zero);
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == tenantId), Is.Zero);
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == otherTenantId), Is.EqualTo(1));
            Assert.That(await db.Tenants.CountAsync(item => item.Id == tenantId), Is.EqualTo(1));
            Assert.That(await db.UserTenantRoleAssignments.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
        }
    }

    [TestCase("test tenant")]
    [TestCase("Test Tenant ")]
    public async Task DeleteAll_WithNonExactConfirmation_Returns400AndDoesNotDelete(string confirmationName)
    {
        var (tenantId, _) = await SeedDataAsync();

        var response = await _client.SendAsync(DeleteRequest(confirmationName));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Status, Is.EqualTo(400));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
    }

    [Test]
    public async Task DeleteAll_AsEditor_ReturnsForbidden()
    {
        SwitchToEditor();

        var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteAll_WhenDatabaseDeleteFails_RollsBackAllData()
    {
        var (tenantId, _) = await SeedDataAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_tenant_data_transaction_delete
            BEFORE DELETE ON "YoFi.V3.Transactions"
            BEGIN
                SELECT RAISE(ABORT, 'Injected deletion failure');
            END;
            """);

        try
        {
            var response = await _client.SendAsync(DeleteRequest("Test Tenant"));

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(await db.Transactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
            Assert.That(await db.Splits.CountAsync(), Is.EqualTo(1));
            Assert.That(await db.PayeeMatchingRules.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
            Assert.That(await db.ImportReviewTransactions.CountAsync(item => item.TenantId == tenantId), Is.EqualTo(1));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_tenant_data_transaction_delete;");
        }
    }

    private async Task<(long TenantId, long OtherTenantId)> SeedDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await db.Tenants.SingleAsync(item => item.Key == _testTenantKey);

        var transactionIds = db.Transactions.Where(item => item.TenantId == tenant.Id).Select(item => item.Id);
        await db.Splits.Where(item => transactionIds.Contains(item.TransactionId)).ExecuteDeleteAsync();
        await db.Transactions.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();
        await db.PayeeMatchingRules.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();
        await db.ImportReviewTransactions.Where(item => item.TenantId == tenant.Id).ExecuteDeleteAsync();

        var otherTenant = new Tenant { Name = "Other Tenant", Description = "Untouched tenant" };
        db.Tenants.Add(otherTenant);
        await db.SaveChangesAsync();

        db.Transactions.AddRange(
            new Transaction
            {
                TenantId = tenant.Id,
                Payee = "Target transaction",
                Splits = [new Split { Category = "Target category", Amount = -10 }]
            },
            new Transaction { TenantId = otherTenant.Id, Payee = "Other transaction" });
        db.PayeeMatchingRules.AddRange(
            new PayeeMatchingRule { TenantId = tenant.Id, PayeePattern = "Target", Category = "Target" },
            new PayeeMatchingRule { TenantId = otherTenant.Id, PayeePattern = "Other", Category = "Other" });
        db.ImportReviewTransactions.AddRange(
            new ImportReviewTransaction { TenantId = tenant.Id, Payee = "Target pending import" },
            new ImportReviewTransaction { TenantId = otherTenant.Id, Payee = "Other pending import" });
        if (!await db.UserTenantRoleAssignments.AnyAsync(item =>
                item.TenantId == tenant.Id && item.UserId == "test-user-id"))
        {
            db.UserTenantRoleAssignments.Add(new UserTenantRoleAssignment
            {
                UserId = "test-user-id",
                TenantId = tenant.Id,
                Role = TenantRole.Owner
            });
        }
        await db.SaveChangesAsync();
        return (tenant.Id, otherTenant.Id);
    }

    private HttpRequestMessage DeleteRequest(string confirmationName)
        => new(HttpMethod.Delete, DataUrl)
        {
            Content = JsonContent.Create(new TenantDataDeletionRequest(confirmationName))
        };
}
