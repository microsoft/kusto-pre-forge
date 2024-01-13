@description('Location for all resources')
param location string = resourceGroup().location

var prefix = 'kpft'
var suffix = uniqueString(resourceGroup().id)
var testCases = [
  {
    name: 'text'
  }
]

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
    eventGridTopicName: '${prefix}-newBlobTopic-${suffix}'
  }
}

module folderHandle '../../templates/folder-handler.bicep' = [for case in testCases: {
  name: '${deployment().name}-${case.name}'
  dependsOn: [
    cluster
    storage
  ]
  params: {
    location: location
    appIdentityName: '${prefix}-app-id-${suffix}'
    kustoClusterName: '${prefix}kusto${suffix}'
    kustoDbName: 'test'
    //  Kusto Table ???
    serviceBusName: '${prefix}-service-bus-${suffix}'
    serviceBusQueueName: case.name
    storageAccountName: '${prefix}storage${suffix}'
    storageContainerName: 'landing'
    eventGridTopicName: '${prefix}-newBlobTopic-${suffix}'
    //  Storage folder
    eventGridSubscriptionName: 'toServiceBus'
    appEnvironmentName: '${prefix}-app-env-${suffix}'
    appName: '${prefix}-app-${suffix}-${case.name}'
  }
}
]
