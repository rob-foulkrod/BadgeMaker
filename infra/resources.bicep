targetScope = 'resourceGroup'

param location string
param tags object
param environmentName string
@secure()
param badgeViewAppDefinition object

param badgeViewAppExists bool

@description('Deploy images to the storage account from the GitHub repo')
param deployImages bool = true

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

//give container app permissiont to app insights
//Monitoring Metrics Publisher Role (3913510d-42f4-4e42-8a64-420c390055eb)
//the conainer app uses ManagedIdentity to talk to App Insights
module monitoringMetricsPublisher 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'monitoringMetricsPublisherDeployment'
  params: {
    principalId: badgeViewAppIdentity.outputs.principalId
    resourceId: monitoring.outputs.applicationInsightsResourceId
    roleDefinitionId: '3913510d-42f4-4e42-8a64-420c390055eb'
    roleName: 'Monitoring Metrics Publisher'
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
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
      }
    ]
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
        maxDeliveryCount: 10
      }
    ]
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
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
      containers: [
        {
          name: 'badges'
          publicAccess: 'None'
        }
      ]
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
      {
        principalId: blobUploadIdentity.outputs.principalId
        principalType: 'ServicePrincipal'
        roleDefinitionIdOrName: 'Storage Blob Data Contributor'
      }
    ]
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
      }
    ]
  }
}

module serverfarm 'br/public:avm/res/web/serverfarm:0.4.0' = {
  name: 'serverfarmDeployment'
  params: {
    name: '${abbrs.webServerFarms}WebApp${resourceToken}'
    location: location
    tags: tags
    kind: 'windows'
    skuName: 'B3'
    skuCapacity: 1
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
      }
    ]
  }
}

module blazorFrontEndWebApp 'br/public:avm/res/web/site:0.12.0' = {
  name: 'blazorFrontEndWebAppDeployment'
  params: {
    kind: 'app'
    tags: union(tags, {
      'azd-service-name': 'badge-front-end-app'
    })
    name: '${abbrs.webSitesAppService}maker${resourceToken}'
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
      servicebus__endpoint: '${serviceBus.outputs.name}.servicebus.windows.net'
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

module blobUploadIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.2.1' = {
  name: 'blobUploadIdentityDeployment'
  params: {
    name: '${abbrs.managedIdentityUserAssignedIdentities}upload-${resourceToken}'
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

var badgeViewAppAppSettingsArray = filter(array(badgeViewAppDefinition.settings), i => i.name != '')
var badgeViewAppSecrets = map(filter(badgeViewAppAppSettingsArray, i => i.?secret != null), i => {
  name: i.name
  value: i.value
  secretRef: i.?secretRef ?? take(replace(replace(toLower(i.name), '_', '-'), '.', '-'), 32)
})
var badgeViewAppEnv = map(filter(badgeViewAppAppSettingsArray, i => i.?secret == null), i => {
  name: i.name
  value: i.value
})

module badgeViewAppFetchLatestImage './modules/fetch-container-image.bicep' = {
  name: 'badgeViewApp-fetch-image'
  params: {
    exists: badgeViewAppExists //was a parameter. Keep an eye on it
    name: '${abbrs.appContainerApps}badgeviewapp${resourceToken}'
  }
}

module badgeViewApp 'br/public:avm/res/app/container-app:0.8.0' = {
  name: 'badgeViewApp'

  params: {
    name: '${abbrs.appContainerApps}badgeviewapp${resourceToken}'
    ingressAllowInsecure: false
    scaleMinReplicas: 1
    scaleMaxReplicas: 10
    ingressTargetPort: 8080

    secrets: {
      secureList: union(
        [],
        map(badgeViewAppSecrets, secret => {
          name: secret.secretRef
          value: secret.value
        })
      )
    }
    containers: [
      {
        image: badgeViewAppFetchLatestImage.outputs.?containers[?0].?image ?? 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
        name: 'main'
        resources: {
          cpu: json('0.5')
          memory: '1.0Gi'
        }
        env: union(
          [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: monitoring.outputs.applicationInsightsConnectionString
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: badgeViewAppIdentity.outputs.clientId
            }
            {
              name: 'BadgesStorageAccount'
              value: storageAccount.outputs.primaryBlobEndpoint
            }
          ],
          badgeViewAppEnv,
          map(badgeViewAppSecrets, secret => {
            name: secret.name
            secretRef: secret.secretRef
          })
        )
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

module uploadBlobsScript 'br/public:avm/res/resources/deployment-script:0.5.0' = if (deployImages) {
  name: 'uploadBlobsScriptDeployment'
  params: {
    kind: 'AzurePowerShell'
    name: 'pwscript-uploadBlobsScript'
    azPowerShellVersion: '12.3'
    location: location
    managedIdentities: {
      userAssignedResourceIds: [
        blobUploadIdentity.outputs.resourceId
      ]
    }
    cleanupPreference: 'OnSuccess'
    retentionInterval: 'P1D'
    enableTelemetry: true
    storageAccountResourceId: storageAccount.outputs.resourceId
    arguments: '-StorageAccountName ${storageAccount.outputs.name}' //multi line strings do not support interpolation in bicep yet
    scriptContent: '''
      param([string] $StorageAccountName)


      Invoke-WebRequest -Uri "https://github.com/rob-foulkrod/BadgeMaker/raw/3b91a9fa5a117bb79807c98bfb767c0d5e0e645e/sampleBadges/badge1.jpg" -OutFile badge1.jpg
      Invoke-WebRequest -Uri "https://github.com/rob-foulkrod/BadgeMaker/raw/3b91a9fa5a117bb79807c98bfb767c0d5e0e645e/sampleBadges/badge2.jpg" -OutFile badge2.jpg

      $context = New-AzStorageContext -StorageAccountName $StorageAccountName

      Set-AzStorageBlobContent -Context $context -Container "badges" -File badge1.jpg -Blob badge1.jpg -Force
      Set-AzStorageBlobContent -Context $context -Container "badges" -File badge2.jpg -Blob badge2.jpg -Force
      '''
  }
}

module badgeProcessingFunctionPlan 'br/public:avm/res/web/serverfarm:0.4.0' = {
  name: 'badgeProcessingFunctionPlanDeployment'
  params: {
    kind: 'functionApp'
    name: '${abbrs.webServerFarms}FunctionPlan${resourceToken}'
    location: location
    tags: tags
    skuName: 'Y1' //Consumption plan
    diagnosticSettings: [
      {
        name: 'basicSetting'
        workspaceResourceId: monitoring.outputs.logAnalyticsWorkspaceResourceId
      }
    ]
  }
}

module badgeProcessingFunction 'br/public:avm/res/web/site:0.12.0' = {
  name: 'badgeProcessingFunctionDeployment'
  params: {
    // Required parameters
    kind: 'functionapp'
    name: '${abbrs.webSitesFunctions}BadgeProcessingFunction${resourceToken}'
    tags: union(tags, { 'azd-service-name': 'badge-processing-function' })
    serverFarmResourceId: badgeProcessingFunctionPlan.outputs.resourceId
    appInsightResourceId: monitoring.outputs.applicationInsightsResourceId
    appSettingsKeyValuePairs: {
      AzureFunctionsJobHost__logging__logLevel__default: 'Trace'
      FUNCTIONS_EXTENSION_VERSION: '~4'
      FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
      WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED: 1
      badgeQueueName: '${abbrs.serviceBusNamespacesQueues}${resourceToken}'

      badgeservicebus__fullyQualifiedNamespace: '${serviceBus.outputs.name}.servicebus.windows.net'
      AzureWebJobsStorage__accountName: storageAccount.outputs.name
      AzureWebJobsStorage__blobUri: storageAccount.outputs.primaryBlobEndpoint
    }

    location: location
    managedIdentities: {
      systemAssigned: true
    }

    siteConfig: {
      netFrameworkVersion: 'v8.0'
      numberOfWorkers: 1
      workerSize: '0'
      workerSizeId: 0
      alwaysOn: false
      use32BitWorkerProcess: false
    }
    storageAccountResourceId: storageAccount.outputs.resourceId
    storageAccountUseIdentityAuthentication: true
  }
}

//Output Blob Requirement
//https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob-output?tabs=python-v2%2Cisolated-process%2Cnodejs-v4&pivots=programming-language-csharp#grant-permission-to-the-identity
module functionStorageBlobOwner 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'functionStorageBlobOwnerDeployment'
  params: {
    principalId: badgeProcessingFunction.outputs.systemAssignedMIPrincipalId
    resourceId: storageAccount.outputs.resourceId
    roleDefinitionId: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
    roleName: 'Storage Blob Data Owner'
  }
}

module functionStorageQueueContributor 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'functionStorageQueueContributorDeployment'
  params: {
    principalId: badgeProcessingFunction.outputs.systemAssignedMIPrincipalId
    resourceId: storageAccount.outputs.resourceId
    roleDefinitionId: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
    roleName: 'Storage Queue Data Contributor'
  }
}

module functionStorageTableContributor 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'functionStorageTableContributorDeployment'
  params: {
    principalId: badgeProcessingFunction.outputs.systemAssignedMIPrincipalId
    resourceId: storageAccount.outputs.resourceId
    roleDefinitionId: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
    roleName: 'Storage Table Data Contributor'
  }
}

module functionServiceBusDataReceiver 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'functionServiceBusDataReceiverDeployment'
  params: {
    principalId: badgeProcessingFunction.outputs.systemAssignedMIPrincipalId
    resourceId: serviceBus.outputs.resourceId
    roleDefinitionId: '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
    roleName: 'Azure Service Bus Data Receiver'
  }
}
//This has to be returned to the main.bicep file for the deployment to work
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = containerRegistry.outputs.loginServer
output AZURE_RESOURCE_BADGE_VIEW_APP_ID string = badgeViewApp.outputs.resourceId
output AZURE_RESOURCE_BADGE_MAKER_ID string = blazorFrontEndWebApp.outputs.resourceId
