// Main template: combines all modules

@description('Azure region (must be allowed by the student subscription policy)')
param location string = resourceGroup().location

@description('Short unique suffix, e.g. your initials + a number (lowercase letters and numbers only)')
param suffix string

var storageAccountName = 'ssp1stg${suffix}'

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: storageAccountName
  }
}

output storageAccountName string = storage.outputs.storageAccountName
