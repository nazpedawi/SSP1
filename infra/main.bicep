// Main template: combines all modules

@description('Azure region (must be allowed by the student subscription policy)')
param location string = resourceGroup().location

@description('Region for the Container Apps environment, API and jobs (germanywestcentral has no capacity for new environments)')
param appsLocation string = 'swedencentral'

@description('Short unique suffix, e.g. your initials (lowercase letters and numbers only)')
param suffix string

@description('Tag of the images to deploy')
param imageTag string = 'latest'

@secure()
@description('Unsplash access key, passed in when deploying (never stored in Git)')
param unsplashAccessKey string

var storageAccountName = 'ssp1stg${suffix}'
var registryName = 'ssp1acr${suffix}'
var pullIdentityName = 'ssp1-pull-id-${suffix}'
var environmentName = 'ssp1-env-${suffix}'
var logAnalyticsName = 'ssp1-law-${suffix}'
var apiAppName = 'ssp1-api-${suffix}'
var fetchJobName = 'ssp1-fetch-job-${suffix}'
var processJobName = 'ssp1-process-job-${suffix}'

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
    environmentLocation: appsLocation
    environmentName: environmentName
    logAnalyticsName: logAnalyticsName
  }
}

module api 'modules/api.bicep' = {
  name: 'api'
  params: {
    location: appsLocation
    appName: apiAppName
    environmentId: environment.outputs.environmentId
    registryLoginServer: registry.outputs.loginServer
    pullIdentityId: registry.outputs.pullIdentityId
    imageTag: imageTag
    storageAccountName: storage.outputs.storageAccountName
  }
}

module jobs 'modules/jobs.bicep' = {
  name: 'jobs'
  params: {
    location: appsLocation
    environmentId: environment.outputs.environmentId
    registryLoginServer: registry.outputs.loginServer
    pullIdentityId: registry.outputs.pullIdentityId
    imageTag: imageTag
    storageAccountName: storage.outputs.storageAccountName
    fetchJobName: fetchJobName
    processJobName: processJobName
    unsplashAccessKey: unsplashAccessKey
  }
}

output storageAccountName string = storage.outputs.storageAccountName
output registryName string = registry.outputs.registryName
output registryLoginServer string = registry.outputs.loginServer
output apiUrl string = 'https://${api.outputs.fqdn}'
