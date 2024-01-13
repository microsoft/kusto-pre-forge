@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the storage account')
param storageAccountName string

@description('Name of the storage container')
param storageContainerName string

@description('Name of the event grid system topic')
param eventGridTopicName string

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    isHnsEnabled: true
  }

  resource blobServices 'blobServices' = {
    name: 'default'

    resource testContainer 'containers' = {
      name: storageContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

resource newBlobTopic 'Microsoft.EventGrid/systemTopics@2023-06-01-preview' = {
  name: eventGridTopicName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    source: storage.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}
