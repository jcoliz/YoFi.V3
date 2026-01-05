using Microsoft.Playwright;
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Base class for transaction-related step definition classes.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides shared infrastructure for all transaction step classes:
/// - Common helper methods (prefix handling, object store access)
/// - Page object factory methods
/// - Composition of shared step classes
///
/// All transaction step classes should inherit from this base to maintain consistency.
/// Object store keys are centralized in <see cref="ObjectStoreKeys"/>.
/// </remarks>
public abstract class TransactionStepsBase(ITestContext context)
{
    /// <summary>
    /// Test context providing access to test infrastructure.
    /// </summary>
    protected readonly ITestContext _context = context;

    #region Composed Step Classes

    /// <summary>
    /// Provides navigation-related step definitions.
    /// </summary>
    protected readonly NavigationSteps NavigationSteps = new(context);

    /// <summary>
    /// Provides authentication-related step definitions.
    /// </summary>
    protected readonly AuthSteps AuthSteps = new(context);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls.
    /// </summary>
    /// <param name="name">The name without prefix.</param>
    /// <returns>The name with __TEST__ prefix.</returns>
    protected static string AddTestPrefix(string name) => $"__TEST__{name}";

    /// <summary>
    /// Gets a required value from the object store, throwing if not found.
    /// </summary>
    /// <param name="key">The object store key.</param>
    /// <returns>The stored value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is not found.</exception>
    protected string GetRequiredFromStore(string key)
    {
        return _context.ObjectStore.Get<string>(key)
            ?? throw new InvalidOperationException($"Required value '{key}' not found in object store");
    }

    /// <summary>
    /// Gets the last transaction payee from object store.
    /// </summary>
    /// <returns>The payee name of the last transaction.</returns>
    protected string GetLastTransactionPayee()
    {
        return GetRequiredFromStore(ObjectStoreKeys.TransactionPayee);
    }

    /// <summary>
    /// Asserts that a user cannot perform a specific action.
    /// </summary>
    /// <param name="actionKey">The object store key for the permission check.</param>
    /// <param name="message">The assertion failure message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the permission check key is not found.</exception>
    protected void AssertCannotPerformAction(string actionKey, string message)
    {
        var canPerform = _context.ObjectStore.Get<object>(actionKey) as bool?
            ?? throw new InvalidOperationException($"Permission check '{actionKey}' not found");
        Assert.That(canPerform, Is.False, message);
    }

    #endregion
}
