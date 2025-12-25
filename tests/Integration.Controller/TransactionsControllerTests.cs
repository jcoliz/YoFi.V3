using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TransactionsControllerTests : AuthenticatedTestBase
{
    #region Unauthenticated Tests

    [Test]
    public async Task GetTransactions_InvalidTenantIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid tenant ID format (not a valid GUID)
        using var unauthClient = _factory.CreateClient();

        // When: API Client requests transactions with invalid tenant ID format
        var response = await unauthClient.GetAsync("/api/tenant/1/transactions");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GetTransactionById_InvalidTenantIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid tenant ID format (not a valid GUID)
        var validTransactionKey = Guid.NewGuid();
        using var unauthClient = _factory.CreateClient();

        // When: API Client requests a transaction with invalid tenant ID format
        var response = await unauthClient.GetAsync($"/api/tenant/invalid-guid/transactions/{validTransactionKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GetTransactionById_InvalidTransactionIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid transaction ID format (not a valid GUID)
        var validTenantKey = Guid.NewGuid();
        using var unauthClient = _factory.CreateClient();

        // When: API Client requests a transaction with invalid transaction ID format
        var response = await unauthClient.GetAsync($"/api/tenant/{validTenantKey}/transactions/not-a-guid");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    #endregion

    #region GET Tests with Authorization

    [Test]
    public async Task GetTransactions_AsViewer_ReturnsOK()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: User requests transactions
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain a list (may have transactions from other tests)
        var transactions = await response.Content.ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);
    }

    [Test]
    public async Task GetTransactions_AsEditor_ReturnsOK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // When: User requests transactions
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain a list (may have transactions from other tests)
        var transactions = await response.Content.ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);
    }

    [Test]
    public async Task GetTransactionById_AsViewer_Returns404ForNonExistent()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // And: Transaction does not exist
        var nonExistentKey = Guid.NewGuid();

        // When: User requests transaction by ID
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region POST Tests with Authorization

    [Test]
    public async Task CreateTransaction_AsEditor_ReturnsCreated()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Valid transaction data
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 123.45m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain the created transaction
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Payee, Is.EqualTo("Test Payee"));
        Assert.That(created.Amount, Is.EqualTo(123.45m));
        Assert.That(created.Key, Is.Not.EqualTo(Guid.Empty));

        // And: Location header should point to the created resource
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain(created.Key.ToString()));
    }

    [Test]
    public async Task CreateTransaction_AsViewer_ReturnsForbidden()
    {
        // Given: User has Viewer role for tenant (read-only)
        SwitchToViewer();

        // And: Valid transaction data
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 123.45m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: User attempts to create a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CreateTransaction_AsOwner_ReturnsCreated()
    {
        // Given: User has Owner role for tenant
        SwitchToOwner();

        // And: Valid transaction data
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 99.99m,
            Payee: "Owner Transaction",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain the created transaction
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Payee, Is.EqualTo("Owner Transaction"));
    }

    #endregion

    #region PUT Tests with Authorization

    [Test]
    public async Task UpdateTransaction_AsEditor_ReturnsOK()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: A transaction exists
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Updated transaction data
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Amount: 200m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain the updated transaction
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Payee, Is.EqualTo("Updated Payee"));
        Assert.That(updated.Amount, Is.EqualTo(200m));
        Assert.That(updated.Key, Is.EqualTo(created.Key));
    }

    [Test]
    public async Task UpdateTransaction_AsViewer_ReturnsForbidden()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: User switches to Viewer role
        SwitchToViewer();

        // And: Updated transaction data
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 200m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: Viewer attempts to update the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateTransaction_NonExistent_Returns404()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction does not exist
        var nonExistentKey = Guid.NewGuid();

        // And: Updated transaction data
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 200m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null
        );

        // When: User attempts to update non-existent transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentKey}", updateDto);

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region DELETE Tests with Authorization

    [Test]
    public async Task DeleteTransaction_AsEditor_ReturnsNoContent()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: A transaction exists
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "To Be Deleted",
            Memo: null,
            Source: null,
            ExternalId: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User deletes the transaction
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // And: Transaction should no longer exist
        var getResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{created.Key}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteTransaction_AsViewer_ReturnsForbidden()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Protected Transaction",
            Memo: null,
            Source: null,
            ExternalId: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: User switches to Viewer role
        SwitchToViewer();

        // When: Viewer attempts to delete the transaction
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // And: Transaction should still exist
        var getResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{created.Key}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DeleteTransaction_NonExistent_Returns404()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction does not exist
        var nonExistentKey = Guid.NewGuid();

        // When: User attempts to delete non-existent transaction
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion
}
