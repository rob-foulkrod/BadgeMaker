
# BadgeMaker Solution - Copilot Instructions

## Solution Overview
BadgeMaker is a multi-tier Azure application for creating AI-generated badges using DALL-E 3.

## Architecture Components

### BlazorFrontEndApp (`/BlazorFrontEndApp/BadgeMaker/`)
- **Type**: Blazor Server (.NET 8)
- **Purpose**: Badge creation UI with DALL-E 3 integration
- **Key Services**: OpenAIService (DALL-E), ServiceBusService (approvals), BadgeGeneratorViewModel
- **Config**: Requires OpenAI credentials and Service Bus connection

### BadgeProcessingFunction (`/BadgeProcessingFunction/BPF/`)
- **Type**: Azure Function (.NET 8, Isolated)
- **Purpose**: Process approved badges from Service Bus to Blob Storage
- **Trigger**: Service Bus queue messages
- **Auth**: Managed Identity for Azure services

### BadgeViewApp (`/BadgeViewApp/`)
- **Type**: ASP.NET Core Razor Pages (.NET 8)
- **Purpose**: Display stored badges from Blob Storage
- **Features**: Docker support for containerization

## Data Flow
1. User creates badge → DALL-E 3 generates image
2. User approves → Message to Service Bus
3. Function triggered → Downloads image → Stores in Blob Storage
4. BadgeViewApp displays stored badges

## Key Configurations
- **OpenAI**: endpoint, apiKey, deployment (DALL-E 3)
- **Service Bus**: connectionString, queueName
- **Storage**: Azure Blob Storage connection
- **Telemetry**: APPLICATIONINSIGHTS_CONNECTION_STRING

## Telemetry & Observability
- **Package**: Azure Monitor OpenTelemetry (not classic App Insights)
- **Activity Sources**: `BadgeMaker.OpenAIService`, `BadgeMaker.ServiceBus`, `BadgeMaker.UI`
- **Conventions**: `badge.*`, `openai.*`, `queue.*` tag prefixes, `*_duration_ms` metrics

## Important Constraints
- DALL-E 3: Limited to 3 calls per minute
- Badge URLs from DALL-E are temporary (must be downloaded)
- Service Bus message format: `{imageUri, prompt, timestamp}`

## Development Patterns
- Static ActivitySource for telemetry
- Async/await for all Azure service calls
- Managed Identity preferred over connection strings
