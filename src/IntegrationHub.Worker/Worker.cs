using IntegrationHub.Domain.Enums;
using IntegrationHub.Domain.Events;
using IntegrationHub.Domain.Interfaces;
using IntegrationHub.Infrastructure.ExternalClients;

namespace IntegrationHub.Worker;

/// <summary>
/// Worker responsável por consumir mensagens e orquestrar o workflow de integração
/// </summary>
public class IntegrationOrchestrationWorker : BackgroundService
{
    private readonly ILogger<IntegrationOrchestrationWorker> _logger;
    private readonly IServiceProvider _serviceProvider;

    public IntegrationOrchestrationWorker(
        ILogger<IntegrationOrchestrationWorker> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Integration Orchestration Worker starting...");

        // Aguarda um pouco para garantir que a API já está rodando e o sistema está pronto
        await Task.Delay(2000, stoppingToken);

        try
        {
            // Cria um scope para resolver dependências scoped
            await using var scope = _serviceProvider.CreateAsyncScope();
            var messageConsumer = scope.ServiceProvider.GetRequiredService<IMessageConsumer>();

            _logger.LogInformation("Starting to consume integration request events...");

            // Consome mensagens do tópico de eventos de requisição criada
            await messageConsumer.ConsumeAsync<IntegrationRequestCreated>(
                "integration-request-created",
                async (evt, correlationId) => await ProcessIntegrationRequestAsync(evt, correlationId, stoppingToken),
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Integration Orchestration Worker is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Integration Orchestration Worker");
            throw;
        }
    }

    /// <summary>
    /// Processa uma requisição de integração através do workflow
    /// </summary>
    private async Task ProcessIntegrationRequestAsync(
        IntegrationRequestCreated evt,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing integration request. RequestId: {RequestId}, Source: {Source}, Target: {Target}, CorrelationId: {CorrelationId}",
            evt.RequestId, evt.SourceSystem, evt.TargetSystem, correlationId);

        try
        {
            // Cria um novo scope para cada processamento
            await using var scope = _serviceProvider.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRequestRepository>();
            var externalClient = scope.ServiceProvider.GetRequiredService<IExternalSystemClient>();

            // 1. Busca a requisição no repositório
            var request = await repository.GetByIdAsync(evt.RequestId, cancellationToken);
            
            if (request == null)
            {
                _logger.LogWarning("Integration request {RequestId} not found", evt.RequestId);
                return;
            }

            // 2. Atualiza status para Processing
            _logger.LogInformation("Marking request {RequestId} as Processing", evt.RequestId);
            request.MarkAsProcessing();
            await repository.UpdateAsync(request, cancellationToken);

            // Simulação de processamento interno
            await Task.Delay(Random.Shared.Next(500, 1500), cancellationToken);

            // 3. Marca como aguardando resposta externa
            _logger.LogInformation("Marking request {RequestId} as WaitingExternal", evt.RequestId);
            request.MarkAsWaitingExternal();
            await repository.UpdateAsync(request, cancellationToken);

            // 4. Envia para sistema externo
            _logger.LogInformation(
                "Sending data to external system '{TargetSystem}'. RequestId: {RequestId}",
                evt.TargetSystem, evt.RequestId);

            var success = await externalClient.SendDataAsync(
                evt.TargetSystem,
                request.Payload,
                correlationId,
                cancellationToken);

            // 5. Atualiza status final baseado no resultado
            if (success)
            {
                _logger.LogInformation("Request {RequestId} completed successfully", evt.RequestId);
                request.MarkAsCompleted();
            }
            else
            {
                _logger.LogWarning("Request {RequestId} failed to send to external system", evt.RequestId);
                request.MarkAsFailed($"Failed to send data to external system '{evt.TargetSystem}'");
            }

            await repository.UpdateAsync(request, cancellationToken);

            _logger.LogInformation(
                "Integration request {RequestId} processing finished with status: {Status}",
                evt.RequestId, request.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing integration request {RequestId}. CorrelationId: {CorrelationId}",
                evt.RequestId, correlationId);

            // Tenta marcar como falha
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IIntegrationRequestRepository>();
                var request = await repository.GetByIdAsync(evt.RequestId, cancellationToken);
                
                if (request != null)
                {
                    request.MarkAsFailed($"Internal error: {ex.Message}");
                    await repository.UpdateAsync(request, cancellationToken);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to mark request {RequestId} as failed", evt.RequestId);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Integration Orchestration Worker is stopping...");
        return base.StopAsync(cancellationToken);
    }
}
