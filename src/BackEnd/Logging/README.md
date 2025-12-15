# Custom Console Logger

This directory contains a custom console logger implementation that formats log output to mimic systemd scoped logs.

## Components

### [`CustomConsoleLogger.cs`](CustomConsoleLogger.cs)
The main logger implementation that formats log entries with:
- **Syslog priority codes** (RFC 5424): `<6>` for Info, `<7>` for Debug, `<4>` for Warning, `<3>` for Error, etc.
- **Timestamp**: Configurable format (default: `MM-dd'T'HH:mm:ss`)
- **Category name**: The logger category (e.g., `Microsoft.AspNetCore.Hosting.Diagnostics`)
- **Event ID**: Optional event ID in brackets `[1]`
- **Scopes**: Captured scope information (Activity, Request context, etc.) in the format `=> Scope1 => Scope2`
- **Message**: The log message
- **Exception**: Full exception details if present

### [`CustomConsoleLoggerOptions.cs`](CustomConsoleLoggerOptions.cs)
Configuration options:
- `TimestampFormat`: Format string for timestamps (default: `"MM-dd'T'HH:mm:ss "`)
- `UseUtcTimestamp`: Whether to use UTC time (default: `false`)
- `IncludeScopes`: Whether to capture and display scope information (default: `true`)

### [`CustomConsoleLoggerScope.cs`](CustomConsoleLoggerScope.cs)
Manages logging scope state using `AsyncLocal<T>` to maintain scope context across async/await boundaries. This ensures scope information (like Activity traces, request IDs) is correctly associated with log entries.

### [`CustomConsoleLoggerProvider.cs`](CustomConsoleLoggerProvider.cs)
Logger provider that creates and manages logger instances. Uses a concurrent dictionary to cache loggers by category name.

### [`LoggingBuilderExtensions.cs`](LoggingBuilderExtensions.cs)
Extension methods for easy integration with ASP.NET Core logging:
```csharp
builder.Logging.AddCustomConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "MM-dd'T'HH:mm:ss ";
    options.UseUtcTimestamp = false;
});
```

## Output Format

Example log output:
```
<6>12-14T19:35:54 Microsoft.AspNetCore.Hosting.Diagnostics[1] => SpanId:14e8b00574fce3ea, TraceId:24650c62eedb8850aaaa225d5ceb27a5, ParentId:0000000000000000 => ConnectionId:0HNHRHB0K6L1U => RequestPath:/api/auth/logout RequestId:0HNHRHB0K6L1U:00000003 Request starting HTTP/1.1 OPTIONS http://localhost:5379/api/auth/logout - - -
<6>12-14T19:35:54 YoFi.V3.Controllers.AuthController[647297223] => SpanId:650388030a461946, TraceId:b1ed177019e4f9cc264cf1fcd200fd3d, ParentId:20521463c7825157 => ConnectionId:0HNHRHB0K6L1V => RequestPath:/api/auth/logout RequestId:0HNHRHB0K6L1V:00000003 => System.Collections.Generic.Dictionary`2[System.String,System.Object] => YoFi.V3.Controllers.AuthController.Logout (YoFi.V3.Controllers) Logout requested
```

## Usage

The custom console logger is registered in [`Program.cs`](../Program.cs):

```csharp
using YoFi.V3.BackEnd.Logging;

// Startup logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddCustomConsole(options =>
    {
        options.IncludeScopes = true;
#if DEBUG
        options.TimestampFormat = "MM-dd'T'HH:mm:ss ";
        options.UseUtcTimestamp = false;
#endif
   });
});

// Application logger
builder.Logging.AddCustomConsole(options =>
{
    options.IncludeScopes = true;
#if DEBUG
    options.TimestampFormat = "MM-dd'T'HH:mm:ss ";
    options.UseUtcTimestamp = false;
#endif
});
```

## Syslog Priority Mapping

The logger maps .NET `LogLevel` to syslog priority codes:

| .NET LogLevel | Syslog Priority | Code |
|---------------|----------------|------|
| Trace         | Debug          | 7    |
| Debug         | Debug          | 7    |
| Information   | Informational  | 6    |
| Warning       | Warning        | 4    |
| Error         | Error          | 3    |
| Critical      | Critical       | 2    |

## Design Notes

- **AsyncLocal scope storage**: Ensures scope information follows async execution flow
- **Immutable scope stack**: Uses `ImmutableStack<T>` for thread-safe scope management
- **Performance**: Uses `StringBuilder` for efficient string concatenation and `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for hot path methods
- **Provider alias**: Registered as `"CustomConsole"` for configuration purposes
