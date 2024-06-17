namespace BadgeMaker.Components.Interfaces;

public interface IServiceBusService
{
    bool IsConfigured { get;  }

    Task SendMessageAsync(string messageBody);
}
