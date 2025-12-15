# Documentation Generation Scripts

This directory contains scripts to automatically generate documentation from source code.

## Available Scripts

### generate-api-docs.ps1

Generates markdown documentation from the NSwag-generated `ApiClient.cs` file.

**Usage:**

```powershell
# From the project root (tests/Functional)
.\.roo\scripts\generate-api-docs.ps1

# With custom paths
.\.roo\scripts\generate-api-docs.ps1 -SourceFile "path/to/ApiClient.cs" -OutputFile "path/to/output.md"
```

**What it does:**
- Extracts all API client interfaces (IAuthClient, ITestControlClient, etc.)
- Documents method signatures and HTTP endpoints
- Catalogs all data models (request/response DTOs, enums)
- Organizes models by category (Authentication, Test Control, etc.)
- Includes usage examples and common patterns

**When to run:**
- After regenerating `ApiClient.cs` with NSwag
- When the API specification changes
- To keep documentation in sync with code

**Output:**
- Creates/updates `Api/API-CLIENT-REFERENCE.md`

## Creating New Documentation Scripts

To create a similar script for other code files:

1. **Copy the template structure** from `generate-api-docs.ps1`
2. **Adjust the parsing logic** for your specific code patterns
3. **Define categorization** for organizing extracted content
4. **Create markdown sections** appropriate to your documentation needs
5. **Add usage examples** relevant to the documented code

### Script Template Pattern

```powershell
param(
    [string]$SourceFile = "path/to/source.cs",
    [string]$OutputFile = "path/to/output.md"
)

$ErrorActionPreference = "Stop"

# 1. Read source file
$content = Get-Content $SourceFile -Raw

# 2. Parse content (extract classes, methods, etc.)
# ... your parsing logic ...

# 3. Generate markdown
$markdown = @"
# Documentation Title

Your content here...
"@

# 4. Write output
$markdown | Out-File -FilePath $OutputFile -Encoding UTF8 -NoNewline
```

### Common Parsing Patterns

**Extract classes:**
```powershell
if ($line -match 'public (partial )?class (\w+)') {
    $className = $Matches[2]
}
```

**Extract methods:**
```powershell
if ($line -match 'public\s+\w+\s+(\w+)\(([^)]*)\)') {
    $methodName = $Matches[1]
    $parameters = $Matches[2]
}
```

**Extract properties:**
```powershell
if ($line -match 'public\s+([^{]+?)\s+(\w+)\s*\{\s*get;') {
    $propType = $Matches[1].Trim()
    $propName = $Matches[2]
}
```

**Extract XML comments:**
```powershell
if ($line -match '/// <summary>(.+?)</summary>') {
    $summary = $Matches[1]
}
```

## Integration with Build Process

You can integrate these scripts into your workflow:

### Manual Execution

Run after code generation:
```powershell
# Regenerate API client
nswag run nswag.json

# Update documentation
.\.roo\scripts\generate-api-docs.ps1
```

### Pre-commit Hook

Add to `.git/hooks/pre-commit`:
```bash
#!/bin/bash
powershell.exe -File .roo/scripts/generate-api-docs.ps1
git add Api/API-CLIENT-REFERENCE.md
```

### CI/CD Pipeline

Add to your build script:
```yaml
- name: Generate Documentation
  run: |
    .\.roo\scripts\generate-api-docs.ps1
    git diff --exit-code Api/API-CLIENT-REFERENCE.md || \
      echo "⚠️ Documentation is out of sync"
```

## Tips for Maintaining Scripts

1. **Keep parsing logic simple** - Focus on extracting key information
2. **Handle edge cases** - Check for null/empty values
3. **Add error handling** - Validate input files exist
4. **Version control** - Track script changes alongside code
5. **Test regularly** - Run after major code changes
6. **Document patterns** - Add comments explaining regex patterns

## Example: Documenting Page Objects

If you wanted to create documentation for Page Object Models:

```powershell
# generate-page-docs.ps1
param(
    [string]$SourceDir = "Pages",
    [string]$OutputFile = "Pages/PAGE-OBJECTS.md"
)

# Parse all .cs files in Pages directory
Get-ChildItem "$SourceDir/*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw

    # Extract page class name
    if ($content -match 'public class (\w+Page)') {
        $pageName = $Matches[1]

        # Extract locators
        # Extract methods
        # etc...
    }
}

# Generate markdown with all pages
```

## Related Documentation

- [API Client Reference](../../Api/API-CLIENT-REFERENCE.md) - Generated output example
- [Functional Tests README](../../README.md) - Main test documentation
- [NSwag Configuration](../../nswag.json) - API client generation config
