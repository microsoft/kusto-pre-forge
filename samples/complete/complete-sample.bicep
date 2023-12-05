@description('Location for all resources')
param location string = resourceGroup().location

var containerImageLocation = 'kustopreforge.azurecr.io/kusto-pre-forge/dev:latest'
var prefix = 'kpf'
var suffix = uniqueString(resourceGroup().id)

//  Identity orchestrating, i.e. accessing Kusto + Storage
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-app-id-${suffix}'
  location: location
}

//  Dev cluster
resource testCluster 'Microsoft.Kusto/clusters@2023-05-02' = {
  name: '${prefix}kusto${suffix}'
  location: location
  sku: {
    name: 'Dev(No SLA)_Standard_E2a_v4'
    tier: 'Basic'
    capacity: 1
  }
  properties: {
    enableStreamingIngest: true
  }

  //  Data plane permission:  admin for app 
  resource symbolicname 'principalAssignments' = {
    name: 'string'
    properties: {
      principalId: appIdentity.properties.principalId
      principalType: 'App'
      role: 'AllDatabasesAdmin'
      tenantId: appIdentity.properties.tenantId
    }
  }

  //  Landing database
  resource db 'databases' = {
    name: 'test'
    kind: 'ReadWrite'
    location: location

    //  Script to create landing table
    resource symbolicname 'scripts' = {
      name: 'setup'
      properties: {
        continueOnErrors: false
        scriptContent: loadTextContent('script.kql')
      }
    }
  }
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${prefix}-service-bus-${suffix}'
  location: location
  sku: {
    capacity: 1
    name: 'Standard'
    tier: 'Standard'
  }

  resource queue 'queues' = {
    name: 'blob-notification'
    properties: {
      enableBatchedOperations: true
      enableExpress: false
      enablePartitioning: true
    }
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: '${prefix}storage${suffix}'
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
      name: 'test'
      properties: {
        publicAccess: 'None'
      }
    }
  }
}

resource newBlobTopic 'Microsoft.EventGrid/systemTopics@2023-06-01-preview' = {
  name: '${prefix}-newBlobTopic-${suffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    source: storage.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }

  resource newBlobSubscription 'eventSubscriptions' = {
    name: 'toServiceBus'
    dependsOn: [ topicBusRbacAuthorization ]
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
        subjectBeginsWith: '/blobServices/default/containers/${storage::blobServices::testContainer.name}/blobs/landing/'
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
}

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
  scope: storage

  properties: {
    description: 'Giving data contributor'
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: resourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  }
}

resource appEnvironment 'Microsoft.App/managedEnvironments@2022-10-01' = {
  name: '${prefix}-app-env-${suffix}'
  location: location
  sku: {
    name: 'Consumption'
  }
  properties: {
    zoneRedundant: false
  }
}

resource app 'Microsoft.App/containerApps@2022-10-01' = {
  name: '${prefix}-app-${suffix}'
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
              name: 'SourceBlob'
              value: 'https://${storage.name}.blob.core.windows.net/dev/adx.gz'
            }
            {
              name: 'DestinationBlobPrefix'
              value: 'https://${storage.name}.blob.core.windows.net/dev/split/'
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
              value: 'Landing'
            }
            {
              name: 'InputCompression'
              value: 'GZip'
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
