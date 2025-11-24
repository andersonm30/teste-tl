using IntegrationHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace IntegrationHub.Infrastructure.Messaging;

/// <summary>
/// Implementação fake/in-memory do Message Bus para desenvolvimento e testes
/// Em produção, substituir por RabbitMQ, Azure Service Bus, etc.
/// </summary>
public class InMemoryMessageBus : IMessageBus, IMessageConsumer
{
    private readonly ILogger<InMemoryMessageBus> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<(object message, string correlationId)>> _queues = new();

    public InMemoryMessageBus(ILogger<InMemoryMessageBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
    {
        return PublishAsync(message, topic, Guid.NewGuid().ToString(), cancellationToken);
    }

    public Task PublishAsync<T>(T message, string topic, string correlationId, CancellationToken cancellationToken = default) where T : class
    {
        if (message == null) throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic cannot be null or empty", nameof(topic));

        var queue = _queues.GetOrAdd(topic, _ => new ConcurrentQueue<(object, string)>());
        queue.Enqueue((message, correlationId));

        _logger.LogInformation(
            "Message published to topic '{Topic}'. Type: {MessageType}, CorrelationId: {CorrelationId}",
            topic, typeof(T).Name, correlationId);

        // Simulação: em produção, aqui iria para RabbitMQ ou similar
        _logger.LogDebug("Message payload: {Payload}", JsonSerializer.Serialize(message));

        return Task.CompletedTask;
    }

    public async Task ConsumeAsync<T>(string topic, Func<T, string, Task> handler, CancellationToken cancellationToken = default) where T : class
    {
        if (string.IsNullOrWhiteSpace(topic)) throw new ArgumentException("Topic cannot be null or empty", nameof(topic));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        _logger.LogInformation("Starting to consume messages from topic '{Topic}'", topic);

        var queue = _queues.GetOrAdd(topic, _ => new ConcurrentQueue<(object, string)>());

        while (!cancellationToken.IsCancellationRequested)
        {
            if (queue.TryDequeue(out var item))
            {
                try
                {
                    if (item.message is T typedMessage)
                    {
                        _logger.LogInformation(
                            "Processing message from topic '{Topic}'. CorrelationId: {CorrelationId}",
                            topic, item.correlationId);

                        await handler(typedMessage, item.correlationId);

                        _logger.LogInformation(
                            "Message processed successfully. CorrelationId: {CorrelationId}",
                            item.correlationId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Message type mismatch. Expected: {Expected}, Actual: {Actual}",
                            typeof(T).Name, item.message.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error processing message from topic '{Topic}'. CorrelationId: {CorrelationId}",
                        topic, item.correlationId);
                    
                    // Em produção: implementar dead-letter queue, retry policy, etc.
                }
            }
            else
            {
                // Aguarda um pouco antes de verificar novamente (polling)
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogInformation("Stopped consuming messages from topic '{Topic}'", topic);
    }
}
