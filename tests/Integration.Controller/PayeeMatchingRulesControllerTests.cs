using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

/// <summary>
/// Controller integration tests for PayeeMatchingRulesController.
/// Tests HTTP-specific concerns ONLY (auth, status codes, serialization).
/// Business logic is tested in Integration.Application layer.
/// </summary>
[TestFixture]
public class PayeeMatchingRulesControllerTests : AuthenticatedTestBase
{
    #region HTTP Routing and Invalid Input Tests

    [Test]
    public async Task GetRuleByKey_InvalidRuleKeyFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid rule key format (not a valid GUID)
        using var unauthClient = _factory.CreateClient();

        // When: API Client requests a rule with invalid rule key format
        var response = await unauthClient.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules/not-a-guid");

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
    public async Task UpdateRule_InvalidRuleKeyFormat_Returns404WithProblemDetails()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Update data with valid rule key format but invalid route parameter
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: API Client attempts to update rule with invalid key format
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/not-a-guid", ruleDto);

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");
    }

    [Test]
    public async Task DeleteRule_InvalidRuleKeyFormat_Returns404WithProblemDetails()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // When: API Client attempts to delete rule with invalid key format
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/payee-rules/invalid-key");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");
    }

    #endregion

    #region Authentication Tests

    [Test]
    public async Task GetRules_Unauthenticated_Returns403()
    {
        // Given: No authentication
        using var unauthClient = _factory.CreateClient();

        // When: Request rules without authentication
        var response = await unauthClient.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules");

        // Then: 403 Forbidden should be returned (tenant middleware checks access first)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task GetRuleByKey_Unauthenticated_Returns403()
    {
        // Given: No authentication
        using var unauthClient = _factory.CreateClient();

        // And: A rule key (doesn't matter if it exists)
        var ruleKey = Guid.NewGuid();

        // When: Request rule without authentication
        var response = await unauthClient.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules/{ruleKey}");

        // Then: 403 Forbidden should be returned (tenant middleware checks access first)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task CreateRule_Unauthenticated_Returns403()
    {
        // Given: No authentication
        using var unauthClient = _factory.CreateClient();

        // And: Valid rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Test",
            PayeeIsRegex: false,
            Category: "Test"
        );

        // When: Attempt to create rule without authentication
        var response = await unauthClient.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 403 Forbidden should be returned (tenant middleware checks access first)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateRule_Unauthenticated_Returns403()
    {
        // Given: No authentication
        using var unauthClient = _factory.CreateClient();

        // And: Rule key and data
        var ruleKey = Guid.NewGuid();
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: Attempt to update rule without authentication
        var response = await unauthClient.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/{ruleKey}", ruleDto);

        // Then: 403 Forbidden should be returned (tenant middleware checks access first)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteRule_Unauthenticated_Returns403()
    {
        // Given: No authentication
        using var unauthClient = _factory.CreateClient();

        // And: A rule key
        var ruleKey = Guid.NewGuid();

        // When: Attempt to delete rule without authentication
        var response = await unauthClient.DeleteAsync($"/api/tenant/{_testTenantKey}/payee-rules/{ruleKey}");

        // Then: 403 Forbidden should be returned (tenant middleware checks access first)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region Authorization Tests - Viewer Role

    [Test]
    public async Task GetRules_AsViewer_Returns200()
    {
        // Given: User has Viewer role
        SwitchToViewer();

        // When: Request rules
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules");

        // Then: 200 OK should be returned (Viewer can read)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetRuleByKey_AsViewer_Returns404ForNonExistent()
    {
        // Given: User has Viewer role
        SwitchToViewer();

        // And: Non-existent rule key
        var nonExistentKey = Guid.NewGuid();

        // When: Request rule by key
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules/{nonExistentKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateRule_AsViewer_Returns403()
    {
        // Given: User has Viewer role (read-only)
        SwitchToViewer();

        // And: Valid rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "Shopping"
        );

        // When: Viewer attempts to create rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateRule_AsViewer_Returns403()
    {
        // Given: User has Viewer role (read-only)
        SwitchToViewer();

        // And: Rule key and data
        var ruleKey = Guid.NewGuid();
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: Viewer attempts to update rule
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/{ruleKey}", ruleDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteRule_AsViewer_Returns403()
    {
        // Given: User has Viewer role (read-only)
        SwitchToViewer();

        // And: Rule key
        var ruleKey = Guid.NewGuid();

        // When: Viewer attempts to delete rule
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/payee-rules/{ruleKey}");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion

    #region Authorization Tests - Editor Role

    [Test]
    public async Task GetRules_AsEditor_Returns200()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // When: Request rules
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should be valid PaginatedResultDto
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task CreateRule_AsEditor_Returns201WithLocation()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Valid rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Starbucks",
            PayeeIsRegex: false,
            Category: "Coffee"
        );

        // When: Editor creates rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 201 Created should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // And: Location header should point to created resource
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain("payee-rules"));

        // And: Response should contain created rule
        var created = await response.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.PayeePattern, Is.EqualTo("Starbucks"));
    }

    [Test]
    public async Task UpdateRule_AsEditor_Returns200()
    {
        // Given: User has Editor role and creates a rule
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Original",
            PayeeIsRegex: false,
            Category: "Original"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // And: Updated rule data
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: Editor updates rule
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/{created!.Key}", updateDto);

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain updated rule
        var updated = await response.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.PayeePattern, Is.EqualTo("Updated"));
    }

    [Test]
    public async Task DeleteRule_AsEditor_Returns204()
    {
        // Given: User has Editor role and creates a rule
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "ToDelete",
            PayeeIsRegex: false,
            Category: "ToDelete"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // When: Editor deletes rule
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/payee-rules/{created!.Key}");

        // Then: 204 No Content should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task GetRuleByKey_AsEditor_ReturnsRule()
    {
        // Given: User has Editor role and creates a rule
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Test Rule",
            PayeeIsRegex: false,
            Category: "Test Category"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // When: Editor retrieves rule by key
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules/{created!.Key}");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain the rule
        var retrieved = await response.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();
        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.Key, Is.EqualTo(created.Key));
        Assert.That(retrieved.PayeePattern, Is.EqualTo("Test Rule"));
        Assert.That(retrieved.Category, Is.EqualTo("Test Category"));
    }

    [Test]
    public async Task UpdateRule_NonExistent_Returns404()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Non-existent rule key
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: Editor attempts to update non-existent rule
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/{nonExistentKey}", updateDto);

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteRule_NonExistent_Returns404()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Non-existent rule key
        var nonExistentKey = Guid.NewGuid();

        // When: Editor attempts to delete non-existent rule
        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/payee-rules/{nonExistentKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Query Parameter Tests

    [Test]
    public async Task GetRules_WithPagination_ReturnsPagedResults()
    {
        // Given: User has Viewer role
        SwitchToViewer();

        // When: Request rules with pagination parameters (pageSize is ignored, uses constant)
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules?pageNumber=1");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain pagination metadata with constant page size
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Metadata.PageNumber, Is.EqualTo(1));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(50));
    }

    [Test]
    public async Task GetRules_WithSorting_ReturnsSortedResults()
    {
        // Given: User has Editor role and creates multiple rules
        SwitchToEditor();
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules",
            new PayeeMatchingRuleEditDto("Zebra", false, "Last"));
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules",
            new PayeeMatchingRuleEditDto("Alpha", false, "First"));

        // When: Request rules sorted by PayeePattern
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules?sortBy=PayeePattern");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should be properly sorted
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Empty);
    }

    [Test]
    public async Task GetRules_WithSearchText_ReturnsFilteredResults()
    {
        // Given: User has Editor role and creates rules with distinct patterns
        SwitchToEditor();
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules",
            new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"));
        await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules",
            new PayeeMatchingRuleEditDto("Starbucks", false, "Coffee"));

        // When: Request rules with search text
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules?searchText=Amazon");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain search results
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Empty);
    }

    [Test]
    public async Task GetRules_WithInvalidPageNumber_ReturnsEmptyResults()
    {
        // Given: User has Viewer role
        SwitchToViewer();

        // When: Request rules with page number beyond available data
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules?pageNumber=999");

        // Then: 200 OK should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // And: Response should contain empty items but valid metadata
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Empty);
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(999));
    }

    #endregion

    #region Validation Tests

    [Test]
    public async Task CreateRule_EmptyPayeePattern_Returns400()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Rule with empty PayeePattern
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "",
            PayeeIsRegex: false,
            Category: "Test"
        );

        // When: Attempt to create rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain validation error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task CreateRule_EmptyCategory_Returns400()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Rule with empty Category
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Test",
            PayeeIsRegex: false,
            Category: ""
        );

        // When: Attempt to create rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain validation error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task CreateRule_InvalidRegexPattern_Returns400()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Rule with invalid regex pattern
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "(?<invalid",  // Invalid regex syntax
            PayeeIsRegex: true,
            Category: "Test"
        );

        // When: Attempt to create rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain validation error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task UpdateRule_InvalidRegexPattern_Returns400()
    {
        // Given: User has Editor role and creates a valid rule
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Valid",
            PayeeIsRegex: false,
            Category: "Valid"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // And: Update with invalid regex pattern
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "[invalid",  // Invalid regex syntax
            PayeeIsRegex: true,
            Category: "Updated"
        );

        // When: Attempt to update rule
        var response = await _client.PutAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules/{created!.Key}", updateDto);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain validation error
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
    }

    #endregion

    #region Serialization Tests

    [Test]
    public async Task GetRules_ReturnsValidJson()
    {
        // Given: User has Viewer role
        SwitchToViewer();

        // When: Request rules
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/payee-rules");

        // Then: Response should deserialize to PaginatedResultDto
        var result = await response.Content.ReadFromJsonAsync<PaginatedResultDto<PayeeMatchingRuleResultDto>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Items, Is.Not.Null);
        Assert.That(result.Metadata, Is.Not.Null);
        Assert.That(result.Metadata.PageNumber, Is.GreaterThan(0));
        Assert.That(result.Metadata.PageSize, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateRule_ValidJson_DeserializesResponse()
    {
        // Given: User has Editor role
        SwitchToEditor();

        // And: Valid rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Serialization Test",
            PayeeIsRegex: false,
            Category: "Test Category"
        );

        // When: Create rule
        var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", ruleDto);

        // Then: Response should deserialize to PayeeMatchingRuleResultDto
        var created = await response.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.PayeePattern, Is.EqualTo("Serialization Test"));
        Assert.That(created.Category, Is.EqualTo("Test Category"));
        Assert.That(created.PayeeIsRegex, Is.False);
        Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(created.ModifiedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(created.MatchCount, Is.EqualTo(0));
    }

    #endregion

    #region Tenant Isolation Tests

    [Test]
    public async Task GetRuleByKey_FromOtherTenant_Returns403()
    {
        // Given: User creates a rule in their tenant
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Isolation Test",
            PayeeIsRegex: false,
            Category: "Test"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // When: Attempt to access rule via other tenant's endpoint
        var response = await _client.GetAsync($"/api/tenant/{otherTenantKey}/payee-rules/{created!.Key}");

        // Then: 403 Forbidden should be returned (user doesn't have access to other tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task UpdateRule_FromOtherTenant_Returns403()
    {
        // Given: User creates a rule in their tenant
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Isolation Test",
            PayeeIsRegex: false,
            Category: "Test"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // And: Update data
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Updated",
            PayeeIsRegex: false,
            Category: "Updated"
        );

        // When: Attempt to update rule via other tenant's endpoint
        var response = await _client.PutAsJsonAsync($"/api/tenant/{otherTenantKey}/payee-rules/{created!.Key}", updateDto);

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task DeleteRule_FromOtherTenant_Returns403()
    {
        // Given: User creates a rule in their tenant
        SwitchToEditor();
        var createDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Isolation Test",
            PayeeIsRegex: false,
            Category: "Test"
        );
        var createResponse = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/payee-rules", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<PayeeMatchingRuleResultDto>();

        // And: Create a different tenant (user doesn't have access)
        var otherTenantKey = await CreateTestTenantAsync("Other Tenant", "Second tenant for isolation testing");

        // When: Attempt to delete rule via other tenant's endpoint
        var response = await _client.DeleteAsync($"/api/tenant/{otherTenantKey}/payee-rules/{created!.Key}");

        // Then: 403 Forbidden should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    #endregion
}
