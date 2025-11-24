using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;
using IntegrationHub.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação do repositório de requisições de integração usando EF Core
/// </summary>
public class IntegrationRequestRepository : IIntegrationRequestRepository
{
    private readonly IntegrationHubDbContext _context;
    private readonly ILogger<IntegrationRequestRepository> _logger;

    public IntegrationRequestRepository(
        IntegrationHubDbContext context,
        ILogger<IntegrationRequestRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IntegrationRequest> AddAsync(IntegrationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Adding new integration request with Id: {Id}", request.Id);
        
        await _context.IntegrationRequests.AddAsync(request, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        
        return request;
    }

    public async Task<IntegrationRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching integration request by Id: {Id}", id);
        
        return await _context.IntegrationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IntegrationRequest?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching integration request by ExternalId: {ExternalId}", externalId);
        
        return await _context.IntegrationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ExternalId == externalId, cancellationToken);
    }

    public async Task UpdateAsync(IntegrationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating integration request with Id: {Id}", request.Id);
        
        _context.IntegrationRequests.Update(request);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<IntegrationRequest>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching all integration requests");
        
        return await _context.IntegrationRequests
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<IntegrationRequest>> GetByStatusAsync(IntegrationStatus status, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching integration requests by Status: {Status}", status);
        
        return await _context.IntegrationRequests
            .AsNoTracking()
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
