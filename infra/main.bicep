//
// Provisions a complete set of needed production resources for YoFi.V3
//
// Includes:
//    * Azure Static Web App hosting the front-end Nuxt application
//    * Azure App Service hosting the back-end .NET API
//
// TODO:
//    * Azure Storage account for persistent file storage

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
module frontend './AzDeploy.Bicep/Web/staticapp.bicep' = {
  name: 'frontend'
  params: {
    suffix: suffix
    location: staticWebAppLocation
  }
}

// Construct backend URL for JWT issuer/audience
var backendUrl = 'https://web-${suffix}.azurewebsites.net'

// Provision Web App with App Insights and Log Analytics for backend API
module backend './AzDeploy.Bicep/Web/webapp-appinsights.bicep' = {
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
