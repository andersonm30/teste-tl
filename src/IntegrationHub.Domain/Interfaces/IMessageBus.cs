namespace IntegrationHub.Domain.Interfaces;

/// <summary>
/// Interface genérica para publicação e consumo de mensagens (Message Bus / Mensageria)
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publica uma mensagem no bus
    /// </summary>
    Task PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publica uma mensagem com CorrelationId
    /// </summary>
    Task PublishAsync<T>(T message, string topic, string correlationId, CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// Interface para consumo de mensagens (utilizado pelo Worker)
/// </summary>
public interface IMessageConsumer
{
    /// <summary>
    /// Inicia o consumo de mensagens de um tópico/fila
    /// </summary>
    Task ConsumeAsync<T>(string topic, Func<T, string, Task> handler, CancellationToken cancellationToken = default) where T : class;
}
