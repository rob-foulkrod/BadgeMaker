targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string


param badgeViewAppDefinition object = {
  settings: []
}

param badgeViewAppExists bool = false

param deployImages bool = true

@description('Location for the AI/OpenAI account (may differ from primary location due to model availability)')
@metadata({
  azd: {
    type: 'location'
    displayName: 'AI Location'
    description: 'Select an Azure region for the OpenAI/gpt-image-1.5 deployment. Not all regions support this model — eastus2 and swedencentral are recommended.'
  }
})
param aiLocation string

var tags = {
  'azd-env-name': environmentName
  SecurityControl: 'Ignore'
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
    aiLocation: aiLocation
  }
}

//leaving these here to show them appearing in the .env file. 
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output AZURE_RESOURCE_BADGE_VIEW_APP_ID string = resources.outputs.AZURE_RESOURCE_BADGE_VIEW_APP_ID
output AZURE_RESOURCE_BADGE_MAKER_ID string = resources.outputs.AZURE_RESOURCE_BADGE_MAKER_ID
output AZURE_RESOURCE_BADGE_PROCESSING_FUNCTION_ID string = resources.outputs.AZURE_RESOURCE_BADGE_PROCESSING_FUNCTION_ID
output AZURE_RESOURCE_GROUP_NAME string = rg.name
output AZURE_FUNCTION_NAME string = resources.outputs.AZURE_FUNCTION_NAME
output AZURE_WEBAPP_NAME string = resources.outputs.AZURE_WEBAPP_NAME
