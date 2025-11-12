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

// Provision Web App with App Insights and Log Analytics for backend API
module web './AzDeploy.Bicep/Web/webapp-appinsights.bicep' = {
  name: 'web'
  params: {
    suffix: suffix
    location: location
    // Persistent storage needed for SQLite database files
    configuration: [
      {
        name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
        value: 'true'
      }
    ]
  }
}

// Provision Static Web App for front-end
module staticWebApp './AzDeploy.Bicep/Web/staticapp.bicep' = {
  name: 'staticWebApp'
  params: {
    suffix: suffix
    location: staticWebAppLocation
  }
}

output webAppName string = web.outputs.webAppName
output webAppDefaultHostName string = web.outputs.webAppDefaultHostName
output appInsightsName string = web.outputs.appInsightsName
output logAnalyticsName string = web.outputs.logAnalyticsName
output staticWebAppName string = staticWebApp.outputs.name
output staticWebHostName string = staticWebApp.outputs.defaultHostname
