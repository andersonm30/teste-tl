namespace IntegrationHub.Domain.Enums;

/// <summary>
/// Status possíveis de uma requisição de integração no workflow
/// </summary>
public enum IntegrationStatus
{
    /// <summary>
    /// Requisição recebida e aguardando processamento
    /// </summary>
    Received = 0,

    /// <summary>
    /// Requisição em processamento interno
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Aguardando resposta de sistema externo
    /// </summary>
    WaitingExternal = 2,

    /// <summary>
    /// Requisição completada com sucesso
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Requisição falhou
    /// </summary>
    Failed = 4
}
