# Containerizing Azure Developer CLI (azd) Templates: Removing Local .NET Dependencies

This guide walks through converting an azd template from local .NET builds to container-based deployment, eliminating the need for .NET SDK installation on developer machines. The approach uses Azure Container Registry (ACR) remote builds to compile and package applications in the cloud.

## Overview

By default, azd templates with .NET services require:
- .NET SDK 8.0+ installed locally
- Local compilation during `azd package`
- Zip-based deployment artifacts

This containerization approach instead:
- Builds applications as Docker images using ACR remote builds
- Deploys container images to Azure App Service and Azure Functions
- Requires only Docker knowledge (no .NET SDK needed locally)

## Prerequisites

- Azure Developer CLI (`azd`) installed
- Azure CLI (`az`) installed and authenticated
- Docker knowledge for Dockerfile creation
- PowerShell (for the build scripts)

## Step-by-Step Conversion

### 1. Create Dockerfiles for Each Service

For each .NET service in your template, create a `Dockerfile` in the service root:

**ASP.NET Core Web App** (`BlazorFrontEndApp/BadgeMaker/Dockerfile`):
```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

**Azure Functions** (`BadgeProcessingFunction/BPF/Dockerfile`):
```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS final
WORKDIR /home/site/wwwroot
ENV AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish ./
```

**Add `.dockerignore` files** to each service directory:
```
bin/
obj/
**/bin/
**/obj/
*.user
*.suo
.vscode/
.env
secrets.json
local.settings.json
```

### 2. Update Infrastructure (Bicep Templates)

#### Add Container Image Parameters

In `infra/main.bicep`:
```bicep
param badgeMakerWebImageRepository string = 'badge-front-end-app'
param badgeMakerWebImageTag string = 'latest'
param badgeProcessingFunctionImageRepository string = 'badge-processing-function'
param badgeProcessingFunctionImageTag string = 'latest'
```

In `infra/main.parameters.json`:
```json
{
  "parameters": {
    "badgeMakerWebImageRepository": {
      "value": "${SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME=badge-front-end-app}"
    },
    "badgeMakerWebImageTag": {
      "value": "${SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG=latest}"
    },
    "badgeProcessingFunctionImageRepository": {
      "value": "${SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME=badge-processing-function}"
    },
    "badgeProcessingFunctionImageTag": {
      "value": "${SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG=latest}"
    }
  }
}
```

#### Update Resources for Container Deployment

In `infra/resources.bicep`:

1. **Add Azure Container Registry**:
```bicep
module containerRegistry 'br/public:avm/res/container-registry/registry:0.1.1' = {
  name: 'registry'
  params: {
    name: '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    acrAdminUserEnabled: false
    tags: tags
    publicNetworkAccess: 'Enabled'
  }
}
```

2. **Convert App Service Plan to Linux**:
```bicep
module serverfarm 'br/public:avm/res/web/serverfarm:0.4.0' = {
  name: 'serverfarmDeployment'
  params: {
    name: '${abbrs.webServerFarms}WebApp${resourceToken}'
    location: location
    tags: tags
    kind: 'linux'
    skuName: 'P1v3'  // Premium plan required for containers
    skuCapacity: 1
  }
}
```

3. **Configure App Service for Container**:
```bicep
var blazorFrontEndAppImage = '${containerRegistry.outputs.loginServer}/${badgeMakerWebImageRepository}:${badgeMakerWebImageTag}'

module blazorFrontEndWebApp 'br/public:avm/res/web/site:0.12.0' = {
  params: {
    kind: 'app,linux'
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      http20Enabled: true
      linuxFxVersion: 'DOCKER|${blazorFrontEndAppImage}'
      acrUseManagedIdentityCreds: true
    }
    appSettingsKeyValuePairs: {
      WEBSITES_PORT: '8080'
      WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'false'
      // ...existing app settings
    }
  }
}
```

4. **Configure Function App for Container**:
```bicep
var badgeProcessingFunctionImage = '${containerRegistry.outputs.loginServer}/${badgeProcessingFunctionImageRepository}:${badgeProcessingFunctionImageTag}'

module badgeProcessingFunctionPlan 'br/public:avm/res/web/serverfarm:0.4.0' = {
  params: {
    kind: 'elastic'  // Elastic Premium for containerized functions
    skuName: 'EP1'
  }
}

module badgeProcessingFunction 'br/public:avm/res/web/site:0.12.0' = {
  params: {
    kind: 'functionapp'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${badgeProcessingFunctionImage}'
      http20Enabled: true
      ftpsState: 'Disabled'
      acrUseManagedIdentityCreds: true
    }
    appSettingsKeyValuePairs: {
      FUNCTIONS_EXTENSION_VERSION: '~4'
      FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
      WEBSITES_PORT: '8080'
      WEBSITES_ENABLE_APP_SERVICE_STORAGE: 'false'
      AzureWebJobsFeatureFlags: 'EnableWorkerIndexing'
      // ...existing function settings
    }
  }
}
```

5. **Add ACR Pull Permissions**:
```bicep
module webAppAcrPull 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'webAppAcrPullAssignment'
  params: {
    principalId: blazorFrontEndWebApp.outputs.systemAssignedMIPrincipalId
    resourceId: containerRegistry.outputs.resourceId
    roleDefinitionId: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    roleName: 'AcrPull'
  }
}

module functionAcrPull 'br/public:avm/ptn/authorization/resource-role-assignment:0.1.1' = {
  name: 'functionAcrPullAssignment'
  params: {
    principalId: badgeProcessingFunction.outputs.systemAssignedMIPrincipalId
    resourceId: containerRegistry.outputs.resourceId
    roleDefinitionId: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    roleName: 'AcrPull'
  }
}
```

### 3. Update azure.yaml Configuration

Replace the standard azd configuration with container-aware settings:

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/Azure/azure-dev/main/schemas/v1.0/azure.yaml.json

name: your-app-name
metadata:
    template: your-app-name@0.0.2
infra:
    provider: bicep
    parameters:
        badgeMakerWebImageRepository: ${SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME=badge-front-end-app}
        badgeMakerWebImageTag: ${SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG=latest}
        badgeProcessingFunctionImageRepository: ${SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME=badge-processing-function}
        badgeProcessingFunctionImageTag: ${SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG=latest}
services:
    badge-front-end-app:
        project: ./BlazorFrontEndApp/BadgeMaker
        host: appservice
        language: dotnet
    badge-view-app:
        project: ./BadgeViewApp
        host: containerapp
        language: dotnet
        docker:
            path: Dockerfile
            remoteBuild: true  # Only works for Container Apps
    badge-processing-function:
        project: ./BadgeProcessingFunction/BPF
        language: dotnet
        host: function
hooks:
    postprovision:
        - shell: pwsh
          run: |
              $ErrorActionPreference = 'Stop'
              $acrLoginServer = (azd env get-value AZURE_CONTAINER_REGISTRY_ENDPOINT).Trim()
              if ([string]::IsNullOrWhiteSpace($acrLoginServer)) {
                  throw 'AZURE_CONTAINER_REGISTRY_ENDPOINT is not set. Run "azd provision" first.'
              }
              $registryName = $acrLoginServer.Split('.')[0]

              function Invoke-AcrBuild {
                  param(
                      [string]$ServicePath,
                      [string]$DefaultImageName,
                      [string]$NameKey,
                      [string]$TagKey
                  )

                  Push-Location $ServicePath
                  try {
                      $imageName = (azd env get-value $NameKey).Trim()
                      if ([string]::IsNullOrWhiteSpace($imageName)) {
                          $imageName = $DefaultImageName
                      }
                      $imageTag = (azd env get-value $TagKey).Trim()
                      if ([string]::IsNullOrWhiteSpace($imageTag)) {
                          $imageTag = (Get-Date -Format 'yyyyMMddHHmmss')
                      }
                      az acr build --registry $registryName --image "$imageName:$imageTag" --file Dockerfile .
                      azd env set $NameKey $imageName | Out-Null
                      azd env set $TagKey $imageTag | Out-Null
                  }
                  finally {
                      Pop-Location
                  }
              }

              Invoke-AcrBuild './BlazorFrontEndApp/BadgeMaker' 'badge-front-end-app' 'SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME' 'SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG'
              Invoke-AcrBuild './BadgeProcessingFunction/BPF' 'badge-processing-function' 'SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME' 'SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG'
workflows:
    up:
        steps:
            - azd: provision
            - azd: package
            - azd: deploy --all
```

## Key Changes Explained

### 1. Remote Build Strategy
- **Container Apps**: Use `docker.remoteBuild: true` for native azd support
- **App Service/Functions**: Use `postprovision` hooks with `az acr build` since they don't support the `remoteBuild` flag

### 2. Service Hosting Changes
- **App Service Plans**: Must be Linux (`kind: 'linux'`) to support containers
- **Function Plans**: Use Elastic Premium (`kind: 'elastic'`, `skuName: 'EP1'`) instead of Consumption
- **Container Images**: Reference ACR images via `linuxFxVersion: 'DOCKER|<image-uri>'`

### 3. Security Model
- **Managed Identity**: Services authenticate to ACR using system-assigned managed identities
- **RBAC**: Assign `AcrPull` role to each service's managed identity
- **No Admin Keys**: ACR admin access disabled for security

### 4. Environment Variables
The hook sets these automatically:
- `SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME`
- `SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG`
- `SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME`
- `SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG`

## Usage for Demo Creators

### Initial Setup
```bash
azd init -t your-containerized-template
azd auth login
azd up
```

### Development Workflow
```bash
# Make code changes
# Run azd up - builds happen in Azure, no local .NET needed
azd up

# Or deploy only (skips provisioning)
azd deploy
```

### Customizing Images
```bash
# Set custom image names before deployment
azd env set SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME my-custom-web-app
azd env set SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME my-custom-function

# Deploy with custom names
azd up
```

## Benefits for Demo Creators

1. **No Local Dependencies**: Only need Azure CLI and azd installed
2. **Consistent Builds**: All compilation happens in Azure with identical environments
3. **Faster Onboarding**: New team members don't need to install .NET SDK
4. **Cross-Platform**: Works identically on Windows, macOS, and Linux
5. **Cloud-Native**: Leverages Azure's container ecosystem fully

## Troubleshooting

### Common Issues
- **Hook Fails**: Ensure you're authenticated with `azd auth login` and have Contributor access
- **Image Pull Fails**: Verify managed identity has `AcrPull` role on the container registry
- **Build Timeouts**: Large applications may need extended timeout settings in ACR build
- **Port Conflicts**: Ensure `WEBSITES_PORT: '8080'` matches your Dockerfile's `EXPOSE` directive

### Debugging
```bash
# Check environment variables
azd env get-values

# View detailed logs
azd up --debug

# Test ACR build manually
az acr build --registry <registry-name> --image test:latest --file Dockerfile .
```

## Migration Checklist

- [ ] Create Dockerfiles for all .NET services
- [ ] Add .dockerignore files to each service
- [ ] Update Bicep templates to provision ACR
- [ ] Convert App Service plans to Linux
- [ ] Configure container image references in resources
- [ ] Add ACR pull permissions for managed identities
- [ ] Update azure.yaml with container parameters
- [ ] Add postprovision hook for ACR builds
- [ ] Test full deployment with `azd up`
- [ ] Verify services start and function correctly
- [ ] Update documentation for new workflow

This approach transforms any .NET azd template into a container-first deployment that eliminates local build dependencies while maintaining the same developer experience.