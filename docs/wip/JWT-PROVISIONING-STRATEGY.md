# JWT Security Configuration Provisioning Strategy

## Current Situation

The application currently requires JWT authentication parameters to be configured:
- **JWT:Key** - Secret key for signing tokens (base64-encoded, 256-bit minimum)
- **JWT:Issuer** - Token issuer identifier
- **JWT:Audience** - Token audience identifier
- **JWT:Lifespan** - Token validity duration (default: 00:10:00)

Development uses hardcoded values in [`appsettings.Development.json`](../src/BackEnd/appsettings.Development.json), but production deployment is broken because these parameters aren't being provisioned.

## Recommended Approach: Progressive Security Enhancement

I recommend a **two-phase approach** that balances immediate functionality with future security best practices:

### Phase 1: App Service Environment Variables (Immediate Fix)

**Why this approach first:**
- ✅ Simplest to implement - no additional Azure resources
- ✅ Fixes broken deployment immediately
- ✅ Follows current infrastructure pattern (already using App Service settings for CORS)
- ✅ Sufficient security for initial production use
- ✅ Easy to test and validate
- ✅ Secrets not stored in source control

**Implementation:**
1. Generate cryptographically secure random JWT key during provisioning
2. Set Issuer/Audience to the backend's actual URL (`https://web-{suffix}.azurewebsites.net`)
3. Store all JWT parameters as Azure App Service Application Settings (environment variables)
4. Output the generated key in provisioning script output (for manual backup/reference)

**Security level:** GOOD
- Secrets encrypted at rest in Azure
- Only accessible to authorized Azure users and the running application
- Not exposed in source control or container images
- Automatic key rotation requires manual reprovisioning

### Phase 2: Azure Key Vault Integration (Future Enhancement)

**Why defer this:**
- Requires additional Azure resources and complexity
- Adds Key Vault provisioning, access policies, and managed identities
- Requires App Service configuration to reference Key Vault
- Best suited for when you have multiple environments or compliance requirements

**Future benefits:**
- ✅ Centralized secret management
- ✅ Automatic key rotation capabilities
- ✅ Audit logging for secret access
- ✅ Separation of secrets from application configuration
- ✅ Supports multiple environments with different secrets

**Implementation (when ready):**
1. Provision Azure Key Vault in Bicep template
2. Enable App Service managed identity
3. Grant App Service access to Key Vault
4. Store JWT key as Key Vault secret
5. Reference Key Vault secret in App Service configuration using `@Microsoft.KeyVault(SecretUri=...)`

## Recommended Solution Details (Phase 1)

### 1. JWT Key Generation Strategy

**Generate during provisioning** using PowerShell's cryptographic random generator:

```powershell
# Generate 256-bit (32 bytes) random key and base64 encode
$jwtKeyBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($jwtKeyBytes)
$jwtKey = [Convert]::ToBase64String($jwtKeyBytes)
```

**Rationale:**
- Cryptographically secure (not pseudo-random)
- Appropriate key length (256 bits for HMAC-SHA256)
- Base64 encoding matches current format
- Generated fresh for each environment

### 2. JWT Issuer/Audience Configuration

**Set to backend URL** dynamically based on provisioned resources:

```
Issuer: https://web-{suffix}.azurewebsites.net
Audience: https://web-{suffix}.azurewebsites.net
```

**Rationale:**
- Follows JWT best practices (issuer identifies token creator)
- Audience validation ensures tokens are intended for this API
- Automatically correct for each deployment
- Matches the actual backend URL that clients will call

### 3. JWT Lifespan

**Default to 20 minutes** (matching development):

```
Lifespan: 00:20:00
```

**Rationale:**
- Balance between security (shorter is better) and UX (longer reduces re-auth)
- Matches current development setting
- Can be adjusted via parameter if needed

### 4. Implementation Changes Required

#### A. Bicep Template ([`infra/main.bicep`](../infra/main.bicep))

Add JWT configuration parameters to backend module:

```bicep
module backend './AzDeploy.Bicep/Web/webapp-appinsights.bicep' = {
  name: 'backend'
  params: {
    suffix: suffix
    location: location
    configuration: [
      // ... existing config ...
      {
        name: 'JWT__ISSUER'
        value: 'https://${backend.outputs.webAppDefaultHostName}'
      }
      {
        name: 'JWT__AUDIENCE'
        value: 'https://${backend.outputs.webAppDefaultHostName}'
      }
      {
        name: 'JWT__KEY'
        value: jwtKey  // Pass as parameter
      }
      {
        name: 'JWT__LIFESPAN'
        value: jwtLifespan  // Pass as parameter
      }
    ]
  }
}
```

#### B. Provisioning Script ([`scripts/Provision-Resources.ps1`](../scripts/Provision-Resources.ps1))

Add JWT parameter generation and passing:

```powershell
# Generate secure JWT key
Write-Verbose "Generating JWT signing key..."
$jwtKeyBytes = New-Object byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($jwtKeyBytes)
$jwtKey = [Convert]::ToBase64String($jwtKeyBytes)

# Deploy with JWT parameters
$result = az deployment group create `
    --name $DeploymentName `
    --resource-group $ResourceGroup `
    --template-file $TemplatePath `
    --parameter staticWebAppLocation=$StaticWebAppLocation `
    --parameter jwtKey=$jwtKey `
    --parameter jwtLifespan='00:20:00' `
    | ConvertFrom-Json

# Output JWT key for reference (mark as sensitive)
Write-Host "JWT Configuration:" -ForegroundColor Cyan
Write-Output "  JWT Key: $jwtKey"
Write-Host "  WARNING: Store this key securely! It will not be displayed again." -ForegroundColor Yellow
```

#### C. Pipeline Variables

**No changes needed** - JWT parameters are configured during provisioning and stored in App Service settings.

If CD pipeline needs to update JWT settings independently, add optional pipeline variables:
- `jwtKey` (secret) - Optional override for JWT signing key
- `jwtLifespan` - Optional override for token lifespan

#### D. Container Environment

**Already working** - [`docker-compose-ci.yml`](../docker/docker-compose-ci.yml) already supports JWT environment variables with defaults for testing.

## Security Considerations

### Current Approach Security Assessment

**Strengths:**
- ✅ Cryptographically secure key generation
- ✅ Keys encrypted at rest in Azure
- ✅ Keys never in source control
- ✅ Keys not exposed in container images
- ✅ Access controlled by Azure RBAC
- ✅ Issuer/Audience validation prevents token misuse

**Limitations:**
- ⚠️ Key visible in provisioning script output (mitigated: one-time display, user should save securely)
- ⚠️ Key rotation requires reprovisioning (acceptable for initial production)
- ⚠️ No audit trail for key access (can add later with Key Vault)

### Migration Path to Key Vault (Phase 2)

When ready to enhance security:

1. Provision Key Vault alongside existing resources
2. Copy existing JWT key to Key Vault
3. Update App Service configuration to reference Key Vault
4. Remove explicit key from App Service settings
5. Enable managed identity and access policies
6. Set up key rotation policies (optional)

**No code changes required** - configuration still loads from environment variables, but Azure provides them via Key Vault reference.

## Testing Strategy

1. **Local Development** - No changes needed, continues using development key
2. **Container Testing** - Works with existing environment variable defaults
3. **Production Deployment** - Provisioning injects generated keys
4. **Validation** - Verify authentication works after deployment

## Rollback Plan

If issues occur:
1. Previous deployments unaffected (keys stored per environment)
2. Can manually add JWT settings to App Service if provisioning fails
3. Can revert to development key temporarily (not recommended for production)

## Documentation Updates Needed

- [`docs/PROVISION-RESOURCES.md`](../docs/PROVISION-RESOURCES.md) - Add JWT key output to "After Deployment" section
- [`docs/DEPLOYMENT.md`](../docs/DEPLOYMENT.md) - Note that JWT configuration is handled during provisioning
- [`README.md`](../README.md) - Update security notes if present

## Recommendation Summary

**Implement Phase 1 immediately:**
- Generate secure JWT key during provisioning
- Set Issuer/Audience to backend URL
- Store as App Service environment variables
- Output key for admin reference

**Plan Phase 2 for future:**
- Add Key Vault when scaling to multiple environments
- Implement when compliance requires enhanced secret management
- Consider when implementing automated key rotation

This approach provides **immediate functionality** with **good security**, while maintaining a **clear path to enhanced security** when needed.
