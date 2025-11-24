using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Entities;

/// <summary>
/// Entidade principal que representa uma requisição de integração entre sistemas
/// </summary>
public class IntegrationRequest
{
    /// <summary>
    /// Identificador único interno
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identificador externo fornecido pelo sistema parceiro
    /// </summary>
    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>
    /// Sistema de origem da requisição
    /// </summary>
    public string SourceSystem { get; private set; } = string.Empty;

    /// <summary>
    /// Sistema de destino da requisição
    /// </summary>
    public string TargetSystem { get; private set; } = string.Empty;

    /// <summary>
    /// Payload JSON bruto da requisição
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    /// <summary>
    /// Status atual da requisição no workflow
    /// </summary>
    public IntegrationStatus Status { get; private set; }

    /// <summary>
    /// CorrelationId para rastreabilidade distribuída
    /// </summary>
    public string CorrelationId { get; private set; } = string.Empty;

    /// <summary>
    /// Data/hora de criação da requisição
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Data/hora da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Mensagem de erro caso a requisição tenha falha
    /// </summary>
    public string? ErrorMessage { get; private set; }

    // Construtor para EF Core
    private IntegrationRequest() { }

    /// <summary>
    /// Construtor para criação de nova requisição
    /// </summary>
    public IntegrationRequest(
        string externalId,
        string sourceSystem,
        string targetSystem,
        string payload,
        string correlationId)
    {
        Id = Guid.NewGuid();
        ExternalId = externalId ?? throw new ArgumentNullException(nameof(externalId));
        SourceSystem = sourceSystem ?? throw new ArgumentNullException(nameof(sourceSystem));
        TargetSystem = targetSystem ?? throw new ArgumentNullException(nameof(targetSystem));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        Status = IntegrationStatus.Received;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Atualiza o status da requisição
    /// </summary>
    public void UpdateStatus(IntegrationStatus newStatus, string? errorMessage = null)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Marca a requisição como processando
    /// </summary>
    public void MarkAsProcessing()
    {
        UpdateStatus(IntegrationStatus.Processing);
    }

    /// <summary>
    /// Marca a requisição como aguardando resposta externa
    /// </summary>
    public void MarkAsWaitingExternal()
    {
        UpdateStatus(IntegrationStatus.WaitingExternal);
    }

    /// <summary>
    /// Marca a requisição como completada
    /// </summary>
    public void MarkAsCompleted()
    {
        UpdateStatus(IntegrationStatus.Completed);
    }

    /// <summary>
    /// Marca a requisição como falha
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        UpdateStatus(IntegrationStatus.Failed, errorMessage);
    }
}
