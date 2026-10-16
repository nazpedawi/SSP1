// Main template: combines all modules

@description('Azure region (must be allowed by the student subscription policy)')
param location string = resourceGroup().location

@description('Short unique suffix, e.g. your initials + a number (lowercase letters and numbers only)')
param suffix string

@description('Tag of the images to deploy')
param imageTag string = 'latest'

var storageAccountName = 'ssp1stg${suffix}'
var registryName = 'ssp1acr${suffix}'
var pullIdentityName = 'ssp1-pull-id-${suffix}'
var environmentName = 'ssp1-env-${suffix}'
var logAnalyticsName = 'ssp1-law-${suffix}'
var apiAppName = 'ssp1-api-${suffix}'

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: storageAccountName
  }
}

module registry 'modules/containerRegistry.bicep' = {
  name: 'registry'
  params: {
    location: location
    registryName: registryName
    pullIdentityName: pullIdentityName
  }
}

module environment 'modules/containerAppsEnvironment.bicep' = {
  name: 'environment'
  params: {
    location: location
    environmentName: environmentName
    logAnalyticsName: logAnalyticsName
  }
}

module api 'modules/api.bicep' = {
  name: 'api'
  params: {
    location: location
    appName: apiAppName
    environmentId: environment.outputs.environmentId
    registryLoginServer: registry.outputs.loginServer
    pullIdentityId: registry.outputs.pullIdentityId
    imageTag: imageTag
    storageAccountName: storage.outputs.storageAccountName
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output registryName string = registry.outputs.registryName
output registryLoginServer string = registry.outputs.loginServer
output apiUrl string = 'https://${api.outputs.fqdn}'
