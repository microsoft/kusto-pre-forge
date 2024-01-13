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

module cluster 'cluster.bicep' = {
  name: '${deployment().name}-cluster'
  params: {
    location: location
    kustoClusterName: kustoClusterName
    kustoDbName: kustoDbName
  }
}

module storage 'storage.bicep' = {
  name: '${deployment().name}-storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    storageContainerName: storageContainerName
    eventGridTopicName: eventGridTopicName
  }
}

module folderHandle 'folder-handler.bicep' = {
  name: '${deployment().name}-folder-handler'
  dependsOn: [
    cluster
    storage
  ]
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
