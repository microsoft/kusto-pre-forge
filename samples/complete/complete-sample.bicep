@description('Location for all resources')
param location string = resourceGroup().location

var containerImageLocation = 'kustopreforge.azurecr.io/kusto-pre-forge/dev:latest'
var prefix = 'kpf'
var suffix = uniqueString(resourceGroup().id)

module coreModule 'complete.bicep' = {
  name: '${deployment().name}-core'
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
