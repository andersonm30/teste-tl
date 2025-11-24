using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Interfaces;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Events;
using IntegrationHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntegrationHub.Application.Services;

/// <summary>
/// Implementação do serviço de aplicação para requisições de integração
/// </summary>
public class IntegrationRequestService : IIntegrationRequestService
{
    private readonly IIntegrationRequestRepository _repository;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<IntegrationRequestService> _logger;

    public IntegrationRequestService(
        IIntegrationRequestRepository repository,
        IMessageBus messageBus,
        ILogger<IntegrationRequestService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IntegrationRequestDto> CreateAsync(
        CreateIntegrationRequestCommand command,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating integration request. ExternalId: {ExternalId}, Source: {Source}, Target: {Target}, CorrelationId: {CorrelationId}",
            command.ExternalId, command.SourceSystem, command.TargetSystem, correlationId);

        // Cria a entidade de domínio
        var request = new IntegrationRequest(
            command.ExternalId,
            command.SourceSystem,
            command.TargetSystem,
            command.Payload,
            correlationId);

        // Persiste no repositório
        var savedRequest = await _repository.AddAsync(request, cancellationToken);

        // Publica evento de domínio no message bus
        var domainEvent = new IntegrationRequestCreated(
            savedRequest.Id,
            savedRequest.ExternalId,
            savedRequest.SourceSystem,
            savedRequest.TargetSystem,
            savedRequest.CorrelationId);

        await _messageBus.PublishAsync(
            domainEvent,
            "integration-request-created",
            savedRequest.CorrelationId,
            cancellationToken);

        _logger.LogInformation(
            "Integration request created successfully. Id: {Id}, CorrelationId: {CorrelationId}",
            savedRequest.Id, correlationId);

        return MapToDto(savedRequest);
    }

    public async Task<IntegrationRequestDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching integration request by Id: {Id}", id);
        
        var request = await _repository.GetByIdAsync(id, cancellationToken);
        
        return request != null ? MapToDto(request) : null;
    }

    public async Task<IntegrationRequestDto?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching integration request by ExternalId: {ExternalId}", externalId);
        
        var request = await _repository.GetByExternalIdAsync(externalId, cancellationToken);
        
        return request != null ? MapToDto(request) : null;
    }

    public async Task<IEnumerable<IntegrationRequestDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching all integration requests");
        
        var requests = await _repository.GetAllAsync(cancellationToken);
        
        return requests.Select(MapToDto);
    }

    private static IntegrationRequestDto MapToDto(IntegrationRequest request)
    {
        return new IntegrationRequestDto
        {
            Id = request.Id,
            ExternalId = request.ExternalId,
            SourceSystem = request.SourceSystem,
            TargetSystem = request.TargetSystem,
            Payload = request.Payload,
            Status = request.Status.ToString(),
            CorrelationId = request.CorrelationId,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt,
            ErrorMessage = request.ErrorMessage
        };
    }
}
