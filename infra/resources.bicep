targetScope = 'resourceGroup'

param location string
param tags object
param environmentName string
param rgName string

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

module monitoring 'br/public:avm/ptn/azd/monitoring:0.1.0' = {
  name: 'monitoring'
  params: {
    logAnalyticsName: '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: '${abbrs.portalDashboards}${resourceToken}'
    location: location
    tags: tags
  }
}

module aiAccount 'br/public:avm/res/cognitive-services/account:0.9.0' = {
  name: 'aiAccountDeployment'
  params: {
    kind: 'OpenAI'
    name: '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    tags: tags
    customSubDomainName: '${abbrs.cognitiveServicesAccounts}${resourceToken}'
    deployments: [
      {
        model: {
          format: 'OpenAI'
          name: 'dall-e-3'
          version: '3.0'
        }
        name: 'dall-e-3'
        sku: {
          capacity: 1
          name: 'Standard'
        }
      }
    ]
    location: location

    publicNetworkAccess: 'Enabled'
    disableLocalAuth: false
  }
}

module serviceBus 'br/public:avm/res/service-bus/namespace:0.10.1' = {
  name: 'serviceBusDeployment'
  params: {
    name: '${abbrs.serviceBusNamespaces}${resourceToken}'
    disableLocalAuth: false
    location: location
    skuObject: {
      name: 'Standard'
    }
    zoneRedundant: false
    tags: tags
    queues: [
      {
        name: '${abbrs.serviceBusNamespacesQueues}${resourceToken}'
        deadLetteringOnMessageExpiration: true
        defaultMessageTimeToLive: 'P7D'
        lockDuration: 'PT5M'
        maxDeliveryCount: 1
      }
    ]
  }
}

module storageAccount 'br/public:avm/res/storage/storage-account:0.14.3' = {
  name: 'storageAccountDeployment'

  params: {
    name: '${abbrs.storageStorageAccounts}${resourceToken}'
    tags: tags
    allowBlobPublicAccess: true //is a false here needed?
    defaultToOAuthAuthentication: true // Default to Entra ID Authentication
    supportsHttpsTrafficOnly: true
    kind: 'StorageV2'
    location: location
    skuName: 'Standard_LRS'
    blobServices: {
      enabled: true
    }
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

module serverfarm 'br/public:avm/res/web/serverfarm:0.4.0' = {
  name: 'serverfarmDeployment'
  params: {
    name: '${abbrs.webServerFarms}${resourceToken}'
    location: location
    tags: tags
    kind: 'windows'
    skuName: 'B3'
    skuCapacity: 1
  }
}

module blazorFrontEndWebApp 'br/public:avm/res/web/site:0.12.0' = {
  name: 'blazorFrontEndWebAppDeployment'
  params: {
    kind: 'app'
    tags: union(tags, {
      'azd-service-name': 'badge-maker'
    })
    name: '${abbrs.webSitesAppService}FrontEnd${resourceToken}'
    serverFarmResourceId: serverfarm.outputs.resourceId
    appInsightResourceId: monitoring.outputs.applicationInsightsResourceId
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
      }
    ]
    httpsOnly: true
    location: location
    managedIdentities: {
      systemAssigned: true
    }

    publicNetworkAccess: 'Enabled'

    siteConfig: {
      alwaysOn: true
      metadata: [
        {
          name: 'CURRENT_STACK'
          value: 'dotnetcore'
        }
      ]
    }
    appSettingsKeyValuePairs: {
      openai__deployment: 'dall-e-3'
      openai__endpoint: 'https://${aiAccount.outputs.name}.openai.azure.com/'
      servicebus__namespace: '${serviceBus.outputs.name}.servicebus.windows.net'
      servicebus__queueName: '${abbrs.serviceBusNamespacesQueues}${resourceToken}'
    }
  }
}

//give the webapp role assignment Azure Service Bus Data Sender in the servicebus
//69a216fc-b8fb-44d8-bc22-1f3c2cd27a39
module servicebusContributor 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'resourceRoleAssignmentDeployment'
  params: {
    // Required parameters
    principalId: blazorFrontEndWebApp.outputs.systemAssignedMIPrincipalId
    resourceId: serviceBus.outputs.resourceId
    roleDefinitionId: '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
    roleName: 'Azure Service Bus Data Sender'
  }
}

//Cognitive Services OpenAI User
//5e0bd9bd-7b93-4f28-af87-19fc36ad61bd

module aiContributor 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'aiContributorDeployment'
  params: {
    // Required parameters
    principalId: blazorFrontEndWebApp.outputs.systemAssignedMIPrincipalId
    resourceId: aiAccount.outputs.resourceId
    roleDefinitionId: '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
    roleName: 'Cognitive Services OpenAI User'
  }
}
