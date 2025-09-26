using Azure.Identity;
using Azure.Messaging.ServiceBus;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;
using System.Diagnostics;

namespace BadgeMaker.Components.Services;

public class ServiceBusService : IServiceBusService
{
    private readonly ServiceBusConfig serviceBusConfig;
    private static readonly ActivitySource ActivitySource = new("BadgeMaker.ServiceBus");

    public ServiceBusService(ServiceBusConfig serviceBusConfig)
    {
        this.serviceBusConfig = serviceBusConfig;
    }

    public bool IsConfigured => serviceBusConfig.IsConfigured;


    public async Task SendMessageAsync(string messageBody)
    {
        using var activity = ActivitySource.StartActivity("SendBadgeApproval");
        activity?.SetTag("servicebus.queue", serviceBusConfig.queueName);
        activity?.SetTag("servicebus.endpoint", serviceBusConfig.endpoint);
        activity?.SetTag("servicebus.message_size_bytes", messageBody?.Length ?? 0);

        if (!IsConfigured)
        {
            const string reason = "Service Bus configuration is missing";
            activity?.SetStatus(ActivityStatusCode.Error, reason);
            activity?.AddEvent(new ActivityEvent("ServiceBusNotConfigured"));
            throw new InvalidOperationException(reason);
        }

        try
        {
            var credential = new DefaultAzureCredential();
            await using var client = new ServiceBusClient(serviceBusConfig.endpoint, credential);
            await using var sender = client.CreateSender(serviceBusConfig.queueName);
            var message = new ServiceBusMessage(messageBody);

            var stopwatch = Stopwatch.StartNew();
            await sender.SendMessageAsync(message);
            stopwatch.Stop();

            activity?.SetTag("servicebus.send.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("servicebus.send.success", true);
            activity?.AddEvent(new ActivityEvent("BadgeApprovalSent"));
        }
        catch (Exception ex)
        {
            activity?.SetTag("servicebus.send.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            activity?.SetTag("exception.message", ex.Message);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("BadgeApprovalFailed"));
            throw;
        }
    }
}
