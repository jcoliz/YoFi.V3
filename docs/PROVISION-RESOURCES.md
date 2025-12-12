# Provisioning Resource Infrastructure for Production

This document describes how to provision the necessary Azure resources to deploy this application into.

## What Gets Provisioned

This script creates the following Azure resources:

| Resource Type | Purpose | Estimated Cost |
|---------------|---------|----------------|
| **Static Web App** (Free tier) | Hosts Vue.js frontend | Free |
| **App Service** (B1 Basic) | Hosts .NET API backend | ~$13/month |
| **App Service Plan** (B1) | Compute for App Service | Included above |
| **Application Insights** | Monitoring and telemetry | ~$0-5/month |
| **Log Analytics Workspace** | Log storage | ~$0-5/month |

**Total estimated cost: ~$15-20/month**

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed and updated
- PowerShell 5.1+ or PowerShell Core 7+
- An Azure subscription with Contributor access
- Git (for submodule operations)

## Provisioning Steps

1. Ensure this repository has been cloned with submodules, or if not, initialize the submodules now
    ```
    git submodule update --init --recursive
    ```

2. Ensure you're logged into Azure using the Azure CLI into the correct subscription where you want the resources provisioned.
    ```
    az login --tenant=<your_tenant_id>
    az account set --subscription <your_subscription_id>
    az account show
    ```

3. Choose resource group and locations. **Examples:**

   ```bash
   # Good location combinations:
   RESOURCE_GROUP="rg-yofi-prod"
   PRIMARY_LOCATION="eastus2"           # Most resources
   STATIC_WEB_APP_LOCATION="eastus2"    # Static Web Apps available here

   # Alternative:
   PRIMARY_LOCATION="westus2"
   STATIC_WEB_APP_LOCATION="westus2"    # Check availability at https://aka.ms/staticwebapps/regions
   ```

4. Run the provisioning script, providing these values
    ```powershell
    ./scripts/Provision-Resources.ps1 -ResourceGroup <your_resource_group> -Location <primary_location> -StaticWebAppLocation <static_web_app_location>
    ```

## Troubleshooting

### Common Issues

**"Static Web Apps not available in this region"**
- Use one of these regions: `eastus2`, `westus2`, `centralus`, `eastasia`, `westeurope`
- See [Static Web Apps regions](https://aka.ms/staticwebapps/regions) for full list

**"Insufficient permissions"**
- Ensure your account has `Contributor` role on the subscription
- Check: `az role assignment list --assignee $(az account show --query user.name -o tsv)`

**"Resource group already exists"**
- The script will use the existing resource group if it exists
- Ensure you have access: `az group show --name <resource-group>`

**PowerShell execution policy issues (Windows)**
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## After Deployment

### 1. Save Important Values

The script outputs deployment information and securely generated secrets. **Important:** Save these values immediately, especially the JWT key.

#### Pipeline Variables

You will need these to deploy using Azure Pipelines CD:

```
Deployment Pipeline Inputs:
  azureStaticAppApiToken: <your_deployment_token>
  azureAppServiceName: web-{suffix}
  backendBaseUrl: https://web-{suffix}.azurewebsites.net
  appInsightsConnectionString: <your_appinsights_connection_string>
```

#### JWT Security Configuration

The provisioning script automatically generates and configures JWT authentication:

```
JWT Configuration:
  JWT Issuer: https://web-{suffix}.azurewebsites.net
  JWT Audience: https://web-{suffix}.azurewebsites.net
  JWT Key: <base64-encoded-256-bit-key>
  JWT Lifespan: 00:20:00
```

**Security Notes:**
- The JWT key is **cryptographically generated** (256-bit random key)
- The key is **automatically configured** as an App Service application setting
- The key is **encrypted at rest** in Azure and only accessible to authorized users
- **Save the JWT key securely** for backup/recovery purposes
- The JWT key **does not need to be added to pipeline variables** (it persists in App Service settings)
- The JWT key is **not stored in source control or container images**

### 2. Set up CD

- YoFi.V3 includes CD pipeline definitions for Azure Pipelines
- Create a new pipeline
- Add the "deployment pipeline inputs" given above as pipeline variables
- **Note:** JWT configuration is already set during provisioning and does not need pipeline variables
- TODO: Add appInsightsConnectionString to CD config

### 3. Set up local development monitoring (TODO)
- Create `./src/AppHost/config.toml`
- Add these lines:
    ```toml
    [Application]
    ApplicationInsights=<your_appinsights_connection_string>
    ```

### 4. Monitoring
Access your application monitoring at:
- **Application Insights**: Search for "insights-{suffix}" in Azure Portal
- **Log Analytics**: Search for "logs-{suffix}" in Azure Portal

## Security Best Practices

### JWT Key Management

The provisioning script implements security best practices for JWT configuration:

1. **Automatic Generation:** Cryptographically secure random 256-bit keys
2. **Secure Storage:** Keys stored as encrypted App Service application settings
3. **No Source Control:** Keys never committed to repositories
4. **One-Time Display:** Keys shown once during provisioning for admin backup
5. **Persistence:** Keys survive application deployments (not overwritten)

### Key Rotation

To rotate the JWT key, use the provided script:

```powershell
# Rotate JWT key (with confirmation prompt)
.\scripts\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123"

# Rotate without confirmation (for automation)
.\scripts\Rotate-JwtKey.ps1 -ResourceGroup "yofi-rg" -AppServiceName "web-abc123" -Confirm:$false
```

**Important:** Key rotation immediately invalidates all existing JWT tokens, requiring all users to re-authenticate.

**User Experience During Key Rotation:**

When JWT keys are rotated:

1. **Active users** making API calls will receive 401 Unauthorized responses
2. **NuxtIdentity** (the authentication library) automatically:
   - Detects the 401 response
   - Clears the invalid token from browser storage
   - Redirects user to the login page
3. **User sees** a clean redirect to sign-in (not error messages)
4. **After sign-in** user receives a new valid JWT token

**Best Practices:**
- Schedule key rotation during low-traffic periods if possible
- Notify users in advance if rotation will affect many active sessions
- Monitor Application Insights for 401 spikes after rotation
- Consider implementing token refresh mechanisms for graceful rotation in the future

The rotation script:
- Generates a cryptographically secure new 256-bit key
- Displays current JWT configuration for verification
- Prompts for confirmation before making changes
- Updates the App Service setting automatically
- Outputs the new key for secure backup

See [`scripts/Rotate-JwtKey.ps1`](../scripts/Rotate-JwtKey.ps1) for details.

### Future Security Enhancements

For enhanced security in the future, consider:

- **Azure Key Vault Integration:** Store JWT key in Key Vault instead of App Service settings
- **Managed Identity:** Use managed identity for App Service to access Key Vault
- **Automatic Key Rotation:** Implement automated key rotation policies
- **Multiple Key Support:** Support multiple keys for graceful rotation

See [`docs/wip/JWT-PROVISIONING-STRATEGY.md`](wip/JWT-PROVISIONING-STRATEGY.md) for detailed implementation plans.

## Coming in Future

In the future, this script will be improved to...

* Assign a custom domain to the static web app
* Include storage (when features needing storage get implemented)
* Optional Azure Key Vault provisioning for enhanced secret management
