targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

var tags = {
  'azd-env-name': environmentName
}

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

//invoke the resources.bicep file
module resources './resources.bicep' = {
  scope: rg
  name: 'resourcesDeployment'
  params: {
    location: location
    tags: tags
    environmentName: environmentName
  }
}
