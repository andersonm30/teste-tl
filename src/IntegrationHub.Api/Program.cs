using System.Text;
using IntegrationHub.Api.Middleware;
using IntegrationHub.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

// Configuração do Serilog
DependencyInjection.ConfigureSerilog(new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build());

var builder = WebApplication.CreateBuilder(args);

// Adiciona Serilog ao pipeline de logging
builder.Host.UseSerilog();

// ========================================
// CONFIGURAÇÃO DE SERVIÇOS
// ========================================

// Controllers
builder.Services.AddControllers();

// Configuração de API versioning (preparado para evolução)
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI com configuração de segurança JWT
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Integration Hub API",
        Version = "v1",
        Description = "API de orquestração e integração entre sistemas - TOTVS Tecfin",
        Contact = new OpenApiContact
        {
            Name = "Integration Hub Team",
            Email = "integrationhub@totvs.com.br"
        }
    });

    // Configuração de autenticação JWT no Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Incluir comentários XML (se configurado)
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Autenticação JWT (configuração simplificada para PoC)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MyVerySecretKeyForIntegrationHubTOTVSTecfin2024!@#";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "IntegrationHub";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "IntegrationHubClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("JWT authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Log.Debug("JWT token validated for user: {User}", context.Principal?.Identity?.Name);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// CORS (se necessário para frontends)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Injeção de dependências da Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// Health Checks
builder.Services.AddHealthChecks();

// ========================================
// BUILD DA APLICAÇÃO
// ========================================

var app = builder.Build();

// ========================================
// PIPELINE DE MIDDLEWARES
// ========================================

// Middleware de exceção global (deve ser um dos primeiros)
app.UseGlobalExceptionHandler();

// Middleware de CorrelationId
app.UseCorrelationId();

// Serilog request logging
app.UseSerilogRequestLogging();

// Swagger (habilitado em todos os ambientes para PoC)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Integration Hub API v1");
    options.RoutePrefix = string.Empty; // Swagger na raiz
});

// CORS
app.UseCors("AllowAll");

// HTTPS Redirection
app.UseHttpsRedirection();

// Autenticação e Autorização
app.UseAuthentication();
app.UseAuthorization();

// Mapeamento de controllers
app.MapControllers();

// Health checks endpoint
app.MapHealthChecks("/health");

// ========================================
// INICIALIZAÇÃO
// ========================================

Log.Information("Starting Integration Hub API...");
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);

try
{
    app.Run();
    Log.Information("Integration Hub API stopped gracefully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Integration Hub API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
