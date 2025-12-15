# XML Documentation Setup Guide

This guide explains how to use XML documentation comments to automatically generate markdown documentation.

## Why XML Documentation?

**Advantages over parsing source code:**
- ✅ **Built-in C# feature** - No custom parsing needed
- ✅ **IDE support** - IntelliSense shows your documentation
- ✅ **Compiler validated** - Warnings for missing or incorrect docs
- ✅ **Industry standard** - Works with many documentation tools
- ✅ **Rich metadata** - Parameters, returns, exceptions, examples
- ✅ **Easy to maintain** - Documentation lives with the code

**Comparison:**

| Approach | Pros | Cons |
|----------|------|------|
| **Parse source code** | No build step needed | Fragile regex parsing, no validation |
| **XML documentation** | Validated by compiler, standard format | Requires build step, extra file |

## Setup Steps

### 1. Enable XML Documentation in Project

Edit `YoFi.V3.Tests.Functional.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)\local.runsettings</RunSettingsFilePath>

    <!-- Add this line to enable XML documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>

    <!-- Optional: Suppress warnings for missing XML docs -->
    <!-- <NoWarn>$(NoWarn);CS1591</NoWarn> -->
  </PropertyGroup>

  <!-- ... rest of file ... -->
</Project>
```

### 2. Build the Project

```powershell
dotnet build
```

This generates: `bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml`

### 3. Generate Documentation

```powershell
# For API client (auto-generated code - no XML docs)
.\.roo\scripts\generate-api-docs.ps1

# For your own code with XML docs
.\.roo\scripts\generate-docs-from-xml.ps1 `
    -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" `
    -OutputFile "Steps/STEPS-REFERENCE.md" `
    -NamespaceFilter "YoFi.V3.Tests.Functional.Steps"
```

## Writing XML Documentation

### Basic Class Documentation

```csharp
/// <summary>
/// Provides step definitions for workspace tenancy scenarios.
/// </summary>
/// <remarks>
/// These steps handle multi-tenant workspace operations including
/// workspace creation, user assignment, and role management.
/// </remarks>
public class WorkspaceTenancySteps : FunctionalTest
{
    // ...
}
```

### Method Documentation

```csharp
/// <summary>
/// Creates multiple workspaces with the specified configuration.
/// </summary>
/// <param name="table">DataTable containing workspace configurations with Name, Description, and Role columns</param>
/// <remarks>
/// Each workspace is created for the current test user with the specified role.
/// Workspaces are automatically cleaned up after the test.
/// </remarks>
protected async Task GivenIHaveWorkspacesAsync(DataTable table)
{
    // Implementation
}
```

### Parameter Documentation

```csharp
/// <summary>
/// Assigns a user to a workspace with a specific role.
/// </summary>
/// <param name="username">The username of the user to assign</param>
/// <param name="workspaceName">The name of the workspace</param>
/// <param name="role">The role to assign (Viewer, Editor, or Owner)</param>
/// <exception cref="ArgumentException">Thrown when workspace or user not found</exception>
protected async Task AssignUserToWorkspace(string username, string workspaceName, string role)
{
    // Implementation
}
```

### Complex Documentation

```csharp
/// <summary>
/// Seeds transactions into a workspace for testing purposes.
/// </summary>
/// <param name="workspaceName">The target workspace name</param>
/// <param name="count">Number of transactions to create</param>
/// <returns>A task representing the asynchronous operation</returns>
/// <remarks>
/// <para>
/// This method generates random transactions with the following characteristics:
/// </para>
/// <list type="bullet">
/// <item>Random dates within the past year</item>
/// <item>Random amounts between -$1000 and $1000</item>
/// <item>Sequential payee names (Payee-1, Payee-2, etc.)</item>
/// </list>
/// <para>
/// Use this for performance testing or data visualization scenarios.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// await SeedTransactionsAsync("My Workspace", 100);
/// </code>
/// </example>
protected async Task SeedTransactionsAsync(string workspaceName, int count)
{
    // Implementation
}
```

## Available XML Tags

| Tag | Purpose | Example |
|-----|---------|---------|
| `<summary>` | Brief description | `<summary>Creates a workspace</summary>` |
| `<remarks>` | Detailed explanation | `<remarks>This is the preferred way to...</remarks>` |
| `<param>` | Parameter description | `<param name="id">The workspace ID</param>` |
| `<returns>` | Return value description | `<returns>The created workspace</returns>` |
| `<exception>` | Exception documentation | `<exception cref="ArgumentNullException">...</exception>` |
| `<example>` | Usage example | `<example><code>var x = new Example();</code></example>` |
| `<see>` | Reference to type | `<see cref="WorkspaceDto"/>` |
| `<seealso>` | Related reference | `<seealso cref="CreateWorkspaceAsync"/>` |
| `<para>` | Paragraph in remarks | `<para>First paragraph</para>` |
| `<list>` | List in remarks | `<list type="bullet"><item>...</item></list>` |
| `<code>` | Code example | `<code>var result = Method();</code>` |

## Use Cases

### 1. Document Step Definitions

```powershell
# Generate documentation for all step files
.\.roo\scripts\generate-docs-from-xml.ps1 `
    -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" `
    -OutputFile "Steps/STEPS-REFERENCE.md" `
    -NamespaceFilter "YoFi.V3.Tests.Functional.Steps"
```

### 2. Document Page Objects

```powershell
# Generate documentation for page objects
.\.roo\scripts\generate-docs-from-xml.ps1 `
    -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" `
    -OutputFile "Pages/PAGES-REFERENCE.md" `
    -NamespaceFilter "YoFi.V3.Tests.Functional.Pages"
```

### 3. Document Helpers

```powershell
# Generate documentation for helper classes
.\.roo\scripts\generate-docs-from-xml.ps1 `
    -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" `
    -OutputFile "Helpers/HELPERS-REFERENCE.md" `
    -NamespaceFilter "YoFi.V3.Tests.Functional.Helpers"
```

## Integration with Workflow

### Option 1: Manual (Recommended for now)

```powershell
# When you add/update XML comments
dotnet build
.\.roo\scripts\generate-docs-from-xml.ps1 -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" -OutputFile "OUTPUT.md"
```

### Option 2: Post-Build Event

Add to `.csproj`:

```xml
<Target Name="GenerateDocs" AfterTargets="Build">
  <Exec Command="powershell -File $(ProjectDir).roo\scripts\generate-docs-from-xml.ps1 -XmlDocFile '$(OutputPath)$(AssemblyName).xml' -OutputFile '$(ProjectDir)Steps\STEPS-REFERENCE.md' -NamespaceFilter 'YoFi.V3.Tests.Functional.Steps'" />
</Target>
```

### Option 3: CI/CD Validation

```yaml
- name: Validate Documentation
  run: |
    dotnet build
    .\.roo\scripts\generate-docs-from-xml.ps1 ...
    git diff --exit-code || echo "::warning::Docs need regeneration"
```

## Best Practices

1. **Start simple** - Begin with just `<summary>` tags
2. **Document public APIs** - Focus on classes/methods others will use
3. **Use consistent style** - Follow team conventions
4. **Include examples** - Show how to use complex methods
5. **Update with code** - Keep docs in sync with implementation
6. **Review in IDE** - IntelliSense shows your documentation immediately

## Limitations

### When XML Documentation Works Well
- ✅ Your own code (Steps, Pages, Helpers)
- ✅ Code you control and maintain
- ✅ Code that benefits from IntelliSense

### When to Use Source Parsing Instead
- ❌ Auto-generated code (like `ApiClient.cs` from NSwag)
- ❌ External libraries you don't control
- ❌ Code without XML comments that you can't modify

**Solution**: Use both approaches:
- `generate-api-docs.ps1` for auto-generated API clients
- `generate-docs-from-xml.ps1` for your own code with XML comments

## Example Workflow

1. **Enable XML docs** in project file (one-time setup)
2. **Write code** with XML comments as you go
3. **Build project** to generate XML file
4. **Run script** to generate markdown
5. **Commit** both code and generated docs
6. **Review** documentation in pull requests

This gives you IDE support while coding AND beautiful markdown for documentation!
