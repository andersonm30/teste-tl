using IntegrationHub.Application.Interfaces;
using IntegrationHub.Application.Services;
using IntegrationHub.Domain.Interfaces;
using IntegrationHub.Infrastructure.ExternalClients;
using IntegrationHub.Infrastructure.Messaging;
using IntegrationHub.Infrastructure.Persistence;
using IntegrationHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace IntegrationHub.Infrastructure;

/// <summary>
/// Configuração de Dependency Injection para a camada de Infrastructure
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuração do banco de dados (em memória por padrão, SQL Server em produção)
        var useInMemoryDb = configuration.GetValue<bool>("Database:UseInMemory", true);
        
        if (useInMemoryDb)
        {
            services.AddDbContext<IntegrationHubDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationHubDb")
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            services.AddDbContext<IntegrationHubDbContext>(options =>
                options.UseSqlServer(connectionString,
                    sqlOptions => sqlOptions.EnableRetryOnFailure()));
        }

        // Repositórios
        services.AddScoped<IIntegrationRequestRepository, IntegrationRequestRepository>();

        // Message Bus (em memória para desenvolvimento, RabbitMQ em produção)
        services.AddSingleton<InMemoryMessageBus>();
        services.AddSingleton<IMessageBus>(sp => sp.GetRequiredService<InMemoryMessageBus>());
        services.AddSingleton<IMessageConsumer>(sp => sp.GetRequiredService<InMemoryMessageBus>());

        // External Clients
        services.AddScoped<IExternalSystemClient, FakeExternalSystemClient>();

        // Application Services
        services.AddScoped<IIntegrationRequestService, IntegrationRequestService>();

        // OpenTelemetry Configuration
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("IntegrationHub"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }

    /// <summary>
    /// Configuração do Serilog para logs estruturados
    /// </summary>
    public static void ConfigureSerilog(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "IntegrationHub")
            .Enrich.WithMachineName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/integration-hub-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
