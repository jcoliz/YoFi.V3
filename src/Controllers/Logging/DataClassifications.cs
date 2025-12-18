using Microsoft.Extensions.Compliance.Classification;

namespace YoFi.V3.Controllers.Logging;

/// <summary>
/// Data classification taxonomy for logging.
/// </summary>
/// <remarks>
/// WARNING: If you apply these classifications to data, you MUST ensure that
/// the corresponding redaction policies are implemented in the logging pipeline.
/// Each classification here has specific redaction requirements per environment
/// (development, container/CI, production) that must be enforced to protect
/// sensitive information and comply with privacy/security standards.
/// </remarks>
public static class DataTaxonomy
{
    /// <summary>
    /// Gets the data classification for Personally Identifiable Information (PII).
    /// </summary>
    public static DataClassification PII { get; } = new DataClassification("Logging", "PII");

    /// <summary>
    /// Gets the data classification for test/synthetic data.
    /// </summary>
    public static DataClassification TestData { get; } = new DataClassification("Logging", "TestData");

    /// <summary>
    /// Gets the data classification for security secrets and credentials.
    /// </summary>
    public static DataClassification Secrets { get; } = new DataClassification("Logging", "Secrets");

    /// <summary>
    /// Gets the data classification for authentication tokens.
    /// </summary>
    public static DataClassification AuthToken { get; } = new DataClassification("Logging", "AuthToken");

    /// <summary>
    /// Gets the data classification for financial or sensitive business data.
    /// </summary>
    public static DataClassification FinancialData { get; } = new DataClassification("Logging", "FinancialData");
}

/// <summary>
/// Personally Identifiable Information (PII).
/// </summary>
/// <remarks>
/// Examples: Email addresses, usernames, full names, phone numbers.
///
/// Redaction policy:
/// - Development: Not redacted (test data)
/// - Container/CI: Not redacted (test data)
/// - Production: Fully redacted (real user PII)
///
/// Used for data that identifies or can be used to identify a specific person.
/// In development/testing, this is synthetic test data. In production, it's real PII
/// that must be protected for privacy compliance (GDPR, CCPA, etc.).
/// </remarks>
public class PIIAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PIIAttribute"/> class.
    /// </summary>
    public PIIAttribute() : base(DataTaxonomy.PII) { }
}

/// <summary>
/// Test/synthetic data that mimics sensitive information but has no real-world value.
/// </summary>
/// <remarks>
/// Examples: Test emails, test usernames, seed data, synthetic financial records.
///
/// Redaction policy:
/// - Development: Not redacted (helps debugging)
/// - Container/CI: Not redacted (helps debugging functional tests)
/// - Production: N/A (should never appear in production)
///
/// This classification is for data that LOOKS like PII or sensitive data but is
/// actually synthetic/fake. Unlike [PII] which describes data that might be test
/// data in dev but is real in production, [TestData] describes data that is ALWAYS
/// fake and has no real-world value.
///
/// Use this when you want to explicitly document "this is safe to log everywhere
/// because it's always test data." Useful for:
/// - Hardcoded test users (test@example.com)
/// - Seed data accounts
/// - Demo/sample data
/// - Data in test-only endpoints
/// </remarks>
public class TestDataAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestDataAttribute"/> class.
    /// </summary>
    public TestDataAttribute() : base(DataTaxonomy.TestData) { }
}

/// <summary>
/// Security secrets and credentials that should NEVER be logged.
/// </summary>
/// <remarks>
/// Examples: Passwords, API keys, connection strings, JWT access tokens, private keys.
///
/// Redaction policy:
/// - Development: Fully redacted (security best practice)
/// - Container/CI: Fully redacted (security best practice)
/// - Production: Fully redacted (security requirement)
///
/// Used for credentials that should never appear in logs in any environment.
/// Even in development, logging these creates security risks (accidental commits,
/// exposed logs, bad habits). Always redacted using ErasingRedactor.
/// </remarks>
public class SecretsAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretsAttribute"/> class.
    /// </summary>
    public SecretsAttribute() : base(DataTaxonomy.Secrets) { }
}

/// <summary>
/// Authentication credentials and security tokens.
/// </summary>
/// <remarks>
/// Examples: Refresh tokens, session tokens, bearer tokens.
/// NOTE: Access tokens (JWTs) should use [Secrets] instead.
///
/// Redaction policy:
/// - Development: Not redacted (full visibility for debugging)
/// - Container/CI: Partially redacted (first 12 characters visible)
/// - Production: Fully redacted (security credentials)
///
/// Used for tokens that enable session authentication. Unlike access tokens,
/// these are long-lived credentials that benefit from partial visibility in
/// testing environments for debugging authentication flows.
/// </remarks>
public class AuthTokenAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthTokenAttribute"/> class.
    /// </summary>
    public AuthTokenAttribute() : base(DataTaxonomy.AuthToken) { }
}

/// <summary>
/// Financial or sensitive business data.
/// </summary>
/// <remarks>
/// Examples: Transaction amounts, payee names, account balances, tenant names.
///
/// Redaction policy:
/// - Development: Not redacted (test/seed data)
/// - Container/CI: Not redacted (test/seed data)
/// - Production: Fully redacted (sensitive financial information)
///
/// Used for financial data that has no real-world value in test environments
/// but represents actual sensitive business/financial information in production.
/// Similar policy to PII but semantically different (business data vs. personal data).
/// </remarks>
public class FinancialDataAttribute : DataClassificationAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FinancialDataAttribute"/> class.
    /// </summary>
    public FinancialDataAttribute() : base(DataTaxonomy.FinancialData) { }
}
