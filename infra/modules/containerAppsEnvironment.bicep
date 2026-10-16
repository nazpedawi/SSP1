// The environment the API and both jobs run in, plus Log Analytics for their logs

@description('Azure region for the resources')
param location string

@description('Region for the Container Apps environment (can differ when a region has no capacity)')
param environmentLocation string

@description('Name of the Container Apps environment')
param environmentName string

@description('Name of the Log Analytics workspace')
param logAnalyticsName string

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource environment 'Microsoft.App/managedEnvironments@2026-07-01' = {
  name: environmentName
  location: environmentLocation
  properties: {
    // Explicitly a workload-profiles environment: "express" environments don't support jobs
    environmentMode: 'WorkloadProfiles'
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

output environmentId string = environment.id
