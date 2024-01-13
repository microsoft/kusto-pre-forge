@description('Location for all resources')
param location string = resourceGroup().location

var prefix = 'kpft'
var suffix = uniqueString(resourceGroup().id)

module cluster '../../templates/cluster.bicep' = {
  name: '${deployment().name}-cluster'
  params: {
    location: location
    kustoClusterName: '${prefix}kusto${suffix}'
    kustoDbName: 'test'
  }
}

module storage '../../templates/storage.bicep' = {
  name: '${deployment().name}-storage'
  params: {
    location: location
    storageAccountName: '${prefix}storage${suffix}'
    storageContainerName: 'landing'
    eventGridTopicName:'${prefix}-newBlobTopic-${suffix}'
  }
}

/*
module folderHandle '../../templates/folder-handler.bicep' = {
  name: '${deployment().name}-folder-handler'
  dependsOn: [
    cluster
    storage
  ]
  params: {
    location: location
    appIdentityName: '${prefix}-app-id-${suffix}'
    kustoClusterName: '${prefix}kusto${suffix}'
    kustoDbName: 'test'
    serviceBusName: '${prefix}-service-bus-${suffix}'
    serviceBusQueueName: 'blob-notification'
    storageAccountName: '${prefix}storage${suffix}'
    storageContainerName: 'landing'
    eventGridTopicName:'${prefix}-newBlobTopic-${suffix}'
    eventGridSubscriptionName:'toServiceBus'
    appEnvironmentName:'${prefix}-app-env-${suffix}'
    appName:'${prefix}-app-${suffix}'
  }
}
*/
