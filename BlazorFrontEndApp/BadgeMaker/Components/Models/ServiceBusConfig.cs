namespace BadgeMaker.Components.Models
{
    public class ServiceBusConfig
    {
        public virtual string connectionString { get; set; }
        public virtual string queueName { get; set; }
        public virtual bool IsConfigured => !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(queueName);
    }
}