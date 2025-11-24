namespace IntegrationHub.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando uma nova requisição de integração é criada
/// </summary>
public class IntegrationRequestCreated
{
    public Guid RequestId { get; }
    public string ExternalId { get; }
    public string SourceSystem { get; }
    public string TargetSystem { get; }
    public string CorrelationId { get; }
    public DateTime OccurredAt { get; }

    public IntegrationRequestCreated(
        Guid requestId,
        string externalId,
        string sourceSystem,
        string targetSystem,
        string correlationId)
    {
        RequestId = requestId;
        ExternalId = externalId;
        SourceSystem = sourceSystem;
        TargetSystem = targetSystem;
        CorrelationId = correlationId;
        OccurredAt = DateTime.UtcNow;
    }
}
