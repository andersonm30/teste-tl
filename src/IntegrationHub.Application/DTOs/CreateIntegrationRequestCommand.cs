namespace IntegrationHub.Application.DTOs;

/// <summary>
/// Comando para criar uma nova requisição de integração
/// </summary>
public class CreateIntegrationRequestCommand
{
    /// <summary>
    /// Identificador externo da requisição (fornecido pelo parceiro)
    /// </summary>
    public string ExternalId { get; set; } = string.Empty;

    /// <summary>
    /// Sistema de origem
    /// </summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>
    /// Sistema de destino
    /// </summary>
    public string TargetSystem { get; set; } = string.Empty;

    /// <summary>
    /// Payload JSON da requisição
    /// </summary>
    public string Payload { get; set; } = string.Empty;
}
