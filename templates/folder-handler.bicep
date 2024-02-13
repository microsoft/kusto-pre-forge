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

@description('Blob folder to watch')
param blobFolder string

@description('Name of Kusto table')
param tableName string

@description('File format')
param format string

@description('Input Compression')
param inputCompression string

var containerImageLocation = 'kustopreforge.azurecr.io/kusto-pre-forge/dev:latest'

//  Identity orchestrating, i.e. accessing Kusto + Storage
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: appIdentityName
  location: location
}

resource testCluster 'Microsoft.Kusto/clusters@2023-05-02' existing = {
  name: kustoClusterName

  //  Landing database
  resource db 'databases' existing = {
    name: kustoDbName

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

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' existing = {
  name: storageAccountName

  resource blobServices 'blobServices' existing = {
    name: 'default'

    resource testContainer 'containers' existing = {
      name: storageContainerName
    }
  }
}

resource newBlobTopic 'Microsoft.EventGrid/systemTopics@2023-06-01-preview' existing = {
  name: eventGridTopicName
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    capacity: 1
    name: 'Standard'
    tier: 'Standard'
  }

  resource queue 'queues' = {
    name: serviceBusQueueName
    properties: {
      enableBatchedOperations: true
      enableExpress: false
      enablePartitioning: true
    }
  }
}

resource newBlobSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-06-01-preview' = {
  name: eventGridSubscriptionName
  parent: newBlobTopic
  // dependsOn: [ topicBusRbacAuthorization ]
  properties: {
    deliveryWithResourceIdentity: {
      destination: {
        endpointType: 'ServiceBusQueue'
        properties: {
          resourceId: serviceBus::queue.id
        }
      }
      identity: {
        type: 'SystemAssigned'
      }
    }
    filter: {
      subjectBeginsWith: '/blobServices/default/containers/${storage::blobServices::testContainer.name}/blobs/${blobFolder}'
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
      enableAdvancedFilteringOnArrays: true
    }
    labels: []
    eventDeliverySchema: 'CloudEventSchemaV1_0'
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}

/*
//  Authorize topic to send to service bus
resource topicBusRbacAuthorization 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(newBlobTopic.id, serviceBus::queue.id, 'rbac')
  scope: serviceBus::queue

  properties: {
    description: 'Azure Service Bus Data Sender'
    principalId: newBlobTopic.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
  }
}

//  Authorize app to receive to service bus
resource appBusRbacAuthorization 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appIdentity.id, serviceBus::queue.id, 'rbac')
  scope: serviceBus::queue

  properties: {
    description: 'Azure Service Bus Data Receiver'
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
  }
}

//  Authorize principal to read / write storage (Storage Blob Data Contributor)
resource appStorageRbacAuthorization 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appIdentity.id, storage.id, 'rbac')
  scope: storage::blobServices::testContainer

  properties: {
    description: 'Giving data contributor'
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}
*/

resource appEnvironment 'Microsoft.App/managedEnvironments@2022-10-01' = {
  name: appEnvironmentName
  location: location
  sku: {
    name: 'Consumption'
  }
  properties: {
    zoneRedundant: false
  }
}

resource app 'Microsoft.App/containerApps@2022-10-01' = {
  name: appName
  location: location
  dependsOn: [
    appStorageRbacAuthorization
  ]
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
    }
    environmentId: appEnvironment.id
    template: {
      containers: [
        {
          image: containerImageLocation
          name: 'kusto-pre-forge'
          //  Total CPU and memory for all containers defined in a Container App must add up to one of the following CPU - Memory combinations:
          //  [cpu: 0.25, memory: 0.5Gi]; [cpu: 0.5, memory: 1.0Gi]; [cpu: 0.75, memory: 1.5Gi]; [cpu: 1.0, memory: 2.0Gi]; [cpu: 1.25, memory: 2.5Gi];
          //  [cpu: 1.5, memory: 3.0Gi]; [cpu: 1.75, memory: 3.5Gi]; [cpu: 2.0, memory: 4.0Gi]
          resources: {
            cpu: '1'
            memory: '2Gi'
          }
          env: [
            {
              name: 'AuthMode'
              value: 'ManagedIdentity'
            }
            {
              name: 'ServiceBusQueueUrl'
              value: '${serviceBus.properties.serviceBusEndpoint}/${serviceBus::queue.name}'
            }
            {
              name: 'ManagedIdentityResourceId'
              value: appIdentity.id
            }
            {
              name: 'KustoIngestUri'
              value: testCluster.properties.dataIngestionUri
            }
            {
              name: 'KustoDb'
              value: testCluster::db.name
            }
            {
              name: 'KustoTable'
              value: tableName
            }
            {
              name: 'Format'
              value: format
            }
            {
              name: 'InputCompression'
              value: inputCompression
            }
            {
              name: 'OutputCompression'
              value: 'None'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}
