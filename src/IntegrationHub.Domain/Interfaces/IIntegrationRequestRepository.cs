using IntegrationHub.Domain.Entities;

namespace IntegrationHub.Domain.Interfaces;

/// <summary>
/// Interface do repositório de requisições de integração
/// </summary>
public interface IIntegrationRequestRepository
{
    /// <summary>
    /// Adiciona uma nova requisição
    /// </summary>
    Task<IntegrationRequest> AddAsync(IntegrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma requisição por Id
    /// </summary>
    Task<IntegrationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca uma requisição por ExternalId
    /// </summary>
    Task<IntegrationRequest?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atualiza uma requisição existente
    /// </summary>
    Task UpdateAsync(IntegrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todas as requisições
    /// </summary>
    Task<IEnumerable<IntegrationRequest>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista requisições por status
    /// </summary>
    Task<IEnumerable<IntegrationRequest>> GetByStatusAsync(Enums.IntegrationStatus status, CancellationToken cancellationToken = default);
}
