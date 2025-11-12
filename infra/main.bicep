//
// Provisions a complete set of needed production resources for YoFi.V3
//
// Includes:
//    * Resource group
//    * Azure Static Web App hosting the front-end Nuxt application
//    * Azure App Service hosting the back-end .NET API
//

targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Unique suffix for all resources in this deployment')
@minLength(5)
param suffix string = uniqueString(subscription().id,environmentName)

@description('Optional custom domain to assign')
param customDomain string = ''

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
}

module logs 'AzDeploy.Bicep/OperationalInsights/loganalytics.bicep' = {
  name: 'logs'
  scope: rg
  params: {
    suffix: suffix
    location: location
  }
}

module insights 'AzDeploy.Bicep/Insights/appinsights.bicep' = {
  name: 'insights'
  scope: rg
  params: {
    suffix: suffix
    location: location
    logAnalyticsName: logs.outputs.logAnalyticsName
  }
}

module web 'AzDeploy.Bicep/Web/webapp.bicep' = {
  name: 'web'
  scope: rg
  params: {
    suffix: suffix
    location: location
    insightsName: insights.outputs.name
  }
}

module staticApp 'AzDeploy.Bicep/Web/staticapp-withdomain.bicep' = {
  name: 'static'
  scope: rg
  params: {
    suffix: suffix
    location: location
    customDomain: customDomain
  }
}

output resourceGroupName string = rg.name
output webAppName string = web.outputs.webAppName
output hostingPlanName string = web.outputs.hostingPlanName
output appInsightsName string = insights.outputs.name
output logAnalyticsName string = logs.outputs.logAnalyticsName
output staticAppName string = staticApp.outputs.name
output staticAppHostName string = staticApp.outputs.defaultHostname

