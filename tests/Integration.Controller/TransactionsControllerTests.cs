using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
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

    #region Payee Rule Application Tests

    [Test]
    public async Task ApplyPayeeRules_AsEditor_AppliedResultReturnsAndPersistsCategory()
    {
        // Given: A single-split transaction and a matching tenant payee rule
        SwitchToEditor();
        var transaction = await CreateTransactionAsync("Coffee Shop", "Dining");
        await SeedPayeeRuleAsync("Coffee", "  cafes  ");

        // When: Applying the current tenant's payee rules
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(transaction.Key), null);

        // Then: HTTP 200 and the applied result are returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PayeeRuleApplicationResultDto>();
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.TransactionKey, Is.EqualTo(transaction.Key));
            Assert.That(result.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.Applied));
            Assert.That(result.PreviousCategory, Is.EqualTo("Dining"));
            Assert.That(result.Category, Is.EqualTo("Cafes"));
        }

        var persisted = await _client.GetFromJsonAsync<TransactionDetailDto>(
            $"/api/tenant/{_testTenantKey}/transactions/{transaction.Key}");
        Assert.That(persisted!.Category, Is.EqualTo("Cafes"));
    }

    [Test]
    public async Task ApplyPayeeRules_NoMatchReturnsPreservedCategory()
    {
        // Given: A categorized transaction and no matching tenant rule
        SwitchToEditor();
        await ClearPayeeRuleCacheAsync(_testTenantKey);
        var transaction = await CreateTransactionAsync("Unknown Vendor", "Miscellaneous");

        // When: Applying payee rules
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(transaction.Key), null);

        // Then: The no-match result preserves the category
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PayeeRuleApplicationResultDto>();
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.NoMatch));
            Assert.That(result.Category, Is.EqualTo("Miscellaneous"));
            Assert.That(result.PreviousCategory, Is.EqualTo("Miscellaneous"));
        }
    }

    [Test]
    public async Task ApplyPayeeRules_TransactionBecomesMultiSplitReturnsStaleOutcome()
    {
        // Given: A single-split transaction that becomes multi-split after page load
        SwitchToEditor();
        var transaction = await CreateTransactionAsync("Weekly Shop", "Groceries");
        var splitResponse = await _client.PostAsJsonAsync(
            $"{ApplyTransactionUrl(transaction.Key)}/splits",
            new SplitEditDto(-20m, "Household", "Second category"));
        Assert.That(splitResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // When: Applying rules to the stale row
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(transaction.Key), null);

        // Then: The expected stale outcome is returned as HTTP 200
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PayeeRuleApplicationResultDto>();
        Assert.That(result, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result!.Outcome,
                Is.EqualTo(PayeeRuleApplicationOutcome.TransactionHasMultipleSplits));
            Assert.That(result.Category, Is.Null);
            Assert.That(result.PreviousCategory, Is.Null);
        }
    }

    [Test]
    public async Task ApplyPayeeRules_TransactionWithoutSplitReturns400()
    {
        // Given: A tenant transaction without a category split
        SwitchToEditor();
        var transactionKey = await SeedTransactionAsync(
            _testTenantKey, "No Split", "No split", -10m, Array.Empty<Split>());

        // When: Applying payee rules
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(transactionKey), null);

        // Then: The invariant violation is returned as HTTP 400
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problem, Is.Not.Null);
    }

    [Test]
    public async Task ApplyPayeeRules_MissingTransactionReturns404()
    {
        // Given: An Editor and a transaction key that does not exist
        SwitchToEditor();

        // When: Applying payee rules
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(Guid.NewGuid()), null);

        // Then: HTTP 404 with problem details is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task ApplyPayeeRules_AsViewerReturns403()
    {
        // Given: A Viewer attempts the Editor operation
        SwitchToViewer();

        // When: Applying payee rules
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(Guid.NewGuid()), null);

        // Then: HTTP 403 Forbidden is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ApplyPayeeRules_UnauthenticatedReturns401()
    {
        // Given: A request without authentication
        using var factory = new BaseTestWebApplicationFactory(useTestAuthentication: false);
        using var client = factory.CreateClient();

        // When: Applying payee rules
        using var response = await client.PostAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{Guid.NewGuid()}/apply-payee-rules",
            null);

        // Then: HTTP 401 Unauthorized is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ApplyPayeeRules_OutsideTenantRouteReturns403()
    {
        // Given: A transaction and an Editor client with no access to another tenant
        var transaction = await CreateTransactionAsync("Other", "Dining");
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");

        // When: Calling that tenant's route with the transaction key
        using var response = await _client.PostAsync(
            $"/api/tenant/{otherTenantKey}/transactions/{transaction.Key}/apply-payee-rules",
            null);

        // Then: Tenant authorization rejects the request
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ApplyPayeeRules_OtherTenantTransactionReturns404()
    {
        // Given: A transaction owned by another tenant
        var otherTenantKey = await CreateTestTenantAsync("Other workspace");
        var otherTransactionKey = await SeedTransactionAsync(
            otherTenantKey, "Coffee Shop", "Other tenant", -10m,
            [new Split { Amount = -10m, Category = "Dining", Order = 0 }]);

        // When: The current tenant route applies rules to that key
        using var response = await _client.PostAsync(
            ApplyPayeeRulesUrl(otherTransactionKey), null);

        // Then: Tenant-scoped lookup returns 404
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ApplyPayeeRules_OpenApiDescribesRouteAndResponseStatuses()
    {
        // Given: The running API
        // When: Reading the OpenAPI document
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));
        var path = document.RootElement.GetProperty("paths").EnumerateObject().Single(property =>
            property.Name.Equals("/api/tenant/{tenantKey}/Transactions/{key}/apply-payee-rules",
                StringComparison.OrdinalIgnoreCase));

        // Then: The POST contract and all designed response statuses are documented
        var operation = path.Value.GetProperty("post");
        Assert.That(operation.TryGetProperty("requestBody", out _), Is.False);
        var responses = operation.GetProperty("responses");
        foreach (var status in new[] { "200", "400", "401", "403", "404", "409" })
        {
            Assert.That(responses.TryGetProperty(status, out _), Is.True,
                $"Missing {status} response");
        }
        var schema = responses.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString();
        Assert.That(schema, Does.EndWith("/PayeeRuleApplicationResultDto"));
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
    public async Task GetCategorySuggestions_AsViewer_ReturnsOK()
    {
        // Given: User has Viewer role for tenant
        SwitchToViewer();

        // When: User requests category suggestions
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions/categories?term=g");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: The response should be a collection
        var suggestions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<string>>();
        Assert.That(suggestions, Is.Not.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("::")]
    public async Task GetCategorySuggestions_WithInvalidTerm_ReturnsBadRequest(string? term)
    {
        // Given: A missing, blank, or sanitized-to-empty search term
        SwitchToViewer();
        var termQuery = term is null
            ? string.Empty
            : $"?term={Uri.EscapeDataString(term)}";

        // When: User requests category suggestions
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions/categories{termQuery}");

        // Then: 400 Bad Request with problem details should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.That(problem, Is.Not.Null);
    }

    [Test]
    public async Task GetCategorySuggestions_WithTermOverLimit_ReturnsBadRequest()
    {
        // Given: A category term longer than the 200-character limit
        SwitchToViewer();
        var term = Uri.EscapeDataString(new string('a', 201));

        // When: User requests category suggestions
        var response = await _client.GetAsync(
            $"/api/tenant/{_testTenantKey}/transactions/categories?term={term}");

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
    public async Task AddSplit_AsEditor_ReturnsBalancedTransactionWithOrderedSplitsAndMemo()
    {
        // Given: An existing transaction with its imported category
        SwitchToEditor();
        var createDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.UtcNow),
            Amount: 100m,
            Payee: "Split Test",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Imported"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/transactions", createDto);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(created, Is.Not.Null);

        // When: User adds a split
        var splitDto = new SplitEditDto(Amount: 35m, Category: "Dining", Memo: "Business lunch");
        var response = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{created!.Key}/splits",
            splitDto);

        // Then: Updated transaction is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TransactionDetailDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Key, Is.EqualTo(created.Key));
        Assert.That(updated.Splits, Has.Count.EqualTo(2));
        Assert.That(updated.Splits[0].Order, Is.EqualTo(0));
        Assert.That(updated.Splits[0].Category, Is.EqualTo("Imported"));
        Assert.That(updated.Splits[0].Amount, Is.EqualTo(65m));
        Assert.That(updated.Splits[1].Order, Is.EqualTo(1));
        Assert.That(updated.Splits[1].Category, Is.EqualTo("Dining"));
        Assert.That(updated.Splits[1].Amount, Is.EqualTo(35m));
        Assert.That(updated.Splits[1].Memo, Is.EqualTo("Business lunch"));
        Assert.That(updated.Splits.Sum(split => split.Amount), Is.EqualTo(updated.Amount));
    }

    [Test]
    public async Task AddSplit_AsViewer_ReturnsForbidden()
    {
        // Given: User has Viewer role
        SwitchToViewer();
        var splitDto = new SplitEditDto(Amount: 35m, Category: "Dining", Memo: null);

        // When: User attempts to add a split
        var response = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{Guid.NewGuid()}/splits",
            splitDto);

        // Then: Viewer cannot add a split
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task AddSplit_WithInvalidDto_ReturnsBadRequestWithValidationErrors()
    {
        // Given: Split amount exceeds the validated maximum
        SwitchToEditor();
        var splitDto = new SplitEditDto(Amount: 1_000_000_000m, Category: "Dining", Memo: null);

        // When: User attempts to add the invalid split
        var response = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{Guid.NewGuid()}/splits",
            splitDto);

        // Then: Request validation returns Bad Request with ProblemDetails
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task AddSplit_ForMissingTransaction_ReturnsNotFound()
    {
        // Given: A valid split and a transaction key that does not exist
        SwitchToEditor();
        var splitDto = new SplitEditDto(Amount: 35m, Category: "Dining", Memo: null);

        // When: User attempts to add the split
        var response = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions/{Guid.NewGuid()}/splits",
            splitDto);

        // Then: Not Found is returned with ProblemDetails
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo((int)HttpStatusCode.NotFound));
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
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
        Assert.That(problemDetails.Errors["Memo"], Is.Not.Empty);
        Assert.That(problemDetails.Detail, Is.Null);
        Assert.That(problemDetails.Extensions.ContainsKey("traceId"), Is.True);
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
        Assert.That(firstTransaction.HasMultipleSplits, Is.False);
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

    private string ApplyTransactionUrl(Guid key)
        => $"/api/tenant/{_testTenantKey}/transactions/{key}";

    private string ApplyPayeeRulesUrl(Guid key)
        => $"{ApplyTransactionUrl(key)}/apply-payee-rules";

    private async Task<TransactionDetailDto> CreateTransactionAsync(
        string payee,
        string category)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/tenant/{_testTenantKey}/transactions",
            new TransactionEditDto(
                new DateOnly(2026, 1, 1),
                -10m,
                payee,
                null,
                null,
                null,
                category));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<TransactionDetailDto>())!;
    }

    private async Task SeedPayeeRuleAsync(string payeePattern, string category)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants.SingleAsync(t => t.Key == _testTenantKey);
        dbContext.PayeeMatchingRules.Add(new PayeeMatchingRule
        {
            TenantId = tenant.Id,
            PayeePattern = payeePattern,
            PayeeIsRegex = false,
            Category = category,
            MatchCount = 0
        });
        await dbContext.SaveChangesAsync();
        await ClearPayeeRuleCacheAsync(_testTenantKey);
    }

    private async Task<Guid> SeedTransactionAsync(
        Guid tenantKey,
        string payee,
        string unusedCategory,
        decimal amount,
        IEnumerable<Split> splits)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = await dbContext.Tenants.SingleAsync(t => t.Key == tenantKey);
        var splitList = splits.ToList();
        var transaction = new Transaction
        {
            TenantId = tenant.Id,
            Date = new DateOnly(2026, 1, 1),
            Payee = payee,
            Amount = amount,
            Splits = splitList
        };
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();
        await ClearPayeeRuleCacheAsync(tenantKey);
        return transaction.Key;
    }

    private Task ClearPayeeRuleCacheAsync(Guid tenantKey)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenant = dbContext.Tenants.Single(t => t.Key == tenantKey);
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        cache.Remove($"payee-rules:{tenant.Id}");
        return Task.CompletedTask;
    }
}
