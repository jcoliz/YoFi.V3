using './main.bicep'

// Required Parameters

// Complete name of the environment
// Must be 1-64 characters
param environmentName = 'yofi-v3-uat'

// Primary Azure region for resource deployment
// Example: 'eastus', 'westus2', 'centralus'
param location = 'westus2'

// Optional Parameters

// Unique suffix for resource naming (leave empty to auto-generate)
// If provided, must be at least 5 characters
// param suffix = ''

// Custom domain name for the static web app
// Example: 'www.yourdomain.com'
// param customDomain = ''
