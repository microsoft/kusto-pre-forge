@description('Name of the container registry')
param registryName string

@description('Location for all resources')
param location string = resourceGroup().location

var prefix = 'kpft'
var suffix = uniqueString(resourceGroup().id)

//  Identity orchestrating, i.e. accessing Kusto + Storage
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-app-identity-${suffix}'
  location: location
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: registryName
  location: location
  sku: {
    name: 'Standard'
  }
  properties: {
    adminUserEnabled: true
    anonymousPullEnabled: true
    dataEndpointEnabled: false
    policies: {
      azureADAuthenticationAsArmPolicy: {
        status: 'enabled'
      }
      retentionPolicy: {
        status: 'disabled'
      }
      softDeletePolicy: {
        status: 'disabled'
      }
    }
    publicNetworkAccess: 'enabled'
    zoneRedundancy: 'disabled'
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
  name: 'dev-app'
  location: location
  dependsOn: [
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
          image: '${registry.name}/kusto-pre-forge/dev:latest'
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
              name: 'ManagedIdentityResourceId'
              value: appIdentity.id
            }
            {
              name: 'SourceBlobsPrefix'
              value: ''
            }
            {
              name: 'SourceBlobsSuffix'
              value: ''
            }
            {
              name: 'KustoIngestUri'
              value: ''
            }
            {
              name: 'KustoDb'
              value: ''
            }
            {
              name: 'KustoTable'
              value: ''
            }
            {
              name: 'Format'
              value: 'csv'
            }
            {
              name: 'InputCompression'
              value: 'None'
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
