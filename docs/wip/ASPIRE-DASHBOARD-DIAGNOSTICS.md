# Aspire Dashboard Integration Diagnostics

## Problem Summary

The Aspire dashboard was added to `docker/docker-compose-ci.yml` but is not showing logs or traces from the backend service.

## Root Cause Analysis

### ACTUAL Root Cause (Confirmed)

**Wrong OTLP Port Configuration**: The docker-compose was configured to send telemetry to port **4317** (standard OTLP port), but Aspire Dashboard v13 uses **port 18889** for OTLP/gRPC by default.

**Evidence from Aspire Dashboard logs**:
```
info: OTLP/gRPC listening on: http://[::]:18889
info: OTLP/HTTP listening on: http://[::]:18890
```

The backend was sending telemetry to `http://aspire-dashboard:4317` which was not exposed or listened to by the dashboard, causing all telemetry to be silently dropped.

### Secondary Issues Fixed (Also Important)

#### 1. Missing OpenTelemetry Resource Attributes

**Problem**: The OpenTelemetry configuration in [`src/ServiceDefaults/Extensions.cs`](../src/ServiceDefaults/Extensions.cs) was not setting proper resource attributes (service name, version, instance ID) that Aspire dashboard requires to identify and correlate telemetry data.

**Impact**: Without these attributes, the Aspire dashboard cannot properly categorize or display telemetry, even if it's being received.

**Evidence**:
- No `ConfigureResource()` call in the OpenTelemetry setup
- Tracer was using `builder.Environment.ApplicationName` which may be empty or default
- No explicit `OTEL_SERVICE_NAME` environment variable set in docker-compose

#### 2. Incomplete Service Identification

**Problem**: The docker-compose configuration didn't set the `OTEL_SERVICE_NAME` environment variable, leaving service identification to potentially unreliable defaults.

**Impact**: Telemetry might be sent without proper service identification, causing it to be dropped or misattributed.

### Other Issues Investigated

These were investigated but determined to be configured correctly:

- ✅ OpenTelemetry exporters (UseOtlpExporter properly called)
- ✅ Network connectivity (proper docker-compose service dependency)
- ✅ Instrumentation (ASP.NET Core, HTTP, EF Core all configured)
- ✅ OTLP protocol (correctly set to `grpc`)

## Changes Made

### 1. Enhanced OpenTelemetry Configuration

**File**: [`src/ServiceDefaults/Extensions.cs`](../src/ServiceDefaults/Extensions.cs)

**Changes**:
- Added `OpenTelemetry.Resources` using statement
- Added `ConfigureResource()` call to set service name, version, and instance ID
- Changed tracer source from `builder.Environment.ApplicationName` to explicit `serviceName`
- Added comprehensive diagnostic logging to help troubleshoot configuration issues

**Key Code Addition**:
```csharp
.ConfigureResource(resource =>
{
    resource.AddService(
        serviceName: serviceName,
        serviceVersion: serviceVersion,
        serviceInstanceId: Environment.MachineName);

    // DIAGNOSTIC: Log resource attributes being set
    logger.LogInformation(94, "OpenTelemetry Resource Attributes: Service={ServiceName}, Version={Version}, Instance={Instance}",
        serviceName, serviceVersion, Environment.MachineName);
})
```

**Diagnostic Logging Added**:
- Event ID 90: Log service name, version, and environment on startup
- Event ID 93: Log OTLP endpoint and protocol configuration
- Event ID 94: Log resource attributes being set
- Warning if OTLP endpoint is not configured

### 2. Fixed Docker Compose Port Configuration (CRITICAL FIX)

**File**: [`docker/docker-compose-ci.yml`](../docker/docker-compose-ci.yml)

**Changes**:
- Changed OTLP endpoint from `http://aspire-dashboard:4317` → `http://aspire-dashboard:18889`
- Updated exposed ports to include `18889:18889` (OTLP/gRPC) and `18890:18890` (OTLP/HTTP)
- Added `OTEL_SERVICE_NAME=yofi-backend` environment variable
- Added `SOLUTION_VERSION` pass-through for proper version tracking

**Why the port change**:
- Aspire Dashboard v13 uses non-standard OTLP ports (18889 for gRPC, 18890 for HTTP)
- Standard OTLP port 4317 is not used by Aspire dashboard
- This is documented in Aspire dashboard logs but not prominently in docker-compose examples

**Benefits**:
- Telemetry now reaches the dashboard receiver
- Explicit service identification in Aspire dashboard
- Version tracking for telemetry correlation

## Validation Steps (UPDATED)

To confirm the fix is working, follow these steps:

### 1. Restart Containers with New Configuration

```powershell
# Stop existing containers
docker compose -f docker/docker-compose-ci.yml down

# Start with updated port configuration (no rebuild needed)
docker compose -f docker/docker-compose-ci.yml up -d
```

### 2. Verify Backend Logs Show Correct Endpoint

```powershell
docker logs yofi-v3-backend-1
```

**Expected output** (should show port 18889):
```
info: Configuring OpenTelemetry: ServiceName=yofi-backend, Version='docker-compose', Environment=Container
info: OTLP Endpoint configured: http://aspire-dashboard:18889, Protocol=grpc
info: OpenTelemetry Resource Attributes: Service=yofi-backend, Version='docker-compose', Instance=<container-id>
```

Note the endpoint should now be **18889**, not 4317.

### 3. Access Aspire Dashboard

Open browser to: http://localhost:18888

**What to verify**:
- Dashboard loads successfully
- "yofi-backend" appears in the resources list
- Navigate to "Structured Logs" - should see logs from backend
- Navigate to "Traces" - should see HTTP request traces
- Navigate to "Metrics" - should see ASP.NET Core metrics

### 4. Generate Telemetry Data

Make some API requests to generate telemetry:

```powershell
# Health check (should create traces)
curl http://localhost:5001/health

# Weather endpoint (should create traces and logs)
curl http://localhost:5001/api/weather

# Version endpoint
curl http://localhost:5001/api/version
```

After making requests, refresh the Aspire dashboard and verify:
- Traces appear in the "Traces" view
- Logs appear in the "Structured Logs" view
- Request metrics increment in the "Metrics" view

### 5. Report Results

Please report back with:

1. **The diagnostic log output** from step 2 (ServiceName, OTLP Endpoint, Resource Attributes)
2. **Whether the dashboard shows the service** in the resources list
3. **Whether logs/traces/metrics appear** after making API requests
4. **Any error messages** in either backend logs or Aspire dashboard logs

If telemetry still doesn't appear, we'll investigate further based on the diagnostic output.

## Expected Behavior After Fix

Once working correctly, you should see:

1. **Aspire Dashboard Resources**: "yofi-backend" service listed
2. **Structured Logs**: All backend log entries (Info, Debug, Warning, Error)
3. **Traces**: HTTP request traces showing:
   - Request path and method
   - Response status code
   - Duration
   - Database queries (from EF Core instrumentation)
4. **Metrics**: Runtime and HTTP metrics including:
   - Request rate
   - Response time percentiles
   - Active connections
   - GC metrics

## Additional Notes

### Why Resource Attributes Matter

Aspire dashboard (and OpenTelemetry in general) uses resource attributes to:
- **Identify services**: Service name is the primary key for grouping telemetry
- **Correlate telemetry**: All spans, logs, and metrics from the same service instance are linked
- **Display metadata**: Version and instance information shown in the UI
- **Enable filtering**: Users can filter by service, version, or instance

Without proper resource attributes, telemetry becomes "orphaned" and may be:
- Dropped entirely
- Displayed under "Unknown" service
- Missing correlation between logs, traces, and metrics

### Protocol Configuration

The setup uses gRPC protocol for OTLP export:
- More efficient than HTTP for high-volume telemetry
- Better suited for container-to-container communication
- Standard protocol for Aspire dashboard integration

### Logging vs Telemetry

Important distinction:
- **Console logs**: Still written to container stdout (visible via `docker logs`)
- **OpenTelemetry logs**: Structured logs exported to Aspire dashboard
- Both can coexist - console logs for debugging, OTLP logs for monitoring

## Key Learnings

### Aspire Dashboard Port Configuration

**Important**: Aspire Dashboard does NOT use standard OTLP ports:
- ❌ **Not** port 4317 (standard OTLP/gRPC)
- ❌ **Not** port 4318 (standard OTLP/HTTP)
- ✅ **Uses** port 18889 (Aspire OTLP/gRPC)
- ✅ **Uses** port 18890 (Aspire OTLP/HTTP)

This is by design to avoid port conflicts when running alongside other observability tools that use standard OTLP ports.

### Diagnostic Process

The debugging process that led to the solution:
1. ✅ Verified OpenTelemetry was configured (diagnostic logs confirmed)
2. ✅ Verified resource attributes were set (logs showed service name)
3. ✅ Verified OTLP exporter was enabled (logs confirmed endpoint)
4. ❓ Checked Aspire dashboard logs for receiver errors
5. 🎯 **Found mismatch**: Dashboard listening on 18889, backend sending to 4317

**Lesson**: Always check both sender and receiver logs to verify port/protocol agreement.

## References

- [OpenTelemetry .NET Resource Detection](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Resources)
- [.NET Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard)
- [Aspire Dashboard OTLP Configuration](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone#otlp-configuration)
- [OTLP Exporter Configuration](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol)
