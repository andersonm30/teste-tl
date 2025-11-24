using System.Diagnostics;

namespace IntegrationHub.Api.Middleware;

/// <summary>
/// Middleware para gerenciar CorrelationId em toda a requisição
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Tenta obter CorrelationId do header, senão gera um novo
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // Adiciona ao response header
        context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);

        // Adiciona ao HttpContext para acesso posterior
        context.Items["CorrelationId"] = correlationId;

        // Adiciona à Activity (para OpenTelemetry)
        Activity.Current?.SetTag("correlation_id", correlationId);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Extension method para facilitar uso do middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
