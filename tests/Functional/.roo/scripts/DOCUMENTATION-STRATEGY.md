# Documentation Strategy

## The Simple Answer: Just Use XML Documentation

If your goal is to help Roo (or other AI assistants) understand your codebase, **you don't need to convert XML docs to markdown**. Just enable XML documentation and Roo can read it directly when needed.

## Recommended Approach

### 1. Enable XML Documentation (One-time setup)

Edit `YoFi.V3.Tests.Functional.csproj`:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);CS1591</NoWarn> <!-- Optional: suppress missing doc warnings -->
</PropertyGroup>
```

### 2. Write XML Comments in Your Code

```csharp
/// <summary>
/// Creates multiple workspaces with the specified configuration.
/// </summary>
/// <param name="table">DataTable containing workspace configurations</param>
public async Task GivenIHaveWorkspacesAsync(DataTable table)
{
    // Implementation
}
```

### 3. Build the Project

```powershell
dotnet build
```

This generates: `bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml`

### 4. That's It!

When you ask Roo a question like:
- "How do I use the workspace steps?"
- "What parameters does GivenIHaveWorkspacesAsync accept?"
- "Document the WorkspaceTenancySteps class"

Roo can read the XML file directly:

```
Read bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml
```

No conversion needed!

## When Do You Need Markdown?

Convert to markdown only if you need:

### Human-Readable Documentation
- **For GitHub/GitLab wikis** - Markdown is easier to browse
- **For onboarding docs** - New developers prefer markdown
- **For website generation** - DocFX, Sandcastle, etc.

### Static Documentation Site
- **Documentation generators** like DocFX can consume XML directly
- **Custom styling** - Markdown gives you control over presentation
- **Search and navigation** - Static sites provide better UX

### Version Control History
- **Tracking doc changes** - Easier to see diffs in markdown
- **Pull request reviews** - Markdown is more readable in PR diffs
- **Git blame** - Markdown shows who wrote what documentation

## Best of Both Worlds

### For Auto-Generated Code (ApiClient.cs)
**Problem:** Auto-generated code has no XML comments (NSwag doesn't add them)

**Solution:** Use source parsing script
```powershell
.\.roo\scripts\generate-api-docs.ps1
```

**Why:** Creates searchable reference for humans, preserves API structure

### For Your Own Code (Steps, Pages, Helpers)
**Problem:** Need documentation for Roo and humans

**Solution:** Just use XML comments
```csharp
/// <summary>Documentation here</summary>
public void MyMethod() { }
```

**Why:**
- Roo reads XML directly when asked
- IDE shows docs in IntelliSense
- No extra build steps needed
- If you need markdown later, run the conversion script

## Comparison: Different Approaches

| Approach | Best For | Pros | Cons |
|----------|----------|------|------|
| **XML only** | AI consumption, IDE support | No conversion needed, validated by compiler | Not human-friendly to read raw |
| **Markdown only** | Human docs, wikis | Easy to read/edit | No IDE support, no validation |
| **XML → Markdown** | Both humans and AI | Best of both worlds | Extra build step, maintain two artifacts |
| **Source parsing** | Auto-generated code | Works without XML comments | Fragile, no validation |

## Recommendations by Use Case

### "I want Roo to understand my code"
✅ **Just use XML comments** - Roo reads them directly

### "I want IntelliSense to show documentation"
✅ **Just use XML comments** - Built-in IDE support

### "I need docs for GitHub README"
✅ **Use XML comments + conversion script** (when you need it)

### "I have auto-generated code (ApiClient.cs)"
✅ **Use source parsing script** - No XML comments available

### "I want a documentation website"
✅ **Use DocFX or similar** - Reads XML directly, generates beautiful sites

## Simple Example Workflow

```powershell
# 1. Enable XML docs in .csproj (one time)
# 2. Write code with XML comments
# 3. Build project
dotnet build

# 4. Ask Roo questions - it reads the XML file directly!
# No conversion needed!

# 5. If you need markdown for humans later:
.\.roo\scripts\generate-docs-from-xml.ps1 `
    -XmlDocFile "bin/Debug/net9.0/YoFi.V3.Tests.Functional.xml" `
    -OutputFile "REFERENCE.md"
```

## The Bottom Line

**For auto-generated code:** Use source parsing (`generate-api-docs.ps1`)

**For your own code:** Just write XML comments and let Roo read them

**For human docs:** Convert XML to markdown only when you need it

Don't over-engineer! Start with XML comments, add markdown conversion only if you have a specific need for it.
