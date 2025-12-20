# Test-Log Correlation

## Overview

Functional tests generate correlation headers that connect browser actions to backend API logs, making it easy to trace what the backend did in response to test actions. This is essential for debugging test failures and understanding system behavior.

## Implementation

The project uses a hybrid approach combining W3C Trace Context with custom test metadata headers.

**Test Side:** [`FunctionalTestBase`](Infrastructure/FunctionalTestBase.cs) creates an Activity for each test and sets correlation headers

**Server Side:** [`TestCorrelationMiddleware`](../../src/Controllers/Middleware/TestCorrelationMiddleware.cs) extracts correlation data and adds it to logging scopes

## How It Works

### Test Initialization

When each test starts, [`FunctionalTestBase.SetUp()`](Infrastructure/FunctionalTestBase.cs:91-137) creates a distributed tracing Activity:

```csharp
_testActivity = new Activity("FunctionalTest");
_testActivity.SetTag("test.name", testName);
_testActivity.SetTag("test.class", testClass);
_testActivity.SetTag("test.id", testId);
_testActivity.Start();
```

The Activity generates a unique TraceId and SpanId used to link all operations in the test.

### Header Propagation

Two sets of headers are attached to all browser requests:

**W3C Trace Context Headers:**
- `traceparent: 00-{TraceId}-{SpanId}-01` - Standard distributed tracing header

**Custom Test Metadata:**
- `X-Test-Name` - Name of the test method (URL-encoded)
- `X-Test-Id` - Unique GUID for this test execution
- `X-Test-Class` - Fully-qualified test class name

These headers are set via `Context.SetExtraHTTPHeadersAsync()` and are automatically included in all HTTP requests made by Playwright.

### Backend Processing

[`TestCorrelationMiddleware`](../../src/Controllers/Middleware/TestCorrelationMiddleware.cs) runs early in the request pipeline and extracts test correlation data from headers or cookies (fallback for frontend-initiated requests).

The middleware adds test metadata to both:
1. **Activity Tags** - For distributed tracing and telemetry systems
2. **Logging Scope** - For structured logging with test context

This makes test information available to:
- All log messages during the request
- Application Insights telemetry
- Aspire Dashboard traces
- Custom log formatters

## What Gets Correlated

### Playwright-Controlled Requests ✅

All HTTP requests initiated directly by Playwright test code are fully correlated:
- Login requests
- Page navigations
- API calls via Test Control Client
- Form submissions

These requests include all headers and link to the test's TraceId.

### Frontend-Initiated Requests ⚠️

HTTP requests initiated by the Vue/Nuxt frontend (e.g., automatic token refresh) are partially correlated:
- **Correlation via cookie** - Test metadata is available in logs
- **Activity tags set** - Can filter by test name in Aspire Dashboard
- **Separate TraceId** - NOT linked to test's trace (different trace tree)

This is because:
- Cookies are automatically sent by the browser
- Custom headers are NOT automatically sent
- ASP.NET Core starts a new Activity when `traceparent` header is missing

## Log Output Example

Console logs during functional tests include test context:

```
INF 12/19 17:23:45 [1003] trace:b4982c97 /UserViewsTheirAccountDetails/ Auth.Login: OK
INF 12/19 17:23:45 [2004] trace:b4982c97 /UserViewsTheirAccountDetails/ Transactions.Get: OK 15
```

The format shows:
- **trace:b4982c97** - First 8 chars of TraceId for visual grouping
- **/UserViewsTheirAccountDetails/** - Test name for quick identification
- **[1003]** - Log event ID
- **Auth.Login** - Simplified controller/method category

## Filtering in Aspire Dashboard

Use Activity tags to find all traces from a test:

**Filter by test name:**
```
test.name = "UserViewsTheirAccountDetails"
```

**Filter by test ID:**
```
test.id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

**Filter by test class:**
```
test.class = "YoFi.V3.Tests.Functional.Tests.Tenancy_feature"
```

## Correlation Flow

```
Test Start
    ↓
Create Activity → Generate TraceId/SpanId
    ↓
Set Headers → traceparent + X-Test-* headers
    ↓
Browser Request → Headers sent automatically
    ↓
ASP.NET Core → Receives request with traceparent
    ↓
Activity.Current → Links to test's TraceId
    ↓
TestCorrelationMiddleware → Extracts test metadata
    ↓
Add to Activity Tags → For telemetry
    ↓
Add to Logging Scope → For structured logs
    ↓
Controller Logs → Include test context
    ↓
Log Output → Shows test name and TraceId
```

## Known Limitation: Frontend Request Traces

Frontend-initiated requests (like token refresh) appear as separate traces because:

1. Frontend makes HTTP request using `$fetch`
2. Cookies are sent (metadata available ✅)
3. Custom headers are NOT sent (traceparent missing ❌)
4. ASP.NET Core creates new Activity with new TraceId
5. Middleware adds test metadata to new Activity
6. Request appears in logs and Dashboard but NOT linked to test trace

**Workaround:** Filter by test name or test ID to find all related requests across multiple traces.

**Future Solution:** Modify frontend HTTP client to read test metadata from cookie and add headers to outgoing requests. This would require changes to [`src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts`](../../src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts).

## Files

- **Test Base:** [`tests/Functional/Infrastructure/FunctionalTestBase.cs`](Infrastructure/FunctionalTestBase.cs) - Creates Activities and sets headers
- **Middleware:** [`src/Controllers/Middleware/TestCorrelationMiddleware.cs`](../../src/Controllers/Middleware/TestCorrelationMiddleware.cs) - Extracts correlation data
- **Registration:** Middleware is registered in `Program.cs` via `app.UseMiddleware<TestCorrelationMiddleware>()`

## Benefits

**Debugging Test Failures:**
- See exactly what the backend did when a test failed
- Trace requests through the entire system
- Identify which logs belong to which test

**Performance Analysis:**
- Track request durations per test
- Identify slow operations
- Compare performance across test runs

**System Understanding:**
- See how components interact during tests
- Verify expected behavior in logs
- Understand request flow through layers

**Production Smoke Tests:**
- Same correlation mechanism works in deployed environments
- Filter production logs by test name to isolate test traffic
- No impact on non-test requests
