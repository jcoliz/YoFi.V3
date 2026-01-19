using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>
/// Provides payee matching rule management functionality for the current tenant.
/// </summary>
/// <param name="tenantProvider">Provider for accessing the current tenant context.</param>
/// <param name="dataProvider">Provider for data access operations.</param>
/// <param name="memoryCache">Cache for storing rules per tenant.</param>
/// <remarks>
/// Manages CRUD operations on payee matching rules, rule matching logic, and cache management.
/// Rules are cached per tenant for performance during matching operations.
/// Implements <see cref="IPayeeMatchingService"/> to provide focused matching API for bank import.
/// </remarks>
public class PayeeMatchingRuleFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IMemoryCache memoryCache) : IPayeeMatchingService
{
    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    /// <summary>
    /// Gets paginated, sorted, and optionally filtered payee matching rules for the current tenant.
    /// </summary>
    /// <param name="pageNumber">Page number to retrieve (1-based, default: 1).</param>
    /// <param name="sortBy">Sort order (default: PayeePattern).</param>
    /// <param name="searchText">Optional plain text search across PayeePattern and Category (case-insensitive).</param>
    /// <returns>Paginated result containing rules and pagination metadata.</returns>
    public async Task<PaginatedResultDto<PayeeMatchingRuleResultDto>> GetRulesAsync(
        int pageNumber = 1,
        PayeeRuleSortBy sortBy = PayeeRuleSortBy.PayeePattern,
        string? searchText = null)
    {
        // Load all rules from cache
        var allRules = await GetRulesForTenantAsync();

        // Filter if search text provided
        var filteredRules = string.IsNullOrWhiteSpace(searchText)
            ? allRules
            : allRules.Where(r =>
                r.PayeePattern.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        // Sort in-memory based on sortBy parameter
        var sortedRules = SortRules(filteredRules, sortBy);

        // Paginate in-memory
        var actualPageSize = PaginationHelper.ItemsPerPage;
        var totalCount = sortedRules.Count;
        var paginatedRules = sortedRules
            .Skip((pageNumber - 1) * actualPageSize)
            .Take(actualPageSize)
            .ToList();

        // Convert to DTOs
        var dtos = paginatedRules.Select(ToResultDto).ToList();

        // Calculate pagination metadata
        var metadata = PaginationHelper.Calculate(pageNumber, totalCount);

        return new PaginatedResultDto<PayeeMatchingRuleResultDto>(dtos, metadata);
    }

    /// <summary>
    /// Gets a specific payee matching rule by its unique key.
    /// </summary>
    /// <param name="key">The unique identifier of the rule.</param>
    /// <returns>The requested rule.</returns>
    /// <exception cref="NotFoundException">Thrown when the rule is not found or belongs to a different tenant.</exception>
    public async Task<PayeeMatchingRuleResultDto> GetRuleByKeyAsync(Guid key)
    {
        var rule = await GetRuleByKeyInternalAsync(key);
        return ToResultDto(rule);
    }

    /// <summary>
    /// Creates a new payee matching rule for the current tenant.
    /// </summary>
    /// <param name="ruleDto">The rule data to add. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.PayeeMatchingRuleEditDtoValidator"/> for validation rules.</param>
    /// <returns>The created rule with generated Key.</returns>
    public async Task<PayeeMatchingRuleResultDto> CreateRuleAsync(PayeeMatchingRuleEditDto ruleDto)
    {
        // Sanitize category before storing
        var sanitizedCategory = CategoryHelper.SanitizeCategory(ruleDto.Category);

        var now = DateTimeOffset.UtcNow;
        var newRule = new PayeeMatchingRule
        {
            PayeePattern = ruleDto.PayeePattern,
            PayeeIsRegex = ruleDto.PayeeIsRegex,
            Category = sanitizedCategory,
            TenantId = _currentTenant.Id,
            CreatedAt = now,
            ModifiedAt = now,
            MatchCount = 0,
            LastUsedAt = null
        };

        dataProvider.Add(newRule);
        await dataProvider.SaveChangesAsync();

        // Invalidate cache
        InvalidateCache();

        return ToResultDto(newRule);
    }

    /// <summary>
    /// Updates an existing payee matching rule.
    /// </summary>
    /// <param name="key">The unique identifier of the rule to update.</param>
    /// <param name="ruleDto">The updated rule data. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.PayeeMatchingRuleEditDtoValidator"/> for validation rules.</param>
    /// <returns>The updated rule.</returns>
    /// <exception cref="NotFoundException">Thrown when the rule is not found or belongs to a different tenant.</exception>
    public async Task<PayeeMatchingRuleResultDto> UpdateRuleAsync(Guid key, PayeeMatchingRuleEditDto ruleDto)
    {
        var existingRule = await GetRuleByKeyInternalAsync(key);

        // Sanitize category before storing
        var sanitizedCategory = CategoryHelper.SanitizeCategory(ruleDto.Category);

        // Update fields
        existingRule.PayeePattern = ruleDto.PayeePattern;
        existingRule.PayeeIsRegex = ruleDto.PayeeIsRegex;
        existingRule.Category = sanitizedCategory;
        existingRule.ModifiedAt = DateTimeOffset.UtcNow;

        dataProvider.UpdateRange([existingRule]);
        await dataProvider.SaveChangesAsync();

        // Invalidate cache
        InvalidateCache();

        return ToResultDto(existingRule);
    }

    /// <summary>
    /// Deletes a payee matching rule.
    /// </summary>
    /// <param name="key">The unique identifier of the rule to delete.</param>
    /// <exception cref="NotFoundException">Thrown when the rule is not found or belongs to a different tenant.</exception>
    public async Task DeleteRuleAsync(Guid key)
    {
        var rule = await GetRuleByKeyInternalAsync(key);
        dataProvider.Remove(rule);
        await dataProvider.SaveChangesAsync();

        // Invalidate cache
        InvalidateCache();
    }

    /// <summary>
    /// Matches payee strings against rules and returns category mappings for matched payees.
    /// </summary>
    /// <param name="payees">Collection of payee strings to match against rules.</param>
    /// <returns>Dictionary mapping payee strings to their matched categories. Only includes payees that matched a rule.</returns>
    /// <remarks>
    /// Loads all rules for current tenant from cache, sorted by ModifiedAt DESC for conflict resolution.
    /// Updates usage statistics (MatchCount and LastUsedAt) for matched rules.
    /// This method is designed to be called during bank import before creating ImportReviewTransaction entities.
    /// The caller is responsible for applying the returned category mappings to their transactions.
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, string>> MatchPayeesAsync(IEnumerable<string> payees)
    {
        var distinctPayees = payees.Distinct().ToList();

        if (distinctPayees.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        // Load rules sorted by ModifiedAt DESC for conflict resolution
        var rules = await GetRulesForTenantAsync();
        var sortedRules = rules.OrderByDescending(r => r.ModifiedAt).ToList();

        // Track which rules matched for usage statistics
        var matchedRuleIds = new HashSet<long>();
        var payeeToCategory = new Dictionary<string, string>();

        // Match each unique payee
        foreach (var payee in distinctPayees)
        {
            var matchedCategory = PayeeMatchingHelper.FindBestMatch(payee, sortedRules);
            if (matchedCategory != null)
            {
                payeeToCategory[payee] = matchedCategory;

                // Find which rule matched to track statistics
                var matchedRule = sortedRules.FirstOrDefault(r => r.Category == matchedCategory);
                if (matchedRule != null)
                {
                    matchedRuleIds.Add(matchedRule.Id);
                }
            }
        }

        // Update usage statistics for matched rules
        if (matchedRuleIds.Count > 0)
        {
            await UpdateRuleUsageStatisticsAsync(matchedRuleIds);
        }

        return payeeToCategory;
    }

    /// <summary>
    /// Finds the best matching rule for a single payee string.
    /// </summary>
    /// <param name="payee">Transaction payee string to match.</param>
    /// <returns>Category string from best matching rule, or null if no match.</returns>
    /// <remarks>
    /// Used for manual matching operations (Story 3).
    /// Does not update usage statistics.
    /// </remarks>
    public async Task<string?> FindBestMatchAsync(string payee)
    {
        var rules = await GetRulesForTenantAsync();
        var sortedRules = rules.OrderByDescending(r => r.ModifiedAt).ToList();
        return PayeeMatchingHelper.FindBestMatch(payee, sortedRules);
    }

    /// <summary>
    /// Applies matching rules to a collection of transactions, returning matched categories in parallel order.
    /// </summary>
    /// <param name="transactions">Transactions to categorize.</param>
    /// <returns>Parallel array of categories (null if no match). Order matches input transactions.</returns>
    /// <remarks>
    /// This method implements <see cref="IPayeeMatchingService.ApplyMatchingRulesAsync"/> for bank import integration.
    /// Returns categories in the same order as input transactions for easy zipping.
    /// Updates usage statistics (MatchCount and LastUsedAt) for matched rules.
    /// </remarks>
    public async Task<IReadOnlyList<string?>> ApplyMatchingRulesAsync(
        IReadOnlyCollection<IMatchableTransaction> transactions)
    {
        if (transactions.Count == 0)
        {
            return Array.Empty<string?>();
        }

        // Use MatchPayeesAsync to get category mappings for all unique payees
        var payees = transactions.Select(t => t.Payee).Distinct();
        var payeeToCategory = await MatchPayeesAsync(payees);

        // Return parallel array of categories in same order as input
        return transactions
            .Select(t => payeeToCategory.TryGetValue(t.Payee, out var category) ? category : null)
            .ToList();
    }

    /// <summary>
    /// Loads all payee matching rules for the current tenant from cache or database.
    /// </summary>
    /// <returns>Unsorted list of rules for the current tenant.</returns>
    private async Task<List<PayeeMatchingRule>> GetRulesForTenantAsync()
    {
        var cacheKey = $"payee-rules:{_currentTenant.Id}";

        if (memoryCache.TryGetValue(cacheKey, out List<PayeeMatchingRule>? cachedRules) && cachedRules != null)
        {
            return cachedRules;
        }

        // Cache miss - load from database
        var query = dataProvider.Get<PayeeMatchingRule>()
            .Where(r => r.TenantId == _currentTenant.Id);

        var rules = await dataProvider.ToListNoTrackingAsync(query);

        // Store in cache (no expiration - explicit invalidation on changes)
        memoryCache.Set(cacheKey, rules);

        return rules;
    }

    /// <summary>
    /// Invalidates the cached rules for the current tenant.
    /// </summary>
    private void InvalidateCache()
    {
        var cacheKey = $"payee-rules:{_currentTenant.Id}";
        memoryCache.Remove(cacheKey);
    }

    /// <summary>
    /// Internal method to retrieve a rule entity by its key.
    /// </summary>
    /// <param name="key">The unique identifier of the rule.</param>
    /// <returns>The rule entity.</returns>
    /// <exception cref="NotFoundException">Thrown when the rule is not found or belongs to a different tenant.</exception>
    private async Task<PayeeMatchingRule> GetRuleByKeyInternalAsync(Guid key)
    {
        var query = dataProvider.Get<PayeeMatchingRule>()
            .Where(r => r.Key == key && r.TenantId == _currentTenant.Id);

        var result = await dataProvider.SingleOrDefaultAsync(query);

        if (result == null)
        {
            throw new PayeeMatchingRuleNotFoundException(key);
        }

        return result;
    }

    /// <summary>
    /// Updates usage statistics (MatchCount and LastUsedAt) for matched rules.
    /// </summary>
    /// <param name="ruleIds">Set of rule IDs that matched transactions.</param>
    private async Task UpdateRuleUsageStatisticsAsync(HashSet<long> ruleIds)
    {
        var now = DateTimeOffset.UtcNow;

        // Load rules by IDs
        var query = dataProvider.Get<PayeeMatchingRule>()
            .Where(r => ruleIds.Contains(r.Id));

        var rules = await dataProvider.ToListAsync(query);

        foreach (var rule in rules)
        {
            rule.MatchCount++;
            rule.LastUsedAt = now;
        }

        dataProvider.UpdateRange(rules);
        await dataProvider.SaveChangesAsync();

        // Invalidate cache after updating statistics
        InvalidateCache();
    }

    /// <summary>
    /// Sorts rules based on the specified sort order.
    /// </summary>
    private static List<PayeeMatchingRule> SortRules(IEnumerable<PayeeMatchingRule> rules, PayeeRuleSortBy sortBy)
    {
        return sortBy switch
        {
            PayeeRuleSortBy.PayeePattern => rules.OrderBy(r => r.PayeePattern).ToList(),
            PayeeRuleSortBy.Category => rules.OrderBy(r => r.Category).ToList(),
            PayeeRuleSortBy.LastUsedAt => rules.OrderByDescending(r => r.LastUsedAt ?? DateTimeOffset.MinValue).ToList(),
            _ => rules.OrderBy(r => r.PayeePattern).ToList()
        };
    }

    /// <summary>
    /// Converts a PayeeMatchingRule entity to a result DTO.
    /// </summary>
    private static PayeeMatchingRuleResultDto ToResultDto(PayeeMatchingRule rule)
    {
        return new PayeeMatchingRuleResultDto(
            rule.Key,
            rule.PayeePattern,
            rule.PayeeIsRegex,
            rule.Category,
            rule.CreatedAt,
            rule.ModifiedAt,
            rule.LastUsedAt,
            rule.MatchCount
        );
    }
}

/// <summary>
/// Sort options for payee matching rules.
/// </summary>
public enum PayeeRuleSortBy
{
    /// <summary>Sort by payee pattern alphabetically.</summary>
    PayeePattern,

    /// <summary>Sort by category alphabetically.</summary>
    Category,

    /// <summary>Sort by last used date (most recent first).</summary>
    LastUsedAt
}
