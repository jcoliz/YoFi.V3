using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Tenancy.Models;
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
            ExternalId: null,
            Category: null
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
            ExternalId: null,
            Category: null
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

    #region New Fields Tests (Memo, Source, ExternalId)

    [Test]
    public async Task CreateTransaction_WithAllNewFields_ReturnsCreatedWithAllFields()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with all new fields populated
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 250.75m,
            Payee: "Complete Transaction",
            Memo: "Transaction with all fields populated",
            Source: "Bank of America Checking",
            ExternalId: "EXT-12345-ABC",
            Category: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain the created transaction with all fields
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Payee, Is.EqualTo("Complete Transaction"));
        Assert.That(created.Amount, Is.EqualTo(250.75m));
        Assert.That(created.Memo, Is.EqualTo("Transaction with all fields populated"));
        Assert.That(created.Source, Is.EqualTo("Bank of America Checking"));
        Assert.That(created.ExternalId, Is.EqualTo("EXT-12345-ABC"));
        Assert.That(created.Key, Is.Not.EqualTo(Guid.Empty));
    }

    [Test]
    public async Task CreateTransaction_WithMinimalFields_ReturnsCreatedWithNullableFields()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with only required fields (nullable fields are null)
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 75.50m,
            Payee: "Minimal Transaction",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain the created transaction with null fields
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Payee, Is.EqualTo("Minimal Transaction"));
        Assert.That(created.Amount, Is.EqualTo(75.50m));
        Assert.That(created.Memo, Is.Null);
        Assert.That(created.Source, Is.Null);
        Assert.That(created.ExternalId, Is.Null);
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

        // And: Response should be TransactionResultDto collection (no new fields)
        var transactions = await response.Content.ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);
        Assert.That(transactions!.Count, Is.GreaterThan(0));

        // And: TransactionResultDto does not contain new fields (verify DTO structure)
        var firstTransaction = transactions.First();
        Assert.That(firstTransaction.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(firstTransaction.Payee, Is.Not.Null);
        // TransactionResultDto should not have Memo, Source, ExternalId properties
    }

    [Test]
    public async Task UpdateTransaction_WithNewFields_ReturnsUpdatedWithAllFields()
    {
        // Given: User has Editor role and a transaction exists
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original",
            Memo: "Original memo",
            Source: "Original source",
            ExternalId: "ORIG-001"
        ,            Category: null);
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Updated transaction data with all new fields
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Amount: 300m,
            Payee: "Updated Payee",
            Memo: "Updated memo content",
            Source: "Updated source",
            ExternalId: "UPD-002"
        ,            Category: null);

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain updated transaction with all new fields
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Key, Is.EqualTo(created.Key));
        Assert.That(updated.Payee, Is.EqualTo("Updated Payee"));
        Assert.That(updated.Amount, Is.EqualTo(300m));
        Assert.That(updated.Memo, Is.EqualTo("Updated memo content"));
        Assert.That(updated.Source, Is.EqualTo("Updated source"));
        Assert.That(updated.ExternalId, Is.EqualTo("UPD-002"));
    }

    [Test]
    public async Task UpdateTransaction_ClearingNewFields_ReturnsUpdatedWithNulls()
    {
        // Given: User has Editor role and a transaction with populated new fields exists
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "With Fields",
            Memo: "Has memo",
            Source: "Has source",
            ExternalId: "HAS-EXT-001"
        ,            Category: null);
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Updated transaction data with new fields cleared (null)
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Cleared Fields",
            Memo: null,
            Source: null,
            ExternalId: null
        ,            Category: null);

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should have null values for new fields
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Payee, Is.EqualTo("Cleared Fields"));
        Assert.That(updated.Memo, Is.Null);
        Assert.That(updated.Source, Is.Null);
        Assert.That(updated.ExternalId, Is.Null);
    }

    #endregion

    #region Validation Tests

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: longExternalId
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

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
            ExternalId: null
        ,            Category: null);

        // When: User attempts to create the transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    #endregion

    #region Tenant Isolation Tests

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
            ExternalId: null
        ,            Category: null);
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
            ExternalId: null
        ,            Category: null);
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
            ExternalId: null
        ,            Category: null);

        // When: User attempts to update the transaction via other tenant's endpoint
        var response = await _client.PutAsJsonAsync($"/api/tenant/{otherTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CreateTransaction_SameExternalIdAcrossTenants_Succeeds()
    {
        // Given: User has Editor role and creates a transaction with ExternalId in first tenant
        SwitchToEditor();
        var sharedExternalId = "SHARED-EXT-001";
        var transaction1 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Tenant 1 Transaction",
            Memo: null,
            Source: null,
            ExternalId: sharedExternalId
        ,            Category: null);
        var response1 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction1);

        // Then: First transaction should be created successfully
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created1 = await response1.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created1!.ExternalId, Is.EqualTo(sharedExternalId));

        // And: Create a second tenant with user having Editor access
        var secondTenantKey = await CreateTestTenantAsync("Second Tenant", "Second tenant for cross-tenant testing");

        // And: Create a client with access to both tenants
        var multiTenantClient = CreateMultiTenantClient(
            (_testTenantKey, TenantRole.Editor),
            (secondTenantKey, TenantRole.Editor)
        );

        // When: User creates a transaction with same ExternalId in second tenant
        var transaction2 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 200m,
            Payee: "Tenant 2 Transaction",
            Memo: null,
            Source: null,
            ExternalId: sharedExternalId
        ,            Category: null);
        var response2 = await multiTenantClient.PostAsJsonAsync($"/api/tenant/{secondTenantKey}/transactions", transaction2);

        // Then: Second transaction should also be created successfully
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created2 = await response2.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created2!.ExternalId, Is.EqualTo(sharedExternalId));

        // And: Transactions should have different keys (separate entities)
        Assert.That(created1.Key, Is.Not.EqualTo(created2.Key));

        multiTenantClient.Dispose();
    }

    [Test]
    public async Task CreateTransaction_SameExternalIdWithinTenant_Succeeds()
    {
        // Given: User has Editor role and creates a transaction with ExternalId
        SwitchToEditor();
        var duplicateExternalId = "DUPLICATE-EXT-001";
        var transaction1 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "First Transaction",
            Memo: null,
            Source: null,
            ExternalId: duplicateExternalId
        ,            Category: null);
        var response1 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction1);

        // Then: First transaction should be created successfully
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created1 = await response1.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created1!.ExternalId, Is.EqualTo(duplicateExternalId));

        // When: User creates another transaction with same ExternalId in same tenant
        var transaction2 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Amount: 200m,
            Payee: "Second Transaction",
            Memo: null,
            Source: null,
            ExternalId: duplicateExternalId
        ,            Category: null);
        var response2 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction2);

        // Then: Second transaction should also be created successfully (API allows duplicates)
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created2 = await response2.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created2!.ExternalId, Is.EqualTo(duplicateExternalId));

        // And: Transactions should have different keys (separate entities)
        Assert.That(created1.Key, Is.Not.EqualTo(created2.Key));
    }

    #endregion

    #region Category Field Tests

    [Test]
    public async Task CreateTransaction_WithCategory_AutoCreatesSplitWithSanitizedCategory()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with category needing sanitization
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 50.00m,
            Payee: "Test Store",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "  food : groceries  " // Needs sanitization
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain transaction with sanitized category
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Category, Is.EqualTo("Food:Groceries"));
        Assert.That(created.Payee, Is.EqualTo("Test Store"));
        Assert.That(created.Amount, Is.EqualTo(50.00m));
    }

    [Test]
    public async Task CreateTransaction_WithoutCategory_AutoCreatesSplitWithEmptyCategory()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data without category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 75.00m,
            Payee: "Store Without Category",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Response should contain transaction with empty category
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task CreateTransaction_CategoryWithMultipleSpaces_SanitizesToSingleSpaces()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with category containing multiple spaces
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Home    and   Garden"
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Category should have single spaces between words
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo("Home And Garden"));
    }

    [Test]
    public async Task CreateTransaction_CategoryLowercase_CapitalizesWords()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with lowercase category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "homeandgarden"
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Category should have first letter capitalized
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo("Homeandgarden"));
    }

    [Test]
    public async Task CreateTransaction_CategoryWithEmptyTerm_RemovesEmptyTerm()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with category containing empty term after colon
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Home: "
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Empty term should be removed
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo("Home"));
    }

    [Test]
    public async Task CreateTransaction_CategoryWhitespaceOnly_ReturnsEmptyString()
    {
        // Given: User has Editor role for tenant
        SwitchToEditor();

        // And: Transaction data with whitespace-only category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "   "
        );

        // When: User creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Category should be empty string
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task UpdateTransaction_WithNewCategory_UpdatesSplitCategory()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Updated transaction data with new category
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Dining"
        );

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should be updated
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo("Dining"));
        Assert.That(updated.Key, Is.EqualTo(created.Key));
    }

    [Test]
    public async Task UpdateTransaction_FromNullCategoryToValue_UpdatesSplitCategory()
    {
        // Given: User has Editor role and creates a transaction without category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo(string.Empty));

        // And: Updated transaction data with category
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Entertainment"
        );

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should now be set
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo("Entertainment"));
    }

    [Test]
    public async Task UpdateTransaction_FromValueToNullCategory_ClearsSplitCategory()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Shopping"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created!.Category, Is.EqualTo("Shopping"));

        // And: Updated transaction data without category
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        // When: User updates the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should now be empty
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task QuickEditTransaction_WithNewCategory_UpdatesSplitCategory()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: "Original Memo",
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with new category
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Updated Payee",
            Memo: "Updated Memo",
            Category: "Dining"
        );

        // When: User quick edits the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should be updated
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo("Dining"));
        Assert.That(updated.Payee, Is.EqualTo("Updated Payee"));
        Assert.That(updated.Memo, Is.EqualTo("Updated Memo"));

        // And: Other fields should remain unchanged
        Assert.That(updated.Amount, Is.EqualTo(100m));
        Assert.That(updated.Date, Is.EqualTo(created.Date));
    }

    [Test]
    public async Task QuickEditTransaction_CategoryOnly_UpdatesOnlyCategory()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original Payee",
            Memo: "Original Memo",
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with only category changed
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Original Payee",  // Same
            Memo: "Original Memo",    // Same
            Category: "Groceries"     // Changed
        );

        // When: User quick edits the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Only category should be updated
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo("Groceries"));
        Assert.That(updated.Payee, Is.EqualTo("Original Payee"));
        Assert.That(updated.Memo, Is.EqualTo("Original Memo"));
    }

    [Test]
    public async Task QuickEditTransaction_WithSanitizedCategory_AppliesSanitization()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Quick edit data with category needing sanitization
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Test Payee",
            Memo: null,
            Category: "  dining : out  "
        );

        // When: User quick edits the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should be sanitized
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Category, Is.EqualTo("Dining:Out"));
    }

    [Test]
    public async Task GetTransactionsList_ReturnsCategoryFromSplits()
    {
        // Given: User has Editor role and creates multiple transactions with categories
        SwitchToEditor();
        var transaction1 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Store 1",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var transaction2 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Amount: 200m,
            Payee: "Store 2",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Entertainment"
        );
        var transaction3 = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Amount: 50m,
            Payee: "Store 3",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );

        var response1 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction1);
        var created1 = await response1.Content.ReadFromJsonAsync<TransactionDetailDto>();
        var response2 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction2);
        var created2 = await response2.Content.ReadFromJsonAsync<TransactionDetailDto>();
        var response3 = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", transaction3);
        var created3 = await response3.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User requests transaction list
        var listResponse = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: 200 OK should be returned
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: All transactions should include their category
        var transactions = await listResponse.Content.ReadFromJsonAsync<ICollection<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);

        var txn1 = transactions!.FirstOrDefault(t => t.Key == created1!.Key);
        Assert.That(txn1, Is.Not.Null);
        Assert.That(txn1!.Category, Is.EqualTo("Food"));

        var txn2 = transactions.FirstOrDefault(t => t.Key == created2!.Key);
        Assert.That(txn2, Is.Not.Null);
        Assert.That(txn2!.Category, Is.EqualTo("Entertainment"));

        var txn3 = transactions.FirstOrDefault(t => t.Key == created3!.Key);
        Assert.That(txn3, Is.Not.Null);
        Assert.That(txn3!.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task GetTransactionById_ReturnsCategoryFromSplit()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 150m,
            Payee: "Detail Test Store",
            Memo: "Test memo",
            Source: "Test Source",
            ExternalId: "TEST-001",
            Category: "Shopping:Online"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User retrieves the transaction by ID
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should include category from split
        var retrieved = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Key, Is.EqualTo(created.Key));
        Assert.That(retrieved.Category, Is.EqualTo("Shopping:Online"));
    }

    [Test]
    public async Task GetTransactionById_WithEmptyCategory_ReturnsEmptyString()
    {
        // Given: User has Editor role and creates a transaction without category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 150m,
            Payee: "No Category Store",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: null
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // When: User retrieves the transaction by ID
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Category should be empty string
        var retrieved = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Category, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task CreateTransaction_WithCategory_AsViewer_ReturnsForbidden()
    {
        // Given: User has Viewer role for tenant (read-only)
        SwitchToViewer();

        // And: Valid transaction data with category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Test Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );

        // When: Viewer attempts to create a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CreateTransaction_WithCategory_AsOwner_ReturnsCreated()
    {
        // Given: User has Owner role for tenant
        SwitchToOwner();

        // And: Valid transaction data with category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Owner Transaction",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Business:Travel"
        );

        // When: Owner creates a transaction
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", newTransaction);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Category should be properly sanitized
        var created = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Category, Is.EqualTo("Business:Travel"));
    }

    [Test]
    public async Task UpdateTransaction_WithCategory_AsViewer_ReturnsForbidden()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: User switches to Viewer role
        SwitchToViewer();

        // And: Updated transaction data with new category
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Updated",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Dining"
        );

        // When: Viewer attempts to update the transaction
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task QuickEditTransaction_WithCategory_AsViewer_ReturnsForbidden()
    {
        // Given: User has Editor role and creates a transaction
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: User switches to Viewer role
        SwitchToViewer();

        // And: Quick edit data with new category
        var quickEditDto = new TransactionQuickEditDto(
            Payee: "Updated",
            Memo: null,
            Category: "Dining"
        );

        // When: Viewer attempts to quick edit the transaction
        var response = await _client.PatchAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}",
            JsonContent.Create(quickEditDto)
        );

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CreateTransaction_WithCategory_InOtherTenant_ReturnsForbidden()
    {
        // Given: User has Editor role for their tenant
        SwitchToEditor();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // And: Transaction data with category
        var newTransaction = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Isolation Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );

        // When: User attempts to create transaction in other tenant
        var response = await _client.PostAsJsonAsync($"/api/tenant/{otherTenantKey}/transactions", newTransaction);

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateTransaction_WithCategory_InOtherTenant_ReturnsForbidden()
    {
        // Given: User has Editor role and creates a transaction with category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Original",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Food"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // And: Updated transaction data with new category
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Updated",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Dining"
        );

        // When: User attempts to update transaction via other tenant's endpoint
        var response = await _client.PutAsJsonAsync($"/api/tenant/{otherTenantKey}/transactions/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion
}
