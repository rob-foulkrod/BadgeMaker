# BadgeMaker Solution - Copilot Instructions

## Solution Overview
BadgeMaker is a multi-tier application for creating and managing digital badges using Azure AI Services (DALL-E 3). The solution consists of three interconnected applications that work together to create, process, and display AI-generated badges.

## Architecture Components

### 1. BlazorFrontEndApp (Badge Creation)
**Path:** `/BlazorFrontEndApp/BadgeMaker/`
**Type:** Blazor Server Application (.NET 8)
**Purpose:** Front-end application for badge design and generation

#### Key Features:
- Interactive badge generator using DALL-E 3
- Real-time badge preview
- Approval workflow that sends messages to Azure Service Bus
- Fluent UI components for modern interface

#### Key Files:
- `Components/Pages/BadgeGenerator.razor` - Main badge generation UI
- `Components/Services/OpenAIService.cs` - DALL-E 3 integration
- `Components/Services/ServiceBusService.cs` - Queue message handling
- `Components/Models/BadgeGeneratorViewModel.cs` - View model for badge generation

#### Configuration Requirements:
- OpenAI/Azure OpenAI credentials (endpoint, API key, deployment name)
- Azure Service Bus connection string (optional for approval workflow)
- Application Insights for telemetry

#### Local Development Setup:
```json
// secrets.json structure
{
  "openai": {
    "apiKey": "[openai-api-key]",
    "deployment": "[dall-e-3-deployment-name]",
    "endpoint": "https://[instance-name].openai.azure.com/"
  },
  "servicebus": {
    "connectionString": "[service-bus-connection-string]",
    "queueName": "[queue-name]"
  }
}
```

### 2. BadgeProcessingFunction (Badge Processing)
**Path:** `/BadgeProcessingFunction/BPF/`
**Type:** Azure Function App (.NET 8)
**Purpose:** Backend service for processing approved badges

#### Key Features:
- Service Bus trigger for processing badge approval messages
- Downloads images from DALL-E temporary storage
- Stores badges permanently in Azure Blob Storage
- Managed identity authentication for Azure services

#### Key Files:
- `BadgeDownloader.cs` - Main function for downloading and storing badges
- `Program.cs` - Function app configuration and DI setup

#### Configuration Requirements:
- Azure Storage Account connection (for badge storage)
- Service Bus connection (trigger source)
- Application Insights integration

### 3. BadgeViewApp (Badge Display)
**Path:** `/BadgeViewApp/`
**Type:** ASP.NET Core Razor Pages Application (.NET 8)
**Purpose:** Display approved and processed badges

#### Key Features:
- Lists all approved badges from storage
- Displays badge images
- Docker support for containerized deployment

#### Key Files:
- `Pages/Index.cshtml` - Main page listing badges
- `Pages/BadgeView.cshtml.cs` - Badge image retrieval
- `Models/BadgeService.cs` - Service for accessing badge storage
- `Dockerfile` - Container configuration

#### Configuration:
- Storage account connection for reading badges
- Application Insights for monitoring

## Data Flow

1. **Badge Creation**: User describes badge in BlazorFrontEndApp → DALL-E 3 generates image
2. **Approval**: User approves badge → Message sent to Service Bus queue
3. **Processing**: Azure Function triggered → Downloads image from DALL-E → Stores in Blob Storage
4. **Display**: BadgeViewApp reads from Blob Storage → Displays all approved badges

## Deployment

### Azure Resources Required:
- Azure App Service (for BlazorFrontEndApp and BadgeViewApp)
- Azure Function App (for BadgeProcessingFunction)
- Azure OpenAI Service (DALL-E 3 deployment)
- Azure Service Bus (namespace and queue)
- Azure Storage Account (for badge storage)
- Azure Application Insights (monitoring)
- Azure Container Registry (optional, for Docker images)

### Deployment Method:
Uses Azure Developer CLI (azd) for infrastructure as code deployment:
```bash
azd init -t rob-foulkrod/BadgeMaker
azd up
```

### Infrastructure Files:
- `/infra/resources.bicep` - Main infrastructure definition
- `azure.yaml` - Azure Developer CLI configuration

## Common Development Tasks

### Running Locally:
1. **BlazorFrontEndApp**: 
   - Configure secrets.json with OpenAI and Service Bus settings
   - Run: `dotnet run` in `/BlazorFrontEndApp/BadgeMaker/`

2. **BadgeProcessingFunction**:
   - Configure local.settings.json
   - Run: `func start` in `/BadgeProcessingFunction/BPF/`

3. **BadgeViewApp**:
   - Configure appsettings.json with storage connection
   - Run: `dotnet run` in `/BadgeViewApp/`

### Testing:
- Unit tests available in `/BlazorFrontEndApp/BadgeMaker.Tests/`
- Run tests: `dotnet test` in test project directory

## Technology Stack
- **Framework**: .NET 8
- **Frontend**: Blazor Server with Fluent UI
- **Backend**: Azure Functions (Isolated worker)
- **AI Service**: Azure OpenAI (DALL-E 3)
- **Messaging**: Azure Service Bus
- **Storage**: Azure Blob Storage
- **Monitoring**: Application Insights
- **Containerization**: Docker
- **IaC**: Bicep/Azure Developer CLI

## API Rate Limits
- DALL-E 3 service is limited to 3 calls per minute
- Badge generation includes retry logic for rate limit handling

## Security Considerations
- Uses Managed Identity for Azure service authentication where possible
- Secrets should be stored in Azure Key Vault for production
- Application Insights for security monitoring and telemetry

## Troubleshooting Common Issues

### Configuration Warnings:
- Missing OpenAI configuration: Check secrets.json or app settings
- Missing Service Bus configuration: Approval feature will be disabled
- Storage connection issues: Verify connection strings and firewall rules

### Badge Generation Issues:
- Rate limiting: Wait 20 seconds between generation attempts
- Image not loading: Check DALL-E endpoint and API key configuration

### Processing Function Issues:
- Messages not processing: Verify Service Bus connection and queue name
- Storage errors: Check blob container permissions and managed identity access

## Key Business Logic

### Badge Approval Workflow:
1. Badge generated via DALL-E 3
2. User reviews generated badge
3. On approval, badge URL and metadata sent to Service Bus
4. Function downloads from temporary DALL-E storage (URLs expire)
5. Badge permanently stored in Azure Blob Storage
6. Badge available in BadgeViewApp

### Message Format (Service Bus):
```json
{
  "imageUri": "https://dalle-url...",
  "prompt": "user's badge description",
  "timestamp": "2025-09-24T00:00:00Z"
}
```

## Development Best Practices
- Always use async/await for Azure service calls
- Implement proper error handling and logging
- Use Application Insights for distributed tracing
- Follow Microsoft content policies for AI-generated content
- Implement retry policies for transient failures
- Use health checks for production monitoring