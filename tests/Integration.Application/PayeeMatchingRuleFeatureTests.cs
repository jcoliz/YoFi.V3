using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.
/// </summary>
/// <remarks>
/// Tests PayeeMatchingRuleFeature methods with real ApplicationDbContext and IDataProvider
/// to verify business logic and database integration for payee matching rules.
/// </remarks>
[TestFixture]
public class PayeeMatchingRuleFeatureTests : FeatureTestBase
{
    private PayeeMatchingRuleFeature _feature = null!;
    private ITenantProvider _tenantProvider = null!;
    private IMemoryCache _memoryCache = null!;
    private Tenant _testTenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        // Create test tenant in database
        _testTenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = "Test Tenant"
        };
        _context.Tenants.Add(_testTenant);
        await _context.SaveChangesAsync();

        // Create tenant provider
        _tenantProvider = new TestTenantProvider { CurrentTenant = _testTenant };

        // Create memory cache
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create feature with dependencies
        _feature = new PayeeMatchingRuleFeature(_tenantProvider, _dataProvider, _memoryCache);
    }

    [TearDown]
    public void TearDownPayeeTests()
    {
        // Dispose memory cache before base class disposes context
        _memoryCache?.Dispose();
    }

    #region CreateRuleAsync Tests

    [Test]
    public async Task CreateRuleAsync_ValidSubstringRule_StoresInDatabase()
    {
        // Given: Valid substring rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "Shopping"
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Rule should be created with correct data
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.PayeePattern, Is.EqualTo("Amazon"));
        Assert.That(result.PayeeIsRegex, Is.False);
        Assert.That(result.Category, Is.EqualTo("Shopping"));
        Assert.That(result.MatchCount, Is.EqualTo(0));
        Assert.That(result.LastUsedAt, Is.Null);

        // And: Rule should be in database
        var dbRule = _context.PayeeMatchingRules.Single(r => r.Key == result.Key);
        Assert.That(dbRule.TenantId, Is.EqualTo(_testTenant.Id));
        Assert.That(dbRule.PayeePattern, Is.EqualTo("Amazon"));
    }

    [Test]
    public async Task CreateRuleAsync_ValidRegexRule_StoresInDatabase()
    {
        // Given: Valid regex rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "^AMZN.*",
            PayeeIsRegex: true,
            Category: "Shopping"
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Rule should be created with regex flag set
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.PayeePattern, Is.EqualTo("^AMZN.*"));
        Assert.That(result.PayeeIsRegex, Is.True);
        Assert.That(result.Category, Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task CreateRuleAsync_CategoryWithWhitespace_SanitizesCategory()
    {
        // Given: Rule with category containing extra whitespace
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "  Shopping : Online  "
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Category should be sanitized (whitespace collapsed, colons normalized)
        Assert.That(result.Category, Is.EqualTo("Shopping:Online"));

        // And: Sanitized category stored in database
        var dbRule = _context.PayeeMatchingRules.Single(r => r.Key == result.Key);
        Assert.That(dbRule.Category, Is.EqualTo("Shopping:Online"));
    }

    [Test]
    public async Task CreateRuleAsync_SetsTimestamps()
    {
        // Given: Valid rule data
        var beforeCreate = DateTimeOffset.UtcNow;
        var ruleDto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);
        var afterCreate = DateTimeOffset.UtcNow;

        // Then: CreatedAt and ModifiedAt should be set to same value
        Assert.That(result.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreate));
        Assert.That(result.CreatedAt, Is.LessThanOrEqualTo(afterCreate));
        Assert.That(result.ModifiedAt, Is.EqualTo(result.CreatedAt));
    }

    #endregion

    #region GetRulesAsync Tests

    [Test]
    public async Task GetRulesAsync_NoRules_ReturnsEmptyList()
    {
        // Given: No rules in database

        // When: Getting rules
        var result = await _feature.GetRulesAsync();

        // Then: Should return empty list with pagination metadata
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(0));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRulesAsync_MultipleRules_ReturnsOnlyCurrentTenantRules()
    {
        // Given: Rules for current tenant
        var rule1 = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        var rule2 = new PayeeMatchingRule
        {
            PayeePattern = "Safeway",
            Category = "Groceries",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        // And: Rule for different tenant
        var otherTenant = new Tenant { Key = Guid.NewGuid(), Name = "Other Tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync(); // Save tenant first

        var otherRule = new PayeeMatchingRule
        {
            PayeePattern = "Target",
            Category = "Shopping",
            TenantId = otherTenant.Id
        };
        _context.PayeeMatchingRules.Add(otherRule);
        await _context.SaveChangesAsync(); // Then save rule

        // When: Getting rules
        var result = await _feature.GetRulesAsync();

        // Then: Should return only current tenant's rules
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Amazon"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Safeway"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Does.Not.Contain("Target"));
    }

    [Test]
    public async Task GetRulesAsync_DefaultSort_SortsByPayeePatternAscending()
    {
        // Given: Multiple rules with different patterns
        var ruleB = new PayeeMatchingRule { PayeePattern = "Beta", Category = "Cat1", TenantId = _testTenant.Id };
        var ruleA = new PayeeMatchingRule { PayeePattern = "Alpha", Category = "Cat2", TenantId = _testTenant.Id };
        var ruleC = new PayeeMatchingRule { PayeePattern = "Charlie", Category = "Cat3", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(ruleB, ruleA, ruleC);
        await _context.SaveChangesAsync();

        // When: Getting rules with default sort
        var result = await _feature.GetRulesAsync();

        // Then: Should be sorted by PayeePattern A-Z
        Assert.That(result.Items, Has.Count.EqualTo(3));
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("Alpha"));
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("Beta"));
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("Charlie"));
    }

    [Test]
    public async Task GetRulesAsync_SortByCategory_SortsByCategoryAscending()
    {
        // Given: Multiple rules with different categories
        var rule1 = new PayeeMatchingRule { PayeePattern = "P1", Category = "Zebra", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "P2", Category = "Apple", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "P3", Category = "Banana", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Getting rules sorted by category
        var result = await _feature.GetRulesAsync(sortBy: PayeeRuleSortBy.Category);

        // Then: Should be sorted by Category A-Z
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].Category, Is.EqualTo("Apple"));
        Assert.That(itemsList[1].Category, Is.EqualTo("Banana"));
        Assert.That(itemsList[2].Category, Is.EqualTo("Zebra"));
    }

    [Test]
    public async Task GetRulesAsync_SortByLastUsedAt_SortsByMostRecentFirst()
    {
        // Given: Multiple rules with different LastUsedAt values
        var old = DateTimeOffset.UtcNow.AddDays(-10);
        var recent = DateTimeOffset.UtcNow.AddDays(-1);
        var rule1 = new PayeeMatchingRule { PayeePattern = "P1", Category = "C1", TenantId = _testTenant.Id, LastUsedAt = old };
        var rule2 = new PayeeMatchingRule { PayeePattern = "P2", Category = "C2", TenantId = _testTenant.Id, LastUsedAt = recent };
        var rule3 = new PayeeMatchingRule { PayeePattern = "P3", Category = "C3", TenantId = _testTenant.Id, LastUsedAt = null };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Getting rules sorted by LastUsedAt
        var result = await _feature.GetRulesAsync(sortBy: PayeeRuleSortBy.LastUsedAt);

        // Then: Should be sorted most recent first, null last
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("P2")); // Most recent
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("P1")); // Older
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("P3")); // Never used (null)
    }

    [Test]
    public async Task GetRulesAsync_WithPagination_ReturnsCorrectPage()
    {
        // Given: 10 rules
        for (int i = 1; i <= 10; i++)
        {
            var rule = new PayeeMatchingRule
            {
                PayeePattern = $"Pattern{i:D2}",
                Category = "Cat",
                TenantId = _testTenant.Id
            };
            _context.PayeeMatchingRules.Add(rule);
        }
        await _context.SaveChangesAsync();

        // When: Getting page 2 with 3 items per page
        var result = await _feature.GetRulesAsync(pageNumber: 2, pageSize: 3);

        // Then: Should return correct page
        Assert.That(result.Items, Has.Count.EqualTo(3));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(10));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(2));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(3));
        Assert.That(result.Metadata.TotalPages, Is.EqualTo(4));

        // And: Should contain items 4, 5, 6 (sorted by pattern)
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("Pattern04"));
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("Pattern05"));
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("Pattern06"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_FiltersPayeePattern()
    {
        // Given: Multiple rules
        var rule1 = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "Safeway", Category = "Groceries", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "Target", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Searching for "Safe"
        var result = await _feature.GetRulesAsync(searchText: "Safe");

        // Then: Should return only matching rule
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().PayeePattern, Is.EqualTo("Safeway"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_FiltersCategory()
    {
        // Given: Multiple rules
        var rule1 = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "Safeway", Category = "Groceries", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "Target", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Searching for "Shop"
        var result = await _feature.GetRulesAsync(searchText: "Shop");

        // Then: Should return rules with matching category
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Amazon"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Target"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_IsCaseInsensitive()
    {
        // Given: Rule with mixed case
        var rule = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Searching with different case
        var result = await _feature.GetRulesAsync(searchText: "AMAZON");

        // Then: Should find the rule
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().PayeePattern, Is.EqualTo("Amazon"));
    }

    #endregion

    #region GetRuleByKeyAsync Tests

    [Test]
    public async Task GetRuleByKeyAsync_ExistingRule_ReturnsRule()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Getting rule by key
        var result = await _feature.GetRuleByKeyAsync(rule.Key);

        // Then: Should return correct rule
        Assert.That(result.Key, Is.EqualTo(rule.Key));
        Assert.That(result.PayeePattern, Is.EqualTo("Amazon"));
        Assert.That(result.Category, Is.EqualTo("Shopping"));
    }

    [Test]
    public void GetRuleByKeyAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When/Then: Getting rule should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.GetRuleByKeyAsync(nonExistentKey));
    }

    [Test]
    public async Task GetRuleByKeyAsync_DifferentTenant_ThrowsNotFoundException()
    {
        // Given: Rule for different tenant
        var otherTenant = new Tenant { Key = Guid.NewGuid(), Name = "Other Tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync(); // Save tenant first

        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = otherTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync(); // Then save rule

        // When/Then: Getting rule from current tenant should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.GetRuleByKeyAsync(rule.Key));
    }

    #endregion

    #region UpdateRuleAsync Tests

    [Test]
    public async Task UpdateRuleAsync_ExistingRule_UpdatesFields()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var originalModifiedAt = rule.ModifiedAt;

        // When: Updating the rule
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "^AMZN.*",
            PayeeIsRegex: true,
            Category: "Online"
        );
        var result = await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Fields should be updated
        Assert.That(result.PayeePattern, Is.EqualTo("^AMZN.*"));
        Assert.That(result.PayeeIsRegex, Is.True);
        Assert.That(result.Category, Is.EqualTo("Online"));

        // And: ModifiedAt should be updated
        Assert.That(result.ModifiedAt, Is.GreaterThan(originalModifiedAt));

        // And: CreatedAt should remain unchanged
        Assert.That(result.CreatedAt, Is.EqualTo(rule.CreatedAt));
    }

    [Test]
    public async Task UpdateRuleAsync_CategoryWithWhitespace_SanitizesCategory()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Updating with category containing whitespace
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "  Online : Shopping  "
        );
        var result = await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Category should be sanitized
        Assert.That(result.Category, Is.EqualTo("Online:Shopping"));
    }

    [Test]
    public void UpdateRuleAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new PayeeMatchingRuleEditDto("Pattern", false, "Category");

        // When/Then: Updating should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.UpdateRuleAsync(nonExistentKey, updateDto));
    }

    #endregion

    #region DeleteRuleAsync Tests

    [Test]
    public async Task DeleteRuleAsync_ExistingRule_RemovesFromDatabase()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleKey = rule.Key;

        // When: Deleting the rule
        await _feature.DeleteRuleAsync(ruleKey);

        // Then: Rule should be removed from database
        var dbRule = _context.PayeeMatchingRules.SingleOrDefault(r => r.Key == ruleKey);
        Assert.That(dbRule, Is.Null);
    }

    [Test]
    public void DeleteRuleAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When/Then: Deleting should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.DeleteRuleAsync(nonExistentKey));
    }

    #endregion

    #region MatchPayeesAsync Tests

    [Test]
    public async Task MatchPayeesAsync_NoRules_ReturnsEmptyDictionary()
    {
        // Given: No rules in database
        var payees = new[] { "Amazon", "Safeway", "Target" };

        // When: Matching payees
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should return empty dictionary
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task MatchPayeesAsync_SubstringMatch_ReturnsCategory()
    {
        // Given: Substring rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Matching payee that contains pattern
        var payees = new[] { "Amazon.com", "Safeway" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should match Amazon.com
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Amazon.com"], Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task MatchPayeesAsync_RegexMatch_ReturnsCategory()
    {
        // Given: Regex rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "^AMZN.*",
            PayeeIsRegex = true,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches regex
        var payees = new[] { "AMZN Marketplace", "Target" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should match AMZN Marketplace
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["AMZN Marketplace"], Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task MatchPayeesAsync_UpdatesUsageStatistics()
    {
        // Given: Rule with initial statistics
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            MatchCount = 5,
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-7)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;

        // When: Matching payees (causes rule to match)
        var beforeMatch = DateTimeOffset.UtcNow;
        var payees = new[] { "Amazon.com" };
        await _feature.MatchPayeesAsync(payees);
        var afterMatch = DateTimeOffset.UtcNow;

        // Then: MatchCount should be incremented
        _context.ChangeTracker.Clear(); // Force reload from database
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(6));

        // And: LastUsedAt should be updated
        Assert.That(updatedRule.LastUsedAt, Is.Not.Null);
        Assert.That(updatedRule.LastUsedAt!.Value, Is.GreaterThanOrEqualTo(beforeMatch));
        Assert.That(updatedRule.LastUsedAt!.Value, Is.LessThanOrEqualTo(afterMatch));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_RegexBeatsSubstring()
    {
        // Given: Both regex and substring rules that match same payee
        var regexRule = new PayeeMatchingRule
        {
            PayeePattern = "^AMZN.*",
            PayeeIsRegex = true,
            Category = "Online",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var substringRule = new PayeeMatchingRule
        {
            PayeePattern = "AMZN",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(regexRule, substringRule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches both
        var payees = new[] { "AMZN Marketplace" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer regex rule
        Assert.That(result["AMZN Marketplace"], Is.EqualTo("Online"));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_LongerSubstringBeatsshorter()
    {
        // Given: Two substring rules, one longer than other
        var shortRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var longRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon Prime",
            PayeeIsRegex = false,
            Category = "Subscription",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(shortRule, longRule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches both
        var payees = new[] { "Amazon Prime Video" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer longer substring
        Assert.That(result["Amazon Prime Video"], Is.EqualTo("Subscription"));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_NewerBeatsOlder()
    {
        // Given: Two rules with same precedence, different ModifiedAt
        var olderRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var newerRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "E-Commerce",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(olderRule, newerRule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches both
        var payees = new[] { "Amazon.com" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer newer rule
        Assert.That(result["Amazon.com"], Is.EqualTo("E-Commerce"));
    }

    [Test]
    public async Task MatchPayeesAsync_DuplicatePayees_OnlyProcessedOnce()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            MatchCount = 0
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;

        // When: Matching with duplicate payees
        var payees = new[] { "Amazon.com", "Amazon.com", "Amazon.com" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should return single entry
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Amazon.com"], Is.EqualTo("Shopping"));

        // And: MatchCount should be incremented only once
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(1));
    }

    #endregion

    #region FindBestMatchAsync Tests

    [Test]
    public async Task FindBestMatchAsync_MatchingRule_ReturnsCategory()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Finding best match for payee
        var result = await _feature.FindBestMatchAsync("Amazon.com");

        // Then: Should return matching category
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task FindBestMatchAsync_NoMatch_ReturnsNull()
    {
        // Given: Rule that doesn't match
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Finding best match for non-matching payee
        var result = await _feature.FindBestMatchAsync("Safeway");

        // Then: Should return null
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindBestMatchAsync_DoesNotUpdateUsageStatistics()
    {
        // Given: Rule with usage statistics
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            MatchCount = 5,
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-7)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;
        var originalMatchCount = rule.MatchCount;
        var originalLastUsedAt = rule.LastUsedAt;

        // When: Finding best match (which matches the rule)
        await _feature.FindBestMatchAsync("Amazon.com");

        // Then: Usage statistics should NOT be updated
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(originalMatchCount));
        Assert.That(updatedRule.LastUsedAt, Is.EqualTo(originalLastUsedAt));
    }

    #endregion

    #region Cache Invalidation Tests

    [Test]
    public async Task CreateRuleAsync_InvalidatesCache()
    {
        // Given: Cache is populated by reading rules
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items, Is.Empty);

        // When: Creating a new rule
        var ruleDto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");
        await _feature.CreateRuleAsync(ruleDto);

        // Then: Next read should see the new rule
        var afterCreateResult = await _feature.GetRulesAsync();
        Assert.That(afterCreateResult.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UpdateRuleAsync_InvalidatesCache()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // And: Cache is populated
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items.First().Category, Is.EqualTo("Shopping"));

        // When: Updating the rule
        var updateDto = new PayeeMatchingRuleEditDto("Amazon", false, "Online");
        await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Next read should see updated category
        var afterUpdateResult = await _feature.GetRulesAsync();
        Assert.That(afterUpdateResult.Items.First().Category, Is.EqualTo("Online"));
    }

    [Test]
    public async Task DeleteRuleAsync_InvalidatesCache()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // And: Cache is populated
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items, Has.Count.EqualTo(1));

        // When: Deleting the rule
        await _feature.DeleteRuleAsync(rule.Key);

        // Then: Next read should not see the rule
        var afterDeleteResult = await _feature.GetRulesAsync();
        Assert.That(afterDeleteResult.Items, Is.Empty);
    }

    #endregion
}

/// <summary>
/// Test implementation of ITenantProvider for testing.
/// </summary>
file class TestTenantProvider : ITenantProvider
{
    public required Tenant CurrentTenant { get; set; }
}
