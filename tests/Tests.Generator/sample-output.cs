using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Steps.Transaction;
using YoFi.V3.Tests.Functional.Steps.Workspace;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Transaction Record Fields
/// </summary>
/// <remarks>
/// As a user managing transactions
/// I want to record additional details about each transaction
/// So that I can track memo notes, source accounts, and external identifiers
/// </remarks>
public partial class TransactionRecordFieldsFeature_Tests : FunctionalTestBase
{
    #region Step class references

    protected NavigationSteps NavigationSteps => _theNavigationSteps ??= new(this);
    private NavigationSteps? _theNavigationSteps;

    protected AuthSteps AuthSteps => _theAuthSteps ??= new(this);
    private AuthSteps? _theAuthSteps;

    protected WorkspaceDataSteps WorkspaceDataSteps => _theWorkspaceDataSteps ??= new(this);
    private WorkspaceDataSteps? _theWorkspaceDataSteps;

    protected TransactionDataSteps TransactionDataSteps => _theTransactionDataSteps ??= new(this);
    private TransactionDataSteps? _theTransactionDataSteps;

    protected TransactionListSteps TransactionListSteps => _theTransactionListSteps ??= new(this);
    private TransactionListSteps? _theTransactionListSteps;

    protected TransactionQuickEditSteps TransactionQuickEditSteps => _theTransactionQuickEditSteps ??= new(this);
    private TransactionQuickEditSteps? _theTransactionQuickEditSteps;

    protected TransactionCreateSteps TransactionCreateSteps => _theTransactionCreateSteps ??= new(this);
    private TransactionCreateSteps? _theTransactionCreateSteps;

    protected TransactionSharedSteps TransactionSharedSteps => _theTransactionSharedSteps ??= new(this);
    private TransactionSharedSteps? _theTransactionSharedSteps;

    protected TransactionDetailsSteps TransactionDetailsSteps => _theTransactionDetailsSteps ??= new(this);
    private TransactionDetailsSteps? _theTransactionDetailsSteps;

    protected TransactionEditSteps TransactionEditSteps => _theTransactionEditSteps ??= new(this);
    private TransactionEditSteps? _theTransactionEditSteps;

    #endregion

    [SetUp]
    public async Task SetupAsync()
    {
        // Given the application is running
        await NavigationSteps.GivenLaunchedSite();

        // And I am logged in as a user with "Editor" role
        await WorkspaceDataSteps.GivenIAmLoggedInAsAUserWithEditorRole();

    }

    #region Rule: Quick Edit Modal
    // The quick edit modal should only show Payee, Category, and Memo fields for rapid updates

    /// <summary>
    /// Quick edit modal shows Payee, Category, and Memo fields
    /// </summary>
    [Test]
    public async Task QuickEditModalShowsPayeeCategoryAndMemoFields()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Shop"],
            ["Amount", "5.50"],
            ["Category", "Beverages"],
            ["Memo", "Morning coffee"]
        );
        await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await TransactionListSteps.GivenIAmOnTheTransactionsPage();

        // When I click the "Edit" button on the transaction
        await TransactionQuickEditSteps.WhenIClickTheEditButtonOnTheTransaction();

        // Then I should see a modal titled "Quick Edit Transaction"
        await TransactionQuickEditSteps.ThenIShouldSeeAModalTitled("Quick Edit Transaction");

        // And I should only see fields for "Payee", "Category", and "Memo"
        await TransactionQuickEditSteps.ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo();

    }

    /// <summary>
    /// User updates Memo via quick edit modal
    /// </summary>
    [Test]
    public async Task UserUpdatesMemoViaQuickEditModal()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Co"],
            ["Amount", "-5.50"],
            ["Memo", "Morning latte"]
        );
        await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await TransactionListSteps.GivenIAmOnTheTransactionsPage();

        // When I quick edit the transaction
        await TransactionQuickEditSteps.WhenIQuickEditTheTransaction();

        // And I change Memo to "Large latte with extra shot"
        await TransactionQuickEditSteps.WhenIChangeMemoTo("Large latte with extra shot");

        // And I click "Update"
        await TransactionQuickEditSteps.WhenIClickUpdate();

        // Then the modal should close
        await TransactionSharedSteps.ThenTheModalShouldClose();

    }

    #endregion

    #region Rule: Access Control
    // The system enforces authentication requirements for protected resources

    /// <summary>
    /// Logged in user cannot access login page
    /// </summary>
    /// <remarks>
    /// It seems the application doesn't always redirect logged-in users away from the login page.
    /// This could be due to caching issues or session management quirks.
    ///
    /// UPDATE: Have worked in this area, so it seems to be working now. Will try
    /// removing the Explicit attribute to see if the test is stable.
    /// </remarks>
    [Test]
    public async Task LoggedInUserCannotAccessLoginPage()
    {
        // Given I am logged in
        await AuthSteps.GivenIAmLoggedIn();

        // When I try to navigate directly to the login page, expecting it to fail
        await NavigationSteps.WhenITryToNavigateDirectlyToTheLoginPageExpectingFailure();

        // Then I should be redirected to my profile page
        await NavigationSteps.ThenIShouldBeRedirectedToMyProfilePage();

        // And I should not see the login form
        await AuthSteps.ThenIShouldNotSeeTheLoginForm();

    }

    /// <summary>
    /// Anonymous user cannot access protected pages
    /// </summary>
    [TestCase("/weather")]
    [TestCase("/counter")]
    [TestCase("/about")]
    [TestCase("/profile")]
    [Test]
    public async Task AnonymousUserCannotAccessProtectedPages(string page)
    {
        // Given I am not logged in
        await AuthSteps.GivenIAmNotLoggedIn();

        // When I try to navigate directly to a protected page like <page>
        await NavigationSteps.WhenITryToNavigateDirectlyToAProtectedPageLike(page);

        // Then I should be redirected to the login page
        await NavigationSteps.ThenIShouldBeRedirectedToTheLoginPage();

    }

    #endregion

    #region Rule: Transaction Details Page
    // Users can view, edit, and navigate transaction details

    /// <summary>
    /// User edits all fields on transaction details page
    /// </summary>
    [Test]
    public async Task UserEditsAllFieldsOnTransactionDetailsPage()
    {
        // Given I am viewing the details page for a transaction with:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Gas Mart"],
            ["Amount", "-40.00"],
            ["Source", "Chase Checking"]
        );
        await TransactionDataSteps.GivenIAmViewingTheDetailsPageForATransactionWith(table);

        // When I click the "Edit" button
        await TransactionEditSteps.WhenIClickTheEditButton();

        // And I change Source to "Chase Visa"
        await TransactionEditSteps.WhenIChangeSourceTo("Chase Visa");

        // And I click "Save"
        await TransactionSharedSteps.WhenIClickSave();

        // Then I should see "Chase Visa" as the Source
        await TransactionDetailsSteps.ThenIShouldSeeValueAsField("Chase Visa", "Source");

    }

    /// <summary>
    /// User sees all fields in create transaction modal
    /// </summary>
    [Test]
    public async Task UserSeesAllFieldsInCreateTransactionModal()
    {
        // Given I am on the transactions page
        await TransactionListSteps.GivenIAmOnTheTransactionsPage();

        // When I click the "Add Transaction" button
        await TransactionCreateSteps.WhenIClickTheAddTransactionButton();

        // Then I should see the following fields in the create form:
        var fieldsTable = new DataTable(
            ["Field"],
            ["Date"],
            ["Payee"],
            ["Amount"],
            ["Memo"],
            ["Source"],
            ["External ID"]
        );
        await TransactionCreateSteps.ThenIShouldSeeTheFollowingFieldsInTheCreateForm(fieldsTable);

    }

    #endregion


    #region Unimplemented Steps
    /// <summary>
    /// Given a new feature is being implemented
    /// </summary>
    async Task GivenANewFeatureIsBeingImplemented()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I perform action with "parameter"
    /// </summary>
    async Task WhenIPerformActionWithParameter(string parameter)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then I should see result with count 5
    /// </summary>
    async Task ThenIShouldSeeResultWithCount(int count)
    {
        throw new NotImplementedException();
    }

    #endregion
}
