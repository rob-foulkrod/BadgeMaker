namespace BadgeMaker.Components.Models
{
    public class ServiceBusConfig
    {
        public virtual string? queueName { get; set; }

        public virtual string? endpoint { get; set; }
        public virtual bool IsConfigured => !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(queueName);
    }
}