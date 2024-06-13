namespace BadgeMaker.Components.Models
{
    public class ServiceBusConfig
    {
        public string connectionString { get; set; }
        public string queueName { get; set; }
        public bool IsConfigured => !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(queueName);
    }
}