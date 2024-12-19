targetScope = 'resourceGroup'

param location string
param tags object
param environmentName string

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
    // Managed Identity Authentication doesn't work without a custom subdomain
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
    roleAssignments: [
      {
        principalId: badgeViewAppIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Storage Blob Data Reader'
      }
    ]
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

module badgeViewAppIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'badgeViewAppidentity'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}badgeViewApp-${resourceToken}'
    location: location
  }
}

// Container registry
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    acrAdminUserEnabled: true
    tags: tags
    publicNetworkAccess: 'Enabled'
    roleAssignments: [
      {
        principalId: badgeViewAppIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: subscriptionResourceId(
          'Microsoft.Authorization/roleDefinitions',
          '7f951dda-4ed3-4680-a7ca-43fe172d538d'
        )
      }
    ]
  }
}

// Container apps environment
module containerAppsEnvironment 'br/public:avm/res/app/managed-environment:0.4.5' = {
  name: 'container-apps-environment'
  params: {
    logAnalyticsWorkspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
    name: '${abbrs.appManagedEnvironments}${resourceToken}'
    location: location
    zoneRedundant: false
  }
}

// var badgeViewAppAppSettingsArray = filter(array(badgeViewAppDefinition.settings), i => i.name != '')
// var badgeViewAppSecrets = map(filter(badgeViewAppAppSettingsArray, i => i.?secret != null), i => {
//   name: i.name
//   value: i.value
//   secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', '-'), '.', '-'), 32)
// })
// var badgeViewAppEnv = map(filter(badgeViewAppAppSettingsArray, i => i.?secret == null), i => {
//   name: i.name
//   value: i.value
// })

module badgeViewAppFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'badgeViewApp-fetch-image'
  params: {
    exists: false //was a parameter. Keep an eye on it
    name: 'badge-view-app'
  }
}

module badgeViewApp 'br/public:avm/res/app/container-app:0.11.0' = {
  name: 'badgeViewApp'
  params: {
    name: 'badge-view-app'
    ingressTargetPort: 5000
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    // secrets: {
    //   secureList:  union([
    //   ],
    //   map(badgeViewAppSecrets, secret => {
    //     name: secret.secretRef
    //     value: secret.value
    //   }))

    containers: [
      {
        image: badgeViewAppFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
      }
    ]
    managedIdentities: {
      systemAssigned: false
      userAssignedResourceIds: [badgeViewAppIdentity.outputs.resourceId]
    }
    registries: [
      {
        server: containerRegistry.outputs.loginServer
        identity: badgeViewAppIdentity.outputs.resourceId
      }
    ]
    environmentResourceId: containerAppsEnvironment.outputs.resourceId
    location: location
    tags: union(tags, { 'azd-service-name': 'badge-view-app' })
  }
}
