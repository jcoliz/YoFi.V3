# Configuring NSwag to Generate ProblemDetails for All Error Responses

## Problem Statement

When using NSwag to generate TypeScript clients from ASP.NET Core APIs, error responses that don't have explicit `[ProducesResponseType]` attributes are generated as generic exceptions. This results in TypeScript code that cannot properly parse error details:

```typescript
// Generic error handling - loses error details
else if (status !== 200 && status !== 204) {
    return throwException("An unexpected server error occurred.", status, _responseText, _headers);
}
```

However, if you use ASP.NET Core's `ProblemDetails` for all error responses, you want NSwag to generate proper deserialization code:

```typescript
// Proper error handling - preserves ProblemDetails
else if (status === 400) {
    let result400 = ProblemDetails.fromJS(resultData400);
    return throwException("A server side error occurred.", status, _responseText, _headers, result400);
}
```

## Solution: Global Convention Pattern

Rather than adding `[ProducesResponseType(typeof(ProblemDetails), StatusCodes.StatusXXX)]` to every controller or endpoint, use an MVC convention to automatically add these attributes to all actions.

### Step 1: Create the Convention Class

Create a file `Conventions/ProblemDetailsConvention.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace YourNamespace.Conventions;

/// <summary>
/// Convention to automatically add ProblemDetails response types to all API endpoints
/// </summary>
/// <remarks>
/// This ensures NSwag generates proper TypeScript client code that handles all error responses
/// as ProblemDetails, eliminating generic "ApiError" responses for undeclared status codes.
/// </remarks>
public class ProblemDetailsConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var action in controller.Actions)
            {
                // Add ProblemDetails for common error status codes if not already explicitly declared
                AddResponseTypeIfMissing(action, StatusCodes.Status400BadRequest);
                AddResponseTypeIfMissing(action, StatusCodes.Status401Unauthorized);
                AddResponseTypeIfMissing(action, StatusCodes.Status403Forbidden);
                AddResponseTypeIfMissing(action, StatusCodes.Status404NotFound);
                AddResponseTypeIfMissing(action, StatusCodes.Status500InternalServerError);
            }
        }
    }

    private static void AddResponseTypeIfMissing(ActionModel action, int statusCode)
    {
        // Check if this status code already has a ProducesResponseType attribute
        var hasResponseType = action.Filters.OfType<ProducesResponseTypeAttribute>()
            .Any(f => f.StatusCode == statusCode);

        if (!hasResponseType)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode));
        }
    }
}
```

### Step 2: Register the Convention

In your `Program.cs` or `Startup.cs`, register the convention when configuring MVC:

```csharp
using YourNamespace.Conventions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    // Automatically add ProblemDetails response types to all endpoints
    options.Conventions.Add(new ProblemDetailsConvention());
});
```

### Step 3: Regenerate TypeScript Client

After implementing the convention:

1. **Build your project** to ensure the convention is compiled
2. **Run NSwag** to regenerate your TypeScript client
3. **Verify the output** - all error responses should now use `ProblemDetails.fromJS()`

## Benefits

✅ **Global Application** - Applies to all controllers and actions automatically
✅ **No Manual Decoration** - No need to add attributes to every endpoint
✅ **Proper Error Typing** - TypeScript client gets strongly-typed error responses
✅ **Maintainable** - Single location to manage error response configuration
✅ **Override-Friendly** - Explicitly declared response types are preserved
✅ **Future-Proof** - New controllers automatically inherit the behavior

## Customization

### Adding More Status Codes

To add additional status codes, modify the `Apply` method:

```csharp
public void Apply(ApplicationModel application)
{
    foreach (var controller in application.Controllers)
    {
        foreach (var action in controller.Actions)
        {
            AddResponseTypeIfMissing(action, StatusCodes.Status400BadRequest);
            AddResponseTypeIfMissing(action, StatusCodes.Status401Unauthorized);
            AddResponseTypeIfMissing(action, StatusCodes.Status403Forbidden);
            AddResponseTypeIfMissing(action, StatusCodes.Status404NotFound);
            AddResponseTypeIfMissing(action, StatusCodes.Status409Conflict);        // Added
            AddResponseTypeIfMissing(action, StatusCodes.Status422UnprocessableEntity); // Added
            AddResponseTypeIfMissing(action, StatusCodes.Status500InternalServerError);
        }
    }
}
```

### Filtering Specific Controllers

To exclude certain controllers from this convention:

```csharp
public void Apply(ApplicationModel application)
{
    foreach (var controller in application.Controllers)
    {
        // Skip controllers with a specific attribute
        if (controller.Attributes.OfType<NoProblemDetailsAttribute>().Any())
            continue;

        foreach (var action in controller.Actions)
        {
            // ... add response types
        }
    }
}
```

### Controller-Specific Status Codes

To apply different status codes based on controller type:

```csharp
private static void AddResponseTypesForController(ControllerModel controller)
{
    var statusCodes = controller.ControllerName switch
    {
        "Identity" => new[] { 400, 401, 500 },
        "Items" => new[] { 400, 401, 403, 404, 500 },
        _ => new[] { 400, 401, 500 }
    };

    foreach (var action in controller.Actions)
    {
        foreach (var statusCode in statusCodes)
        {
            AddResponseTypeIfMissing(action, statusCode);
        }
    }
}
```

## Alternative Approaches

### Option 1: Controller-Level Attributes

Apply attributes at the controller level instead of using a convention:

```csharp
[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class MyController : ControllerBase
{
    // All actions inherit these response types
}
```

**Pros:** Explicit and visible
**Cons:** Must be added to every controller

### Option 2: NSwag Operation Processor

Create a custom NSwag operation processor (more complex):

```csharp
public class ProblemDetailsOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        context.OperationDescription.Operation.Responses.TryAdd("400",
            new OpenApiResponse { Schema = JsonSchema.FromType<ProblemDetails>() });
        // Add other status codes...
        return true;
    }
}
```

Then register in `nswag.json` or programmatically.

**Pros:** Works at OpenAPI document level
**Cons:** More complex, requires NSwag-specific code

## Verification

After implementation, verify the generated TypeScript client contains code like:

```typescript
protected processYourMethod(response: Response): Promise<YourReturnType> {
    const status = response.status;
    let _headers: any = {};
    if (response.headers && response.headers.forEach) {
        response.headers.forEach((v: any, k: any) => _headers[k] = v);
    };

    if (status === 200) {
        // Success handling...
    } else if (status === 400) {
        return response.text().then((_responseText) => {
            let result400: any = null;
            let resultData400 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver);
            result400 = ProblemDetails.fromJS(resultData400); // ✅ Proper deserialization
            return throwException("A server side error occurred.", status, _responseText, _headers, result400);
        });
    } else if (status === 401) {
        return response.text().then((_responseText) => {
            let result401: any = null;
            let resultData401 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver);
            result401 = ProblemDetails.fromJS(resultData401); // ✅ Proper deserialization
            return throwException("A server side error occurred.", status, _responseText, _headers, result401);
        });
    }
    // ... other status codes
}
```

## Troubleshooting

### Convention Not Applied

- Ensure the convention class is in a namespace accessible to `Program.cs`
- Verify the project builds successfully after adding the convention
- Check that `AddControllers()` is called before the convention is added

### TypeScript Client Still Has Generic Errors

- Rebuild the ASP.NET Core project
- Delete the old TypeScript client file
- Regenerate using NSwag
- Check the OpenAPI/Swagger JSON to verify response types are present

### Some Endpoints Missing ProblemDetails

- The convention only adds response types that aren't explicitly declared
- Check if the action has conflicting attributes
- Verify the action is part of a controller (not a minimal API endpoint)

## References

- [ASP.NET Core MVC Application Model](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/application-model)
- [ProblemDetails in ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.problemdetails)
- [NSwag Documentation](https://github.com/RicoSuter/NSwag/wiki)
