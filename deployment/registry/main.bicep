@description('Name of the container registry')
param registryName string

@description('Location for all resources')
param location string = resourceGroup().location

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
