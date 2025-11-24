namespace IntegrationHub.Application.DTOs;

/// <summary>
/// DTO de resposta representando uma requisição de integração
/// </summary>
public class IntegrationRequestDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
