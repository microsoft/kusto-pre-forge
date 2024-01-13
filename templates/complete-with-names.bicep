@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the Application Identity')
param appIdentityName string

@description('Name of the Kusto Cluster')
param kustoClusterName string

@description('Name of the Kusto database')
param kustoDbName string

@description('Name of the Service Bus namespace')
param serviceBusName string

@description('Name of the Service Bus queue')
param serviceBusQueueName string

@description('Name of the storage account')
param storageAccountName string

@description('Name of the storage container')
param storageContainerName string

@description('Name of the event grid system topic')
param eventGridTopicName string

@description('Name of the event grid subscription')
param eventGridSubscriptionName string

@description('Name of App Environment')
param appEnvironmentName string

@description('Name of App')
param appName string

//  Identity orchestrating, i.e. accessing Kusto + Storage
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: appIdentityName
  location: location
}

//  Dev cluster
resource testCluster 'Microsoft.Kusto/clusters@2023-05-02' = {
  name: kustoClusterName
  location: location
  sku: {
    name: 'Dev(No SLA)_Standard_E2a_v4'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    enableStreamingIngest: true
  }

  //  Landing database
  resource db 'databases' = {
    name: kustoDbName
    kind: 'ReadWrite'
    location: location

    //  Script to create landing table
    resource script 'scripts' = {
      name: 'setup'
      properties: {
        continueOnErrors: false
        scriptContent: loadTextContent('script.kql')
      }
    }

    //  Data plane permission:  admin for app 
    resource appRole 'principalAssignments' = {
      name: 'string'
      properties: {
        principalId: appIdentity.properties.principalId
        principalType: 'App'
        role: 'Ingestor'
        tenantId: appIdentity.properties.tenantId
      }
    }
  }
}

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

module folderHandle 'folder-handler.bicep' = {
  name: '${deployment().name}-folder-handler'
  params: {
    location: location
    appIdentityName: appIdentityName
    kustoClusterName: kustoClusterName
    kustoDbName: kustoDbName
    serviceBusName: serviceBusName
    serviceBusQueueName: serviceBusQueueName
    storageAccountName: storageAccountName
    storageContainerName: storageContainerName
    eventGridTopicName: eventGridTopicName
    eventGridSubscriptionName: eventGridSubscriptionName
    appEnvironmentName: appEnvironmentName
    appName: appName
  }
}
