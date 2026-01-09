namespace YoFi.V3.Tests.Functional.Infrastructure;

/// <summary>
/// Central registry of all ObjectStore keys used across functional tests.
/// </summary>
/// <remarks>
/// This class provides a single source of truth for all object store keys,
/// preventing duplication and typos. All step classes should reference these
/// constants rather than using string literals.
/// </remarks>
public static class ObjectStoreKeys
{
    #region User and Authentication Keys

    /// <summary>
    /// The full username (with __TEST__ prefix) of the currently logged-in user.
    /// </summary>
    public const string LoggedInAs = "LoggedInAs";

    /// <summary>
    /// Username set by pre-login entitlement steps before actual login occurs.
    /// </summary>
    public const string PendingUserContext = "PendingUserContext";

    #endregion

    #region Workspace Keys

    /// <summary>
    /// The currently selected workspace name (with __TEST__ prefix).
    /// </summary>
    public const string CurrentWorkspace = "CurrentWorkspaceName";

    /// <summary>
    /// The new workspace name after a rename operation (with __TEST__ prefix).
    /// </summary>
    public const string NewWorkspaceName = "NewWorkspaceName";

    #endregion

    #region Transaction Keys

    /// <summary>
    /// The payee name of the last created or referenced transaction.
    /// </summary>
    public const string TransactionPayee = "TransactionPayee";

    /// <summary>
    /// The amount of the last created or referenced transaction (as string).
    /// </summary>
    public const string TransactionAmount = "TransactionAmount";

    /// <summary>
    /// The category of the last created or referenced transaction.
    /// </summary>
    public const string TransactionCategory = "TransactionCategory";

    /// <summary>
    /// The memo of the last created or referenced transaction.
    /// </summary>
    public const string TransactionMemo = "TransactionMemo";

    /// <summary>
    /// The source of the last created or referenced transaction.
    /// </summary>
    public const string TransactionSource = "TransactionSource";

    /// <summary>
    /// The external ID of the last created or referenced transaction.
    /// </summary>
    public const string TransactionExternalId = "TransactionExternalId";

    /// <summary>
    /// The unique key (Guid as string) of the last created or referenced transaction.
    /// </summary>
    public const string TransactionKey = "TransactionKey";

    /// <summary>
    /// Collection of GUIDs of the last seeded transactions.
    /// </summary>
    public const string ExistingTransactionKeys = "ExistingTransactionKeys";

    /// <summary>
    /// Collection of entire transactions of the last seeded transactions.
    /// </summary>
    public const string ExistingTransactions = "ExistingTransactions";

    #endregion

    #region Permission Check Keys

    /// <summary>
    /// Boolean flag indicating whether user has access to a workspace (for negative tests).
    /// </summary>
    public const string HasWorkspaceAccess = "HasWorkspaceAccess";

    /// <summary>
    /// Boolean flag indicating whether user can delete a workspace (for permission tests).
    /// </summary>
    public const string CanDeleteWorkspace = "CanDeleteWorkspace";

    /// <summary>
    /// Boolean flag indicating whether user can make desired changes (for permission tests).
    /// </summary>
    public const string CanMakeDesiredChanges = "CanMakeDesiredChanges";

    #endregion

    #region Edit Mode Keys

    /// <summary>
    /// The current edit mode context (e.g., "CreateModal", "TransactionDetailsPage").
    /// </summary>
    public const string EditMode = "EditMode";

    #endregion

    #region Bank Import Keys

    /// <summary>
    /// File path to a generated OFX file for import testing.
    /// </summary>
    public const string OfxFilePath = "OfxFilePath";

    /// <summary>
    /// Collection of payee names from recently-uploaded transactions.
    /// </summary>
    public const string UploadedTransactionPayees = "UploadedTransactionPayees";

    #endregion
}
