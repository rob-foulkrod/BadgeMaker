# BadgeMaker


## Configuring Badge Maker

To set up Badge Maker, follow these steps to provide the necessary configuration details:
   - Describe your gpt-image-1.5 deployment. Include relevant information such as the API endpoint, authentication credentials, and any other required parameters.
   - Ensure that Badge Maker can communicate with your gpt-image-1.5 instance.

1. **Create a `secrets.json` File:**
   - Using the cli or visual studio, create a secrets.josn file
   - Add the necessary configuration details in JSON format.

2. **Optional: Service Bus Deployment (Connection String):**
   - If you're using a Service Bus deployment, you can optionally provide the connection string.
   - This can be configured later.
 
    
3. Example of a `secrets.json` file:
    ```json
    {
      "openai": {
        "deployment": "[gpt-image-1.5-deployment-name]",
        "endpoint": "https://[instancename].openai.azure.com/"
      },
      "servicebus": {
        "connectionString": "Endpoint=sb://[instance-name].servicebus.windows.net/;SharedAccessKeyName=Badgemaker-app;SharedAccessKey=[sharedaccesskey]=;EntityPath=[queuename]",
        "queueName": "badgeapproved"
      }
    }
    ```
  
Or once deployed to an app service, the configuration can look like this:

```json 
{
    "name": "openai__apiKey",
    "value": "[openaikey]",
    "slotSetting": false
  },
  {
    "name": "openai__deployment",
    "value": "[gpt-image-1.5-deployment-name]",
    "slotSetting": false
  },
  {
    "name": "openai__endpoint",
    "value": "https://[instancename].openai.azure.com/",
    "slotSetting": false
  },
  {
    "name": "servicebus__endpoint",
    "value": "[serviceBusEndpoint]",
    "slotSetting": false
  },
  {
    "name": "servicebus__queueName",
    "value": "[queuename]",
    "slotSetting": false
  }

```
