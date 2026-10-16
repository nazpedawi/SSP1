// Main template: combines all modules

@description('Azure region (must be allowed by the student subscription policy)')
param location string = resourceGroup().location

@description('Short unique suffix, e.g. your initials + a number (lowercase letters and numbers only)')
param suffix string

var storageAccountName = 'ssp1stg${suffix}'
var registryName = 'ssp1acr${suffix}'
var pullIdentityName = 'ssp1-pull-id-${suffix}'

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

output storageAccountName string = storage.outputs.storageAccountName
output registryName string = registry.outputs.registryName
output registryLoginServer string = registry.outputs.loginServer
