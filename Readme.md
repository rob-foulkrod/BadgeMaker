# Blazor BadgeMaker with Azure AI Services

The BadgeMaker is a Blazor front-end application that provides a user-friendly interface for designing and generating digital badges. It allows users to customize badges with various templates, colors, and text options. which can be deployed to Azure using the [Azure Developer CLI - AZD](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/overview). 

💪 This template scenario is part of the larger **[Microsoft Trainer Demo Deploy Catalog](https://aka.ms/trainer-demo-deploy)**.

## ⬇️ Installation
- [Azure Developer CLI - AZD](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
    - When installing AZD, the above the following tools will be installed on your machine as well, if not already installed:
        - [GitHub CLI](https://cli.github.com)
        - [Bicep CLI](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/install)
    - You need Owner or Contributor access permissions to an Azure Subscription to  deploy the scenario.
 
### Requires locally
- Docker Desktop
- Dotnet 8 sdk

Deploy to a Dall-e-3 Standard compatable region

## 🚀 Deploying the scenario in 4 steps:

1. Create a new folder on your machine.
```
mkdir rob-foulkrod/BadgeMaker
```
2. Next, navigate to the new folder.
```
cd rob-foulkrod/BadgeMaker
```
3. Next, run `azd init` to initialize the deployment.
```
azd init -t rob-foulkrod/BadgeMaker
```
4. Last, run `azd up` to trigger an actual deployment.
```
azd up
```

⏩ Note: you can delete the deployed scenario from the Azure Portal, or by running ```azd down``` from within the initiated folder.

## What is the demo scenario about?

BadgeMaker Project Description
This project streamlines the creation and processing of digital badges using Dall-e-3 for badge creation. The system comprises three main components:

### BadgeMaker Project Description

This project streamlines the creation and processing of digital badges using Dall-e-3 for badge creation. The system comprises three main components:

1. **BadgeMaker (BlazorFrontEndApp)**
   - A Blazor front-end application for designing and generating digital badges.
   - Users can customize badges with various templates, colors, and text.
   - **Project File**: `BlazorFrontEndApp/BadgeMaker.sln`
   - **Framework**: .NET 8

2. **BadgeProcessingFunction**
   - A backend service for processing badge data, storing information, generating unique identifiers, and integrating with external systems.
   - **Project File**: `BadgeProcessingFunction/BPF/BPF2.csproj`
   - **Framework**: .NET 8

3. **BadgeViewApp**
   - Displays digital badges created by the BadgeMaker and approved by the BadgeProcessingFunction.
   - **Project File**: `BadgeViewApp/BadgeViewApp.sln`
   - **Framework**: .NET 8

Once a badge is created and approved, it is added to a message queue, where an Azure Function downloads it to a Storage Account. The View App shows all approved badges. For deployment, it requires Docker Desktop and .NET 8 SDK, and works with the Azure Developer CLI.


## 💭 Feedback and Contributing
Feel free to create issues for bugs, suggestions or Fork and create a PR with new demo scenarios or optimizations to the templates. 
If you like the scenario, consider giving a GitHub ⭐
 
