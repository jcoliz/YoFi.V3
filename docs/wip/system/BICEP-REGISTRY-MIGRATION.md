---
status: Draft
created: 2026-01-12
target_release: TBD
ado: TBD
---

# Proposal: Migrate Bicep Templates to GitHub Container Registry

## Summary

Migrate the [`infra/AzDeploy.Bicep`](../../infra/AzDeploy.Bicep/) git submodule to GitHub Container Registry (ghcr.io) for public distribution. This provides a NuGet-like experience for consuming Bicep modules with semantic versioning and self-service publishing.

## Current State

**Current approach:**
- Bicep templates stored in separate GitHub repository: https://github.com/jcoliz/AzDeploy.Bicep
- Consumed via git submodule at [`infra/AzDeploy.Bicep/`](../../infra/AzDeploy.Bicep/)
- Referenced in [`infra/main.bicep`](../../infra/main.bicep) using relative paths:
  ```bicep
  module frontend './AzDeploy.Bicep/Web/staticapp.bicep' = { ... }
  module backend './AzDeploy.Bicep/Web/webapp-appinsights.bicep' = { ... }
  ```

**Challenges with current approach:**
- ❌ Git submodule management complexity (init, update, sync)
- ❌ No semantic versioning (git commits/tags only)
- ❌ Requires full repo clone (50+ modules when you only need 2-3)
- ❌ No package-level discoverability
- ❌ Updates require git submodule operations

## Proposed Solution

### GitHub Container Registry (ghcr.io)

Publish Bicep modules as OCI artifacts to GitHub Container Registry, consumed via the Bicep registry syntax.

**Publishing pattern:**
```bash
# Publish individual modules to ghcr.io
az bicep publish --file ./Web/staticapp.bicep \
  --target 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.0.0'

az bicep publish --file ./Web/webapp-appinsights.bicep \
  --target 'br:ghcr.io/jcoliz/azdeploy/web/webapp-appinsights:1.0.0'
```

**Consumption pattern:**
```bicep
// infra/main.bicep (updated)
module frontend 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.0.0' = {
  name: 'frontend'
  params: {
    suffix: suffix
    location: staticWebAppLocation
  }
}

module backend 'br:ghcr.io/jcoliz/azdeploy/web/webapp-appinsights:1.0.0' = {
  name: 'backend'
  params: {
    suffix: suffix
    location: location
    configuration: [...]
  }
}
```

**Benefits:**
- ✅ **Free for public packages** (like NuGet.org for open source)
- ✅ **Semantic versioning** - Support for `1.0.0`, `1.1.0`, `2.0.0`, `latest`
- ✅ **Self-service publishing** - No approval process required
- ✅ **GitHub native integration** - Appears in repository packages
- ✅ **Selective consumption** - Only download modules you need
- ✅ **OCI standard** - Uses same technology as Docker registries
- ✅ **Version pinning** - Pin to specific versions in production
- ✅ **Discoverable** - Listed at https://github.com/jcoliz?tab=packages

## Implementation Plan

### Phase 1: Setup Publishing Infrastructure

**1. Configure GitHub Container Registry access**
- Create GitHub Personal Access Token with `write:packages` scope
- Store token securely (GitHub Actions secrets for automation)
- Test manual publish of one module

**2. Define module naming convention**

OCI registries (including ghcr.io) support forward slashes in repository names, allowing hierarchical organization:

- Pattern: `br:ghcr.io/jcoliz/azdeploy/{category}/{module}:{version}`
- Examples:
  - `web/staticapp` - Web/staticapp.bicep
  - `web/webapp-appinsights` - Web/webapp-appinsights.bicep
  - `insights/appinsights` - Insights/appinsights.bicep
  - `storage/storage` - Storage/storage.bicep
  - `storage/container` - Storage/storcontainer.bicep

This mirrors the directory structure of the source repository and provides better organization in the GitHub Packages UI.

**3. Define versioning strategy**
- Use semantic versioning: `MAJOR.MINOR.PATCH`
- MAJOR: Breaking changes (parameter renames, removed outputs)
- MINOR: Backward-compatible additions (new optional parameters)
- PATCH: Bug fixes, documentation updates
- Initial release: `1.0.0` for all currently stable modules

### Phase 2: Automate Publishing with GitHub Actions

**Create publishing workflow in AzDeploy.Bicep repository:**

#### Option A: Manual Module List (Simple, Explicit)

`.github/workflows/publish-modules.yml`:
```yaml
name: Publish Bicep Modules

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to publish (e.g., 1.0.0)'
        required: true

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      packages: write
      contents: read

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Azure CLI
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Determine version
        id: version
        run: |
          if [[ "${{ github.event_name }}" == "push" ]]; then
            VERSION=${GITHUB_REF#refs/tags/v}
          else
            VERSION=${{ github.event.inputs.version }}
          fi
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Publish Web modules
        run: |
          VERSION=${{ steps.version.outputs.version }}

          az bicep publish --file ./Web/staticapp.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/web/staticapp:${VERSION}"

          az bicep publish --file ./Web/webapp-appinsights.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/web/webapp-appinsights:${VERSION}"

          az bicep publish --file ./Web/webapp.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/web/webapp:${VERSION}"

          az bicep publish --file ./Web/fn.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/web/fn:${VERSION}"

          az bicep publish --file ./Web/fn-storage.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/web/fn-storage:${VERSION}"

      - name: Publish Insights modules
        run: |
          VERSION=${{ steps.version.outputs.version }}

          az bicep publish --file ./Insights/appinsights.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/insights/appinsights:${VERSION}"

      - name: Publish Storage modules
        run: |
          VERSION=${{ steps.version.outputs.version }}

          az bicep publish --file ./Storage/storage.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/storage/storage:${VERSION}"

          az bicep publish --file ./Storage/storcontainer.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/storage/container:${VERSION}"

      - name: Publish OperationalInsights modules
        run: |
          VERSION=${{ steps.version.outputs.version }}

          az bicep publish --file ./OperationalInsights/loganalytics.bicep \
            --target "br:ghcr.io/jcoliz/azdeploy/logs/loganalytics:${VERSION}"

      # Add additional module categories as needed

      - name: Create release summary
        run: |
          echo "## Published Modules" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "Version: \`${{ steps.version.outputs.version }}\`" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "Modules published to \`ghcr.io/jcoliz/azdeploy/*\`" >> $GITHUB_STEP_SUMMARY
```

**Trade-offs:**
- ✅ **Explicit control** - You choose exactly which modules to publish
- ✅ **Simple to understand** - Clear what's being published
- ❌ **Manual maintenance** - Need to add new modules to workflow
- ❌ **Can forget to add** - New modules won't be published until added

#### Option B: Auto-Discovery (Automated, Requires Convention)

`.github/workflows/publish-modules-auto.yml`:
```yaml
name: Publish Bicep Modules (Auto-Discovery)

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to publish (e.g., 1.0.0)'
        required: true

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      packages: write
      contents: read

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Azure CLI
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Determine version
        id: version
        run: |
          if [[ "${{ github.event_name }}" == "push" ]]; then
            VERSION=${GITHUB_REF#refs/tags/v}
          else
            VERSION=${{ github.event.inputs.version }}
          fi
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Discover and publish all modules
        run: |
          VERSION=${{ steps.version.outputs.version }}

          # Find all .bicep files (excluding nested modules in subdirectories)
          # Adjust the find pattern based on your repository structure
          find . -maxdepth 2 -name "*.bicep" -type f | while read -r file; do
            # Extract category (directory name) and module name (file name without .bicep)
            dir=$(dirname "$file" | sed 's|^\./||')
            module=$(basename "$file" .bicep)

            # Skip files in root directory or special directories
            if [[ "$dir" == "." ]] || [[ "$dir" == ".github" ]]; then
              continue
            fi

            # Convert directory name to lowercase for registry path
            category=$(echo "$dir" | tr '[:upper:]' '[:lower:]')

            echo "Publishing: $file -> ghcr.io/jcoliz/azdeploy/${category}/${module}:${VERSION}"

            az bicep publish --file "$file" \
              --target "br:ghcr.io/jcoliz/azdeploy/${category}/${module}:${VERSION}" || {
              echo "Warning: Failed to publish $file"
            }
          done

      - name: Create release summary
        run: |
          echo "## Published Modules" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "Version: \`${{ steps.version.outputs.version }}\`" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "All discovered modules published to \`ghcr.io/jcoliz/azdeploy/*\`" >> $GITHUB_STEP_SUMMARY
```

**Trade-offs:**
- ✅ **Automatic discovery** - New modules automatically published
- ✅ **No maintenance** - Add a `.bicep` file and it's published
- ✅ **Consistent naming** - Enforces registry naming convention
- ❌ **Less control** - All modules published automatically
- ❌ **Requires convention** - Directory structure must match desired registry paths
- ❌ **Harder to debug** - Less obvious what's being published

#### Option C: Hybrid Approach (Recommended)

Create a **module manifest file** that lists publishable modules:

`.github/modules.txt`:
```
Web/staticapp.bicep
Web/webapp-appinsights.bicep
Web/webapp.bicep
Web/fn.bicep
Web/fn-storage.bicep
Insights/appinsights.bicep
Storage/storage.bicep
Storage/storcontainer.bicep
OperationalInsights/loganalytics.bicep
```

`.github/workflows/publish-modules-manifest.yml`:
```yaml
name: Publish Bicep Modules

on:
  push:
    tags:
      - '*.*.*'  # Matches semantic version tags: 1.0.0, 2.1.3, etc.

jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      packages: write
      contents: read

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Azure CLI
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Extract version from tag
        id: version
        run: |
          VERSION=${GITHUB_REF#refs/tags/}
          echo "version=$VERSION" >> $GITHUB_OUTPUT

      - name: Publish modules from manifest
        run: ./scripts/publish-modules.sh ${{ steps.version.outputs.version }}

      - name: Create release summary
        run: |
          echo "## Published Modules" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "Version: \`${{ steps.version.outputs.version }}\`" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "Modules from manifest published to \`ghcr.io/jcoliz/azdeploy/*\`" >> $GITHUB_STEP_SUMMARY
```

`scripts/publish-modules.sh`:
```bash
#!/bin/bash
set -euo pipefail

VERSION="${1:-}"
MANIFEST_FILE=".github/modules.txt"

# Validate version parameter
if [[ -z "$VERSION" ]]; then
  echo "Error: Version parameter required"
  echo "Usage: $0 <version>"
  exit 1
fi

# Validate manifest file exists
if [[ ! -f "$MANIFEST_FILE" ]]; then
  echo "Error: Manifest file not found: $MANIFEST_FILE"
  exit 1
fi

echo "Publishing Bicep modules version: $VERSION"
echo "Using manifest: $MANIFEST_FILE"
echo ""

# Track publish success/failure
SUCCESS_COUNT=0
FAILURE_COUNT=0

while IFS= read -r file; do
  # Skip empty lines and comments
  [[ -z "$file" || "$file" =~ ^# ]] && continue

  # Extract category and module name
  dir=$(dirname "$file")
  module=$(basename "$file" .bicep)
  category=$(echo "$dir" | tr '[:upper:]' '[:lower:]')

  registry_path="ghcr.io/jcoliz/azdeploy/${category}/${module}:${VERSION}"

  echo "Publishing: $file -> $registry_path"

  if az bicep publish --file "$file" --target "br:$registry_path"; then
    ((SUCCESS_COUNT++))
    echo "✓ Success: $file"
  else
    ((FAILURE_COUNT++))
    echo "✗ Failed: $file"
  fi

  echo ""

done < "$MANIFEST_FILE"

# Print summary
echo "===================="
echo "Publishing Complete"
echo "===================="
echo "Success: $SUCCESS_COUNT"
echo "Failed:  $FAILURE_COUNT"
echo ""

if [[ $FAILURE_COUNT -gt 0 ]]; then
  echo "Warning: Some modules failed to publish"
  exit 1
fi

echo "All modules published successfully"
```

**Trade-offs:**
- ✅ **Explicit control** - You choose which modules to publish
- ✅ **Easy to maintain** - Simple text file to update
- ✅ **Self-documenting** - Manifest shows published modules
- ✅ **Flexible** - Can add comments, organize sections
- ✅ **DRY** - No repeated workflow code per module
- ✅ **Testable** - Can run script locally before pushing
- ✅ **Maintainable** - Logic in version control, not inline YAML
- ❌ **Still manual** - Need to add new modules to manifest

**Recommendation:** Use **Option C (Hybrid with Manifest + Bash Script)** for the best balance of control, maintainability, and testability.

**Why extract to a script?**
- ✅ **Testable locally** - Run `./scripts/publish-modules.sh 1.0.0-test` before committing
- ✅ **Easier to maintain** - Bash scripts are easier to debug than inline YAML
- ✅ **Reusable** - Can run manually for emergency publishes
- ✅ **Better error handling** - Proper exit codes and error reporting
- ✅ **Version controlled** - Script changes tracked separately
- ✅ **Standard practice** - Most mature projects extract complex logic to scripts

**Common practice in GitHub Actions:**
- Simple commands (1-3 lines): Keep inline in workflow
- Complex logic (loops, conditionals, error handling): Extract to script
- Examples: Most major open-source projects (Kubernetes, .NET, etc.) use separate scripts

**Benefits of automation:**
- Consistent publishing process
- Version-tagged releases
- Manual trigger option for emergency publishes
- GitHub release summary for each publish

### Phase 3: Update YoFi.V3 to Consume Registry Modules

**1. Update `infra/main.bicep`:**

```bicep
//
// Provisions a complete set of needed production resources for YoFi.V3
//
// Includes:
//    * Azure Static Web App hosting the front-end Nuxt application
//    * Azure App Service hosting the back-end .NET API
//

@description('Primary location for all resources')
param location string = resourceGroup().location

@description('Location for static web app resources--only allowed to be in certain regions')
param staticWebAppLocation string = resourceGroup().location

@description('Unique suffix for all resources in this deployment')
param suffix string = uniqueString(subscription().id,resourceGroup().id)

@description('JWT signing key for authentication (base64-encoded string, 256-bit minimum)')
@secure()
param jwtKey string

@description('JWT token lifespan')
param jwtLifespan string = '00:20:00'

// Provision Static Web App for front-end
module frontend 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.0.0' = {
  name: 'frontend'
  params: {
    suffix: suffix
    location: staticWebAppLocation
  }
}

// Construct backend URL for JWT issuer/audience
var backendUrl = 'https://web-${suffix}.azurewebsites.net'

// Provision Web App with App Insights and Log Analytics for backend API
module backend 'br:ghcr.io/jcoliz/azdeploy/web/webapp-appinsights:1.0.0' = {
  name: 'backend'
  params: {
    suffix: suffix
    location: location
    // Persistent storage needed for SQLite database files
    configuration: [
      {
        name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
        value: 'true'
      }
      {
        name: 'Application__AllowedCorsOrigins__0'
        value: 'https://${frontend.outputs.defaultHostname}'
      }
      {
        name: 'Jwt__Issuer'
        value: backendUrl
      }
      {
        name: 'Jwt__Audience'
        value: backendUrl
      }
      {
        name: 'Jwt__Key'
        value: jwtKey
      }
      {
        name: 'Jwt__Lifespan'
        value: jwtLifespan
      }
    ]
  }
}

output webAppName string = backend.outputs.webAppName
output webAppDefaultHostName string = backend.outputs.webAppDefaultHostName
output appInsightsName string = backend.outputs.appInsightsName
output logAnalyticsName string = backend.outputs.logAnalyticsName
output staticWebAppName string = frontend.outputs.name
output staticWebHostName string = frontend.outputs.defaultHostname
```

**2. Remove git submodule:**

```bash
# Remove submodule from git
git submodule deinit -f infra/AzDeploy.Bicep
git rm -f infra/AzDeploy.Bicep
rm -rf .git/modules/infra/AzDeploy.Bicep

# Update .gitmodules (remove AzDeploy.Bicep entry)

# Commit changes
git add .gitmodules infra/main.bicep
git commit -m "refactor(infra): migrate from git submodule to ghcr.io registry"
```

**3. Update documentation:**

Update [`infra/RESOURCES-TEMPLATE.md`](../../infra/RESOURCES-TEMPLATE.md) and any provisioning scripts to reference registry modules instead of submodule.

### Phase 4: Version Management Strategy

**Development workflow:**
1. Work on AzDeploy.Bicep repository (make changes, test locally)
2. When ready for release, create git tag: `git tag 1.1.0`
3. Push tag: `git push origin 1.1.0`
4. GitHub Actions automatically publishes all modules to ghcr.io
5. Update YoFi.V3 to consume new version: `br:ghcr.io/jcoliz/azdeploy/web-staticapp:1.1.0`

**Version pinning strategy for YoFi.V3:**
- **Production deployments:** Pin to specific versions (e.g., `1.0.0`)
- **Development deployments:** Can use `latest` for testing
- **CI/CD pipelines:** Always use pinned versions for reproducibility

**Updating to new versions:**
```bicep
// Before
module frontend 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.0.0' = { ... }

// After (when upgrading)
module frontend 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.1.0' = { ... }
```

## Migration Checklist

### AzDeploy.Bicep Repository

- [ ] Create GitHub Personal Access Token with `write:packages` scope
- [ ] Test manual publish of one module to ghcr.io
- [ ] Create `.github/workflows/publish-modules.yml` workflow
- [ ] Publish initial v1.0.0 release (create tag, let workflow run)
- [ ] Verify packages appear at https://github.com/jcoliz?tab=packages
- [ ] Update README.md with registry consumption examples
- [ ] Add CHANGELOG.md for version history

### YoFi.V3 Repository

- [ ] Wait for AzDeploy.Bicep v1.0.0 to be published
- [ ] Create new branch for migration
- [ ] Update [`infra/main.bicep`](../../infra/main.bicep) to use registry modules
- [ ] Test deployment with registry modules (deploy to test resource group)
- [ ] Remove git submodule from repository
- [ ] Update [`.gitmodules`](../../.gitmodules) file
- [ ] Update [`infra/RESOURCES-TEMPLATE.md`](../../infra/RESOURCES-TEMPLATE.md) documentation
- [ ] Update [`scripts/Provision-Resources.ps1`](../../scripts/Provision-Resources.ps1) if needed
- [ ] Test full deployment workflow end-to-end
- [ ] Create PR and merge to main

## Rollback Plan

If issues arise with registry modules:

1. **Immediate rollback:** Revert commit that removed submodule
2. **Re-initialize submodule:**
   ```bash
   git submodule update --init --recursive
   ```
3. **Fix issues** in AzDeploy.Bicep repository
4. **Publish new version** to registry
5. **Re-attempt migration** with fixed version

## Testing Strategy

### Pre-Migration Testing

1. **Test publish workflow:**
   - Manually publish test module to personal ghcr.io
   - Verify module appears in GitHub packages
   - Test consumption in isolated Bicep file

2. **Test module functionality:**
   - Deploy using registry module to test resource group
   - Verify all outputs are correct
   - Confirm no regressions vs. submodule version

### Post-Migration Testing

1. **Test full YoFi.V3 deployment:**
   - Deploy to test resource group using registry modules
   - Verify all resources created correctly
   - Test application functionality end-to-end

2. **Test CI/CD pipelines:**
   - Verify automated deployments work with registry modules
   - Confirm no submodule-related errors

## Documentation Updates

### AzDeploy.Bicep Repository

Update README.md with:
- Installation instructions using registry syntax
- Version listing and changelog
- Contribution guidelines for versioning
- Publishing process documentation

Example README section:
```markdown
## Installation

Consume modules from GitHub Container Registry:

```bicep
module staticWebApp 'br:ghcr.io/jcoliz/azdeploy/web/staticapp:1.0.0' = {
  name: 'myStaticApp'
  params: {
    suffix: 'myapp'
    location: 'westus2'
  }
}
```

See [CHANGELOG.md](./CHANGELOG.md) for version history.
```

### YoFi.V3 Repository

Update infrastructure documentation:
- Remove submodule initialization steps
- Add registry module version management guidelines
- Document how to upgrade to new module versions

## Cost Analysis

**GitHub Container Registry (ghcr.io):**
- ✅ **Free for public packages**
- ✅ **Unlimited bandwidth for public packages**
- ✅ **No storage costs for public packages**

**Alternative (Azure Container Registry):**
- ❌ Basic tier: ~$5/month
- ❌ Standard tier: ~$20/month

**Recommendation:** Use free GitHub Container Registry for public modules.

## Security Considerations

1. **Package visibility:**
   - Modules published to ghcr.io are PUBLIC
   - Do NOT include secrets, keys, or sensitive configuration
   - Review each module before publishing

2. **Version immutability:**
   - Once published, versions should be immutable
   - Never overwrite existing version tags
   - Use new version numbers for changes

3. **Access control:**
   - GitHub token with `write:packages` scope required for publishing
   - Store token securely in GitHub Actions secrets
   - Limit token scope to packages only

## Success Criteria

- [ ] All required modules published to ghcr.io with v1.0.0
- [ ] YoFi.V3 successfully deploys using registry modules
- [ ] Git submodule removed from YoFi.V3 repository
- [ ] Documentation updated in both repositories
- [ ] GitHub Actions workflow successfully publishes modules
- [ ] No regression in deployment functionality
- [ ] Faster clone times for YoFi.V3 (no submodule initialization)

## Future Enhancements

1. **Automated testing in AzDeploy.Bicep:**
   - Add bicep build/lint checks to PR workflow
   - Deploy test resources to validate modules before publish

2. **Version badge in README:**
   - Display latest version badge from ghcr.io
   - Link to package page

3. **Multi-registry support:**
   - Publish to both ghcr.io and Azure public registry
   - Provide consumers choice of registry

4. **Module documentation site:**
   - Generate docs from Bicep comments
   - Host on GitHub Pages

## References

- [Bicep Registry Specification](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/private-module-registry)
- [GitHub Container Registry Documentation](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Bicep Modules in Container Registries](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/modules#publish-to-a-registry)
- [OCI Artifacts Specification](https://github.com/opencontainers/artifacts)

## Decision

**Status:** Pending review and approval

**Decision date:** TBD

**Implementation target:** TBD
