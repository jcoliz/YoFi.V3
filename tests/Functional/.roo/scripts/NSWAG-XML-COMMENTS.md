# Adding XML Comments to NSwag Generated Code

NSwag can generate XML documentation comments from your OpenAPI/Swagger descriptions. Here's how:

## Option 1: Enable Built-in XML Comment Generation (Simplest)

Unfortunately, NSwag doesn't have a built-in flag to generate XML comments directly. However, your OpenAPI spec already contains descriptions that NSwag uses internally.

## Option 2: Custom Liquid Template (Recommended)

NSwag uses Liquid templates for code generation. You can customize the template to include XML comments.

### Step 1: Create Custom Template Directory

```powershell
mkdir .nswag
mkdir .nswag/templates
```

### Step 2: Extract Default Template

The default CSharp client template is embedded in NSwag. You need to create a modified version.

**Create `.nswag/templates/CSharpClient.liquid`:**

Key changes to add XML comments:
1. Add `<summary>` tags from OpenAPI descriptions
2. Add `<param>` tags for parameters
3. Add `<returns>` tags for return values
4. Add `<exception>` tags for error responses

### Step 3: Update nswag.json

Modify your `nswag.json` to use the custom template:

```json
{
  "codeGenerators": {
    "openApiToCSharpClient": {
      // ... existing settings ...
      "templateDirectory": ".nswag/templates",
      // ... rest of settings ...
    }
  }
}
```

## Option 3: Post-Processing Script (Easier Alternative)

Instead of modifying NSwag templates, add XML comments after generation.

**Create `.roo/scripts/add-xml-comments.ps1`:**

```powershell
param(
    [string]$ApiClientFile = "Api/ApiClient.cs",
    [string]$OpenApiSpec = "../../src/WireApiHost/swagger.json"
)

# Read the swagger spec
$swagger = Get-Content $OpenApiSpec | ConvertFrom-Json

# Read the generated client
$content = Get-Content $ApiClientFile -Raw

# For each endpoint in swagger, add XML comments before the method
foreach ($path in $swagger.paths.PSObject.Properties) {
    foreach ($method in $path.Value.PSObject.Properties) {
        $operation = $method.Value

        if ($operation.summary) {
            # Find the method in generated code and add /// <summary>
            # This is complex - see full implementation below
        }
    }
}

# Write modified content back
$content | Out-File $ApiClientFile -Encoding UTF8
```

## Option 4: Use Source Comments (Simplest for Your Case)

Since your API is defined in C# controllers with XML comments, ensure those are included in the OpenAPI generation.

**In your API project (WireApiHost), ensure XML docs are enabled:**

```xml
<!-- In YoFi.V3.WireApiHost.csproj -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <IncludeXmlComments>true</IncludeXmlComments>
</PropertyGroup>
```

**In your Swagger configuration (Program.cs or Startup.cs):**

```csharp
builder.Services.AddSwaggerGen(options =>
{
    // Include XML comments from the API project
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // Optional: Include comments from model libraries too
    // options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "YoFi.V3.Core.xml"));
});
```

This makes NSwag include descriptions from your API's XML comments in the OpenAPI spec, which NSwag then uses for:
- Operation descriptions (becomes comments in generated code)
- Parameter descriptions
- Response descriptions

## Recommended Approach

**For your situation, I recommend Option 4:**

1. **Add XML comments to your API controllers**:
   ```csharp
   /// <summary>
   /// Retrieves all workspaces for the current user
   /// </summary>
   /// <returns>Collection of workspace DTOs with role information</returns>
   [HttpGet("api/Tenant")]
   public async Task<ICollection<TenantRoleResultDto>> GetTenants()
   ```

2. **Enable XML doc in WireApiHost project**:
   ```xml
   <GenerateDocumentationFile>true</GenerateDocumentationFile>
   ```

3. **Configure Swagger to include XML comments** (shown above)

4. **Regenerate NSwag client**:
   ```powershell
   nswag run nswag.json
   ```

5. **Result**: Generated code includes comments from your API
   - Interface methods get `<summary>` tags
   - Parameters get `<param>` tags
   - Return types are documented

## Verification

After setup, check if NSwag is picking up the descriptions:

1. **Check OpenAPI spec** (swagger.json):
   ```json
   {
     "paths": {
       "/api/Tenant": {
         "get": {
           "summary": "Retrieves all workspaces for the current user",
           "description": "Returns a collection of workspaces...",
           ...
         }
       }
     }
   }
   ```

2. **Check generated ApiClient.cs**:
   ```csharp
   /// <summary>
   /// Retrieves all workspaces for the current user
   /// </summary>
   public virtual async Task<ICollection<TenantRoleResultDto>> GetTenantsAsync()
   ```

If descriptions appear in swagger.json but not in ApiClient.cs, NSwag isn't configured to output them (would need custom template).

## Current Limitations

NSwag's C# client generator **does** include some documentation:
- Operation summaries become `<summary>` on interface methods
- Parameter descriptions become `<param>` tags

But it **doesn't** include:
- Detailed `<remarks>` sections
- `<example>` sections
- `<returns>` detailed descriptions

For complete XML documentation, you'd need a custom Liquid template or post-processing script.

## Quick Test

To see what NSwag currently generates, check your `ApiClient.cs`:

```csharp
// Look for this pattern - NSwag may already be adding some comments:
/// <exception cref="ApiException">A server side error occurred.</exception>
public virtual System.Threading.Tasks.Task<LoginResponse> LoginAsync(LoginRequest request)
```

If you see `<exception>` tags, NSwag is generating some XML docs. You can enhance this by:
1. Adding better descriptions in your API
2. Customizing the NSwag template to include more XML tags

## Bottom Line

**Best approach for your codebase:**
1. ✅ Add XML comments to your **API controllers** (source of truth)
2. ✅ Configure **Swagger to include XML comments**
3. ✅ Regenerate with **NSwag** (picks up descriptions automatically)
4. ✅ Build functional tests with **GenerateDocumentationFile** enabled
5. ✅ **Roo reads both XML files** - API docs from NSwag, test docs from compiler

This gives you full documentation without custom scripts or templates!
