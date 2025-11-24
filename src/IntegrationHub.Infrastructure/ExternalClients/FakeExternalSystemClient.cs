using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.ExternalClients;

/// <summary>
/// Interface para comunicação com sistemas externos (Totvs, parceiros, etc.)
/// </summary>
public interface IExternalSystemClient
{
    Task<bool> SendDataAsync(string targetSystem, string payload, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementação fake de cliente para sistemas externos
/// Em produção, implementar clients específicos para cada sistema
/// </summary>
public class FakeExternalSystemClient : IExternalSystemClient
{
    private readonly ILogger<FakeExternalSystemClient> _logger;

    public FakeExternalSystemClient(ILogger<FakeExternalSystemClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SendDataAsync(string targetSystem, string payload, string correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending data to external system '{TargetSystem}'. CorrelationId: {CorrelationId}",
            targetSystem, correlationId);

        // Simulação de chamada HTTP para sistema externo
        await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

        // Simulação: 90% de sucesso
        var success = Random.Shared.Next(100) < 90;

        if (success)
        {
            _logger.LogInformation(
                "Data sent successfully to '{TargetSystem}'. CorrelationId: {CorrelationId}",
                targetSystem, correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to send data to '{TargetSystem}'. CorrelationId: {CorrelationId}",
                targetSystem, correlationId);
        }

        return success;
    }
}
