@description('Location for all resources')
param location string = resourceGroup().location

@description('Kusto Cluster Tier')
@allowed([
  'Basic'
  'Standard'
])
param kustoClusterTier string = 'Basic'

@description('Kusto Cluster Sku')
param kustoClusterSku string = 'Dev(No SLA)_Standard_E2a_v4'

@description('Number of nodes on Kusto Cluster')
param kustoClusterCapacity int = 1

@description('Name of the Kusto Cluster')
param kustoClusterName string

@description('Name of the Kusto database')
param kustoDbName string

//  Dev cluster
resource testCluster 'Microsoft.Kusto/clusters@2023-05-02' = {
  name: kustoClusterName
  location: location
  sku: {
    name: kustoClusterTier
    tier: kustoClusterSku
    capacity: kustoClusterCapacity
  }
  properties: {
    enableStreamingIngest: true
  }

  //  Landing database
  resource db 'databases' = {
    name: kustoDbName
    kind: 'ReadWrite'
    location: location

    //  Script to create landing table
    resource script 'scripts' = {
      name: 'setup'
      properties: {
        continueOnErrors: false
        scriptContent: loadTextContent('script.kql')
      }
    }
  }
}

output clusterIngestionUri string = testCluster.properties.dataIngestionUri
