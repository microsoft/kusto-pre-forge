@description('App ID of the principal running the tests')
param testIdentityId string

@description('Object ID of the principal running the tests')
param testIdentityObjectId string

@description('Location for all resources')
param location string = resourceGroup().location

var prefix = 'kpft'
var suffix = uniqueString(resourceGroup().id)
var clusterName = '${prefix}kusto${suffix}'
var kustoDbName = 'test'
var storageAccountName = '${prefix}storage${suffix}'
var testContainerName = 'integrated-tests'
var landingFolder = 'tests-landing'
var testCases = [
  {
    name: 'text'
  }
]

module clusterModule '../../templates/cluster.bicep' = {
  name: '${deployment().name}-cluster'
  params: {
    location: location
    kustoClusterTier: 'Standard'
    kustoClusterSku: 'Standard_E8ads_v5'
    kustoClusterCapacity: 2
    kustoClusterName: clusterName
    kustoDbName: kustoDbName
    doRunKustoDbScript: false
  }
}

resource testCluster 'Microsoft.Kusto/clusters@2023-05-02' existing = {
  name: clusterName

  resource Identifier 'principalAssignments' = {
    name: 'testAdmin'
    dependsOn: [ clusterModule ]

    properties: {
      principalId: testIdentityId
      principalType: 'App'
      role: 'AllDatabasesAdmin'
    }
  }

  resource db 'databases' existing = {
    name: kustoDbName

    //  Script to create all landing tables
    resource script 'scripts' = {
      name: 'schema'
      properties: {
        continueOnErrors: false
        scriptContent: loadTextContent('schema.kql')
      }
    }
  }
}

module storageModule '../../templates/storage.bicep' = {
  name: '${deployment().name}-storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    storageContainerName: 'landing'
    eventGridTopicName: '${prefix}-newBlobTopic-${suffix}'
  }
}

//  Add a container + policies to the storage account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName

  resource blobServices 'blobServices' existing = {
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
    dependsOn: [ storageModule ]
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
                  '${testContainerName}/${landingFolder}/'
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

//  Authorize principal to read / write storage (Storage Blob Data Contributor)
resource appStorageRbacAuthorization 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(testIdentityObjectId, storageAccount.id, 'rbac')
  scope: storageAccount::blobServices::testContainer

  properties: {
    description: 'Giving data contributor'
    principalId: testIdentityObjectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

module folderHandle '../../templates/folder-handler.bicep' = [for case in testCases: {
  name: '${deployment().name}-${case.name}'
  dependsOn: [
    clusterModule
    storageModule
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

output storageLandingUrl string = '${storageAccount.properties.primaryEndpoints.blob}${testContainerName}/${landingFolder}'

output clusterIngestionUri string = clusterModule.outputs.clusterIngestionUri
