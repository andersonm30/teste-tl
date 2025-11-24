using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntegrationHub.Api.Controllers;

/// <summary>
/// Controller para health checks
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Health check público
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "IntegrationHub.Api"
        });
    }

    /// <summary>
    /// Health check protegido (exemplo de endpoint com autenticação JWT)
    /// </summary>
    [HttpGet("secure")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetSecureHealth()
    {
        var userId = User.Identity?.Name ?? "Unknown";
        
        _logger.LogInformation("Secure health check accessed by user: {UserId}", userId);

        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            service = "IntegrationHub.Api",
            user = userId
        });
    }
}
