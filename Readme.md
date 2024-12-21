# PR-701 61100 Projects

This system is designed to streamline the creation and processing of digital badges. It consists of two main components: the BadgeMaker application for designing and generating badges, and the BadgeProcessingFunction, a backend service for processing and managing badge data.

## Components

### BadgeMaker (BlazorFrontEndApp)

The BadgeMaker is a Blazor front-end application that provides a user-friendly interface for designing and generating digital badges. It allows users to customize badges with various templates, colors, and text options.

- **Project File**: `BlazorFrontEndApp/BadgeMaker.sln`
- **Framework**: dotnet 8
- **Key Features**:
  - Badge design interface
  - Template selection
  - Color and text customization

### BadgeProcessingFunction

The BadgeProcessingFunction is a backend service responsible for processing badge data. It handles tasks such as storing badge information, generating unique identifiers for badges, and integrating with external systems for badge verification.

- **Project File**: `BadgeProcessingFunction/BPF/BPF2.csproj`
- **Framework**: dotnet 8
- **Key Features**:
  - Badge data processing
  - Unique identifier generation
  - External system integration

### BadgeViewApp

The BadgeViewApp displays digital badges created by the `BadgeMaker` and approved by the `BadgeProcessingFunction`. It serves as the final interface for users to view their completed badges.

**Project File**: `BadgeViewApp/BadgeViewApp.sln`
**Framework**: dotnet 8
**Key Features**:
 - Display approved badges

----

## Now works with the Azure Developer CLI  

Requires locally:
- Docker Desktop
- Dotnet 8 sdk

```
azd init -t rob-foulkrod/BadgeMaker
azd up
```
Deploy to a Dall-e-3 Standard compatable region
