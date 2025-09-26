namespace BadgeMaker.Components.Models
{
    public class ServiceBusConfig
    {
    public virtual string queueName { get; set; } = string.Empty;

    public virtual string endpoint { get; set; } = string.Empty;
        public virtual bool IsConfigured => !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(queueName);
    }
}