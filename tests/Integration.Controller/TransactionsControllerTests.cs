using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Controller Integration tests for TransactionsController.
/// </summary>
/// <remarks>
/// Tests HTTP boundary concerns ONLY: authentication, authorization, status codes,
/// request/response serialization, and API contracts. Business logic is tested in
/// Application Integration tests.
/// </remarks>
[TestFixture]
public class TransactionsControllerTests : AuthenticatedTestBase
{
    #region HTTP Routing and Invalid Input Tests

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

    #region Authorization Tests (Role-Based Access)

    [Test]
    public async Task GetTransactions_AsViewer_ReturnsOK()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: User requests transactions
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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

    [Test]
    public async Task CreateTransaction_AsEditor_ReturnsCreatedWithLocation()
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
            ExternalId: null,
            Category: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Location header should point to the created resource
        Assert.That(response.Headers.Location, Is.Not.Null);

        // And: Response body should contain the created transaction
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain(created!.Key.ToString()));
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
            ExternalId: null,
            Category: null
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
            ExternalId: null,
            Category: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

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
            ExternalId: null,
            Category: null
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
            ExternalId: null,
            Category: null
        );

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
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
            ExternalId: null,
            Category: null
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
            ExternalId: null,
            Category: null
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
            ExternalId: null,
            Category: null
        );

        // When: User attempts to update non-existent transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentKey}", updateDto);

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

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
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User deletes the transaction
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // And: Transaction should no longer exist (verify with GET)
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
            ExternalId: null,
            Category: null
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

    #region Request Validation Tests (HTTP 400 Bad Request)

    [Test]
    public async Task CreateTransaction_MemoTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with Memo exceeding 1000 characters
        var longMemo = new string('M', 1001);
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: longMemo,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task CreateTransaction_SourceTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with Source exceeding 200 characters
        var longSource = new string('S', 201);
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: longSource,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_ExternalIdTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with ExternalId exceeding 100 characters
        var longExternalId = new string('E', 101);
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: longExternalId,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_PayeeTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with Payee exceeding 200 characters
        var longPayee = new string('P', 201);
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: longPayee,
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_PayeeEmpty_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with empty Payee (required field)
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_PayeeWhitespace_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with whitespace-only Payee (required field)
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "   ",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_DateTooFarInPast_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with date more than 50 years in the past
        var tooOldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51));
        var newTransaction = new TransactionEditDto(
            Date: tooOldDate,
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_DateTooFarInFuture_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with date more than 5 years in the future
        var tooFutureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(6));
        var newTransaction = new TransactionEditDto(
            Date: tooFutureDate,
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CreateTransaction_ZeroAmount_Returns400BadRequest()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction with zero amount (business rule violation)
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 0m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task QuickEditTransaction_PayeeEmpty_Returns400BadRequest()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with empty Payee (required field)
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "",
            Memo: null,
            Category: null
        );

        // When: User attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task QuickEditTransaction_PayeeWhitespace_Returns400BadRequest()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with whitespace-only Payee (required field)
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "   ",
            Memo: null,
            Category: null
        );

        // When: User attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task QuickEditTransaction_PayeeTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with Payee exceeding 200 characters
        var longPayee = new string('P', 201);
        var quickEditDto = new TransactionQuickEditDto(
            Payee: longPayee,
            Memo: null,
            Category: null
        );

        // When: User attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task QuickEditTransaction_MemoTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with Memo exceeding 1000 characters
        var longMemo = new string('M', 1001);
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: longMemo,
            Category: null
        );

        // When: User attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task QuickEditTransaction_CategoryTooLong_Returns400BadRequest()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with Category exceeding 200 characters
        var longCategory = new string('C', 201);
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: longCategory
        );

        // When: User attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region Tenant Isolation Tests (HTTP 403 Forbidden)

    [Test]
    public async Task GetTransactionById_FromOtherTenant_Returns403Forbidden()
    {
        // Given: User has Editor role and creates a transaction in their tenant
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Tenant Isolation Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // When: User attempts to access the transaction via other tenant's endpoint
        var response = await _client.GetAsync($"/api/tenant/{otherTenantKey}/transactions/{created!.Key}");

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateTransaction_FromOtherTenant_Returns403Forbidden()
    {
        // Given: User has Editor role and creates a transaction in their tenant
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Update Isolation Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // And: Updated transaction data
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 200m,
            Payee: "Should Not Update",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User attempts to update the transaction via other tenant's endpoint
        var response = await _client.PutAsJsonAsync($"/api/tenant/{otherTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region Response Serialization Tests (DTO Structure)

    [Test]
    public async Task GetTransactionsList_ReturnsTransactionResultDto()
    {
        // Given: User has Viewer role and transactions exist
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "List Test",
            Memo: "Should not appear in list",
            Source: "Should not appear in list",
            ExternalId: "SHOULD-NOT-APPEAR",
            Category: null
        );
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);

        SwitchToViewer();

        // When: User requests transaction list
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should be PaginatedResultDto with TransactionResultDto items
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<TransactionResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items.Count, Is.GreaterThan(0));

        // And: TransactionResultDto does not contain Memo, Source, ExternalId
        var firstTransaction = result.Items.First();
        Assert.That(firstTransaction.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(firstTransaction.Payee, Is.Not.Null);
    }

    [Test]
    public async Task GetTransactionById_WithNewFields_ReturnsTransactionDetailDto()
    {
        // Given: User has Editor role and creates a transaction with new fields
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 150m,
            Payee: "Get By ID Test",
            Memo: "Test memo content",
            Source: "Test Source",
            ExternalId: "TEST-EXT-001",
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User retrieves the transaction by ID
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should be TransactionDetailDto with all fields
        var retrieved = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Key, Is.EqualTo(created.Key));
        Assert.That(retrieved.Payee, Is.EqualTo("Get By ID Test"));
        Assert.That(retrieved.Memo, Is.EqualTo("Test memo content"));
        Assert.That(retrieved.Source, Is.EqualTo("Test Source"));
        Assert.That(retrieved.ExternalId, Is.EqualTo("TEST-EXT-001"));
    }

    #endregion
}
