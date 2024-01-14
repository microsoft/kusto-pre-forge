@description('Location for all resources')
param location string = resourceGroup().location

var prefix = 'kpft'
var suffix = uniqueString(resourceGroup().id)
var storageAccountName = '${prefix}storage${suffix}'
var testContainerName = 'integrated-tests'
var landingFolder = 'tests-landing'
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
    storageAccountName: storageAccountName
    storageContainerName: 'landing'
    eventGridTopicName: '${prefix}-newBlobTopic-${suffix}'
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName

  resource blobServices 'blobServices' existing= {
    name: 'default'

    resource testContainer 'containers' = {
      name: testContainerName
      properties: {
        publicAccess: 'None'
      }
    }
  }

  resource policies 'managementPolicies' = {
    name: 'default'
    dependsOn: [storage]
    properties: {
      policy: {
        rules: [
          {
            definition: {
              actions: {
                baseBlob: {
                  delete: {
                    daysAfterCreationGreaterThan: 1
                  }
                }
              }
              filters: {
                prefixMatch: [
                  '${testContainerName}/${landingFolder}'
                ]
                blobTypes: [
                  'blockBlob'
                ]
              }
            }
            enabled: true
            name: 'clean-tests'
            type: 'Lifecycle'
          }
        ]
      }
    }
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
