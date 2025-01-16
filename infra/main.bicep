targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@secure()
param badgeViewAppDefinition object

param badgeViewAppExists bool

param deployImages bool = true

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
    badgeViewAppDefinition: badgeViewAppDefinition
    badgeViewAppExists: badgeViewAppExists
    deployImages: deployImages
  }
}

//leaving these here to show them appearing in the .env file. But this deployment doesn't make use of these values.
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_RESOURCE_BADGE_VIEW_APP_ID string = resources.outputs.AZURE_RESOURCE_BADGE_VIEW_APP_ID
output AZURE_RESOURCE_BADGE_MAKER_ID string = resources.outputs.AZURE_RESOURCE_BADGE_MAKER_ID
