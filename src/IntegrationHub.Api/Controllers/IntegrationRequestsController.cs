using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Controller para gerenciar requisições de integração
/// </summary>
[ApiController]
[Route("api/integration-requests")]
[Produces("application/json")]
public class IntegrationRequestsController : ControllerBase
{
    private readonly IIntegrationRequestService _service;
    private readonly ILogger<IntegrationRequestsController> _logger;

    public IntegrationRequestsController(
        IIntegrationRequestService service,
        ILogger<IntegrationRequestsController> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Cria uma nova requisição de integração
    /// </summary>
    /// <param name="command">Dados da requisição</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Requisição criada com status 202 Accepted</returns>
    [HttpPost]
    [ProducesResponseType(typeof(IntegrationRequestDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateIntegrationRequest(
        [FromBody] CreateIntegrationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Received request to create integration. ExternalId: {ExternalId}, CorrelationId: {CorrelationId}",
            command.ExternalId, correlationId);

        var result = await _service.CreateAsync(command, correlationId, cancellationToken);

        return AcceptedAtAction(
            nameof(GetIntegrationRequest),
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Consulta uma requisição de integração por Id
    /// </summary>
    /// <param name="id">Id da requisição</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Dados da requisição</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(IntegrationRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIntegrationRequest(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);

        if (result == null)
        {
            return NotFound(new { message = $"Integration request with Id '{id}' not found." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Lista todas as requisições de integração
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de requisições</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<IntegrationRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllIntegrationRequests(CancellationToken cancellationToken)
    {
        var results = await _service.GetAllAsync(cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Endpoint de callback para receber atualizações de sistemas externos
    /// </summary>
    /// <param name="id">Id da requisição</param>
    /// <param name="payload">Dados do callback</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Status da atualização</returns>
    [HttpPatch("{id:guid}/callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveCallback(
        Guid id,
        [FromBody] object payload,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Received callback for integration request {Id}. CorrelationId: {CorrelationId}",
            id, correlationId);

        // Aqui você implementaria a lógica de processar o callback
        // Por exemplo: atualizar status, processar resultado, etc.

        var request = await _service.GetByIdAsync(id, cancellationToken);
        
        if (request == null)
        {
            return NotFound(new { message = $"Integration request with Id '{id}' not found." });
        }

        return Ok(new { message = "Callback received successfully", requestId = id, correlationId });
    }
}
