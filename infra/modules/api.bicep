// The API as a Container App with a public URL

@description('Azure region for the resources')
param location string

@description('Name of the API container app')
param appName string

@description('Resource id of the Container Apps environment')
param environmentId string

@description('Registry login server, e.g. ssp1acrnp.azurecr.io')
param registryLoginServer string

@description('Resource id of the identity that may pull images')
param pullIdentityId string

@description('Image tag to deploy')
param imageTag string

@description('Name of the storage account (used by Shared/Storage.cs)')
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    // SystemAssigned: the app's own identity, used to access storage
    // UserAssigned:   the pull identity, used to download the image from the registry
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      ingress: {
        external: true          // reachable from the internet
        targetPort: 8080        // .NET containers listen on 8080
        allowInsecure: false    // https only
      }
      registries: [
        {
          server: registryLoginServer
          identity: pullIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registryLoginServer}/api:${imageTag}'
          env: [
            {
              name: 'STORAGE_ACCOUNT_NAME'   // makes Storage.IsAzure true
              value: storageAccountName
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0          // scale to zero when idle: saves student credit
        maxReplicas: 2
      }
    }
  }
}

// ---------- Permissions for the API's own identity on the storage account ----------
// Fixed ids of built-in Azure roles
var queueDataContributor = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'  // send messages to start-jobs
var blobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'   // list images + user delegation key (SAS)
var tableDataContributor = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'  // read/write job status

resource apiQueueRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiApp.id, queueDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', queueDataContributor)
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiApp.id, blobDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributor)
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiApp.id, tableDataContributor)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', tableDataContributor)
    principalId: apiApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output fqdn string = apiApp.properties.configuration.ingress.fqdn
