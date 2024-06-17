using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;

namespace BadgeMaker.Components.Services;

public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusConfig serviceBusConfig;

    public ServiceBusService(ServiceBusConfig serviceBusConfig)
    {
        this.serviceBusConfig = serviceBusConfig;
    }

    public bool IsConfigured => serviceBusConfig.IsConfigured;


    public async Task SendMessageAsync(string messageBody)
    {
        var client = new ServiceBusClient(serviceBusConfig.connectionString);
        var sender = client.CreateSender(serviceBusConfig.queueName);
        var message = new ServiceBusMessage(messageBody);
        await sender.SendMessageAsync(message);
    }
}
