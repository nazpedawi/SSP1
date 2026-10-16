// Storage account with everything the app needs:
// 2 queues, 1 private blob container and 1 table

@description('Azure region for the resources')
param location string

@description('Storage account name: 3-24 lowercase letters/numbers, must be globally unique')
param storageAccountName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'          // cheapest: 3 copies in one datacenter
  }
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false  // nothing is public: images only via SAS links
    minimumTlsVersion: 'TLS1_2'
    accessTier: 'Hot'
  }
}

// ---------- Blob: private container for the generated images ----------
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource imagesContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'images'
  properties: {
    publicAccess: 'None'
  }
}

// ---------- Queues: one to start a job, one per image (fan-out) ----------
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource startJobsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'start-jobs'
}

resource processImageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'process-image'
}

// ---------- Table: job status and progress ----------
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource jobsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: tableService
  name: 'jobs'
}

output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
