namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;

/// <summary>
/// Unit tests for Transactions-related pure business logic.
/// </summary>
/// <remarks>
/// TransactionsFeature requires IDataProvider for all operations, so business logic
/// testing is done in Application Integration tests (tests/Integration.Application/TransactionsFeatureTests.cs).
///
/// This file is kept for potential future pure logic tests that don't require data access.
/// </remarks>
[TestFixture]
public class TransactionsTests
{
    // No tests currently - TransactionsFeature business logic requires IDataProvider
    // and is tested in Application Integration tests.

    // Future: Add tests here for pure Transaction entity logic or helper methods
    // that don't require database access.
}
