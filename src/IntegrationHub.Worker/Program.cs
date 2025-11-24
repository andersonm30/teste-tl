using IntegrationHub.Infrastructure;
using IntegrationHub.Worker;
using Serilog;

// Configuração do Serilog
DependencyInjection.ConfigureSerilog(new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build());

Log.Information("Starting Integration Hub Worker...");

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Adiciona Serilog
    builder.Services.AddSerilog();

    // Adiciona a infraestrutura (repositórios, message bus, etc.)
    builder.Services.AddInfrastructure(builder.Configuration);

    // Adiciona o Worker de orquestração
    builder.Services.AddHostedService<IntegrationOrchestrationWorker>();

    var host = builder.Build();

    Log.Information("Integration Hub Worker started successfully");
    await host.RunAsync();
    
    Log.Information("Integration Hub Worker stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Integration Hub Worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
