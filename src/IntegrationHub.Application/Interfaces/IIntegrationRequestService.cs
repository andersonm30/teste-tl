using IntegrationHub.Application.DTOs;

namespace IntegrationHub.Application.Interfaces;

/// <summary>
/// Interface de serviço de aplicação para requisições de integração
/// </summary>
public interface IIntegrationRequestService
{
    /// <summary>
    /// Cria uma nova requisição de integração
    /// </summary>
    Task<IntegrationRequestDto> CreateAsync(CreateIntegrationRequestCommand command, string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma requisição por Id
    /// </summary>
    Task<IntegrationRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma requisição por ExternalId
    /// </summary>
    Task<IntegrationRequestDto?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas as requisições
    /// </summary>
    Task<IEnumerable<IntegrationRequestDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
