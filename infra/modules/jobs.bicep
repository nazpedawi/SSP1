// The two background jobs, started automatically by messages on the queues (KEDA "azure-queue" scaler)

@description('Azure region for the resources')
param location string

@description('Resource id of the Container Apps environment')
param environmentId string

@description('Registry login server, e.g. ssp1acrnp.azurecr.io')
param registryLoginServer string

@description('Resource id of the identity that may pull images')
param pullIdentityId string

@description('Image tag to deploy')
param imageTag string

@description('Name of the storage account')
param storageAccountName string

@description('Name of the fetch-weather job')
param fetchJobName string

@description('Name of the process-image job')
param processJobName string

@secure()
@description('Unsplash access key (secret, never stored in Git)')
param unsplashAccessKey string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

// ---------- Job 1: fetch-weather-job, triggered by the start-jobs queue ----------
resource fetchJob 'Microsoft.App/jobs@2025-01-01' = {
  name: fetchJobName
  location: location
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Event'          // start on an event (a queue message), not on a schedule
      replicaTimeout: 300           // a run may take at most 5 minutes
      replicaRetryLimit: 1
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: 5
          pollingInterval: 10       // check the queue every 10 seconds
          rules: [
            {
              name: 'start-jobs-queue'
              type: 'azure-queue'
              metadata: {
                accountName: storageAccountName
                queueName: 'start-jobs'
                queueLength: '1'    // 1 run per message
              }
              identity: 'system'    // read the queue length with the job's own identity (no keys)
            }
          ]
        }
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
          name: 'fetch-weather-job'
          image: '${registryLoginServer}/fetch-weather-job:${imageTag}'
          env: [
            {
              name: 'STORAGE_ACCOUNT_NAME'
              value: storageAccountName
            }
          ]
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
    }
  }
}

// ---------- Job 2: process-image-job, triggered by the process-image queue (fan-out) ----------
resource processJob 'Microsoft.App/jobs@2025-01-01' = {
  name: processJobName
  location: location
  identity: {
    type: 'SystemAssigned,UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Event'
      replicaTimeout: 300
      replicaRetryLimit: 1
      secrets: [
        {
          name: 'unsplash-access-key'
          value: unsplashAccessKey
        }
      ]
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: 10         // at most 10 images processed at the same time
          pollingInterval: 10
          rules: [
            {
              name: 'process-image-queue'
              type: 'azure-queue'
              metadata: {
                accountName: storageAccountName
                queueName: 'process-image'
                queueLength: '1'    // 1 run per station message
              }
              identity: 'system'
            }
          ]
        }
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
          name: 'process-image-job'
          image: '${registryLoginServer}/process-image-job:${imageTag}'
          env: [
            {
              name: 'STORAGE_ACCOUNT_NAME'
              value: storageAccountName
            }
            {
              name: 'UNSPLASH_ACCESS_KEY'
              secretRef: 'unsplash-access-key'   // read from the secret above
            }
          ]
          resources: {
            cpu: json('0.5')        // image editing needs a bit more power
            memory: '1Gi'
          }
        }
      ]
    }
  }
}

// ---------- Permissions for both jobs on the storage account ----------
var storageRoles = [
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88'  // Storage Queue Data Contributor
  'ba92f5b4-2d11-453d-a403-e96b0029c9fe'  // Storage Blob Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'  // Storage Table Data Contributor
]

resource fetchJobRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in storageRoles: {
  name: guid(storageAccount.id, fetchJob.id, role)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
    principalId: fetchJob.identity.principalId
    principalType: 'ServicePrincipal'
  }
}]

resource processJobRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in storageRoles: {
  name: guid(storageAccount.id, processJob.id, role)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
    principalId: processJob.identity.principalId
    principalType: 'ServicePrincipal'
  }
}]
