// Container Registry for the 3 images (api, fetch-weather-job, process-image-job)
// + a user-assigned identity that is allowed to PULL images (used by the Container Apps)

@description('Azure region for the resources')
param location string

@description('Registry name: 5-50 letters/numbers, globally unique')
param registryName string

@description('Name of the identity that pulls images')
param pullIdentityName string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: {
    name: 'Basic'               // cheapest tier, enough for this project
  }
  properties: {
    adminUserEnabled: false     // no registry password: access only via identities
    publicNetworkAccess: 'Enabled'
  }
}

// The identity the Container Apps use to download images from the registry
resource pullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: pullIdentityName
  location: location
}

// Built-in Azure role "AcrPull" (fixed id, the same in every subscription)
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullIdentity.id, 'AcrPull')
  scope: registry
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: pullIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output registryName string = registry.name
output loginServer string = registry.properties.loginServer
output pullIdentityId string = pullIdentity.id
