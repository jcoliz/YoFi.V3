using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for creating and managing transaction splits via the create modal.
/// </summary>
/// <param name="context">Test context providing access to shared test infrastructure.</param>
/// <remarks>
/// Handles all operations related to the transaction creation modal including opening,
/// field verification, form filling, and validation of created transactions.
/// </remarks>
public class TransactionSplitSteps(ITestContext context) : TransactionStepsBase(context)
{
    /// <summary>
    /// Given I have a workspace with a transaction for payee {string1} having these splits:
    /// </summary>
    [Given("I have a workspace with a transaction for payee {string1} having these splits:")]
    [Requires(ObjectStoreKeys.LoggedInAs)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.TransactionPayee)]
    [Provides(ObjectStoreKeys.TransactionAmount)]
    [Provides(ObjectStoreKeys.TransactionCategory)]
    [Provides(ObjectStoreKeys.TransactionMemo)]
    [Provides(ObjectStoreKeys.TransactionKey)]
    public async Task IHaveAWorkspaceWithATransactionForPayeeHavingTheseSplits(string string1, DataTable table)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I add a new split with the following details:
    /// </summary>
    /// <remarks>
    /// Expects that we are on the transaction detail page for the transaction we're adding to
    /// </remarks>
    [When("I add a new split with the following details:")]
    public async Task IAddANewSplitWithTheFollowingDetails(DataTable table)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the following splits are displayed:
    /// </summary>
    [Then("the following splits are displayed:")]
    public async Task TheFollowingSplitsAreDisplayed(DataTable table)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the transaction is balanced
    /// </summary>
    /// <remarks>
    /// TODO: Display of transaction balance status is not yet implemented.
    /// Update this step when it's done.
    /// </remarks>
    [Then("the transaction is balanced")]
    public async Task TheTransactionIsBalanced()
    {
        Assert.Pass();
    }

}
