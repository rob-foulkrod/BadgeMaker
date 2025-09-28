# Container Image Build Workflow

The `azure.yaml` configuration now runs container builds against Azure Container Registry (ACR) immediately after `azd provision` completes. This ensures the registry and managed identities exist before the OCI images are produced.

## What changed
-	A top-level `postprovision` hook calls `az acr build` for both the Blazor front end (`./BlazorFrontEndApp/BadgeMaker`) and the Azure Function (`./BadgeProcessingFunction/BPF`).
-	Image names default to `badge-front-end-app` and `badge-processing-function`. Tags are timestamp-based (`yyyyMMddHHmmss`).
-	Environment variables recorded in the active azd environment (`SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME`, `SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG`, `SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME`, `SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG`) feed into the Bicep templates.
-	The `workflows.up` section forces `azd up` to run `provision` before `package` and `deploy`, so image builds always happen after infrastructure creation.

## Running the workflow

```pwsh
azd up
```

`azd` performs the following:

1. `azd provision` – Deploys infrastructure, including the ACR instance and managed identities.
2. `postprovision` hook – Executes the PowerShell script that:
   - Reads the ACR login server from `AZURE_CONTAINER_REGISTRY_ENDPOINT`.
   - Builds the Blazor app image.
   - Builds the function app image.
   - Stores the image names and tags back into the environment.
3. `azd package` – Runs the standard packaging step (artifact not used for container deploys but kept for compatibility).
4. `azd deploy --all` – Bicep templates pull the freshly built images using managed identity.

## Customizing image names or tags

Set the environment variables before running `azd up`:

```pwsh
azd env set SERVICE_BADGE_FRONT_END_APP_IMAGE_NAME badge-maker-web
azd env set SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_NAME badge-processor
```

Tags are generated automatically, but you can override them by setting `SERVICE_BADGE_FRONT_END_APP_IMAGE_TAG` or `SERVICE_BADGE_PROCESSING_FUNCTION_IMAGE_TAG` before invoking `azd up`. If you provide a tag, the hook respects the existing value and does not generate a new one.

## Troubleshooting
-	If the hook fails with `AZURE_CONTAINER_REGISTRY_ENDPOINT is not set`, run `azd provision` first.
-	Ensure the logged-in identity has `AcrPush` permissions when running the build locally.
-	Inspect hook output in the azd log (look for the `postprovision` section) to confirm both builds completed.
