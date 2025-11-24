# Segurança - Integration Hub

## 1. Visão Geral

A segurança do Integration Hub é baseada em múltiplas camadas de proteção, seguindo o princípio de **Defense in Depth**. Este documento detalha controles de autenticação, autorização, criptografia, validação de entrada e proteção contra ataques comuns.

---

## 2. Autenticação e Autorização

### 2.1. JWT Bearer Authentication

**Implementação:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"])
            ),
            ClockSkew = TimeSpan.Zero // Remove 5min de tolerância padrão
        };
    });
```

**Estrutura do Token:**
```json
{
  "header": {
    "alg": "HS256",
    "typ": "JWT"
  },
  "payload": {
    "sub": "partner_sap_001",
    "name": "SAP Integration User",
    "role": "Partner",
    "client_id": "sap-integration",
    "iat": 1705320000,
    "exp": 1705323600, // 1 hora de validade
    "iss": "https://integrationhub.totvs.com.br",
    "aud": "https://api.integrationhub.totvs.com.br"
  },
  "signature": "..."
}
```

**Requisição Autenticada:**
```http
POST /api/integration-requests
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "externalId": "PARTNER-12345",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus",
  "payload": {...}
}
```

**Resposta para Token Inválido:**
```http
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Bearer error="invalid_token"
```

---

### 2.2. Roles e Claims

**Roles Implementadas:**

| Role | Permissões |
|------|-----------|
| **Partner** | Criar requisições, consultar próprias requisições |
| **Admin** | Todas as permissões + acesso a dashboards |
| **ReadOnly** | Apenas consultas (analytics, auditoria) |

**Autorização por Role:**
```csharp
[Authorize(Roles = "Partner,Admin")]
public class IntegrationRequestsController : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Partner,Admin")] // Criar requisição
    public async Task<IActionResult> Create([FromBody] CreateIntegrationRequestCommand command)
    {
        // ...
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Partner,Admin,ReadOnly")] // Consultar
    public async Task<IActionResult> GetById(Guid id)
    {
        // Validar se parceiro só acessa próprias requisições
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // ...
    }
}
```

**Isolamento de Dados por Parceiro:**
```csharp
// Parceiro só vê próprias requisições
public async Task<IntegrationRequestDto?> GetByIdAsync(Guid id, string userId)
{
    var request = await _repository.GetByIdAsync(id);
    
    // Verifica se usuário é admin ou dono da requisição
    if (!User.IsInRole("Admin") && request.CreatedBy != userId)
    {
        throw new UnauthorizedAccessException("Access denied");
    }
    
    return request;
}
```

---

### 2.3. Identity Provider (Produção)

**Recomendação:** Azure AD B2C / Identity Server / Keycloak

**Fluxo OAuth 2.0 (Client Credentials Grant):**
```
1. Parceiro obtém token:
   POST https://auth.integrationhub.totvs.com.br/oauth/token
   Content-Type: application/x-www-form-urlencoded
   
   grant_type=client_credentials
   &client_id=sap-integration
   &client_secret={secret}
   &scope=integration.write

2. Identity Provider responde:
   {
     "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
     "token_type": "Bearer",
     "expires_in": 3600
   }

3. Parceiro usa token para chamar API:
   POST /api/integration-requests
   Authorization: Bearer {access_token}
```

**Benefícios:**
- ✅ Gestão centralizada de credenciais
- ✅ Revogação de tokens
- ✅ Auditoria de acessos
- ✅ Suporte a MFA (Multi-Factor Authentication)

---

## 3. Criptografia

### 3.1. TLS 1.3 (Transport Layer Security)

**Configuração (Kestrel):**
```csharp
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = SslProtocols.Tls13;
    });
});
```

**Políticas:**
- ✅ TLS 1.3 obrigatório (TLS 1.0/1.1/1.2 desabilitados)
- ✅ Certificados emitidos por CA confiável (Let's Encrypt / DigiCert)
- ✅ HSTS (HTTP Strict Transport Security) habilitado

**Header HSTS:**
```http
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

---

### 3.2. Encryption at Rest

**Banco de Dados (SQL Server):**
```sql
-- Transparent Data Encryption (TDE)
USE master;
CREATE MASTER KEY ENCRYPTION BY PASSWORD = '{strong-password}';

CREATE CERTIFICATE TDE_Cert WITH SUBJECT = 'Integration Hub TDE Certificate';

USE IntegrationHubDb;
CREATE DATABASE ENCRYPTION KEY
WITH ALGORITHM = AES_256
ENCRYPTION BY SERVER CERTIFICATE TDE_Cert;

ALTER DATABASE IntegrationHubDb
SET ENCRYPTION ON;
```

**Resultado:** Todos os dados (tabelas, índices, logs) são criptografados em disco.

---

### 3.3. Secret Management

**Azure Key Vault:**
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://integrationhub-keyvault.vault.azure.net/"),
    new DefaultAzureCredential()
);

// Uso
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]; // Lido do Key Vault
var dbConnectionString = builder.Configuration["ConnectionStrings:SqlServer"];
```

**Secrets Armazenados:**
- JWT Secret Key
- Database Connection String
- Credenciais de sistemas externos (API keys)
- Certificados TLS

**Benefícios:**
- ✅ Secrets não ficam em código ou appsettings.json
- ✅ Rotação automática de secrets
- ✅ Auditoria de acessos
- ✅ Controle de acesso via RBAC

---

## 4. Validação de Entrada

### 4.1. Data Annotations

```csharp
public class CreateIntegrationRequestCommand
{
    [Required(ErrorMessage = "ExternalId is required")]
    [MaxLength(100, ErrorMessage = "ExternalId cannot exceed 100 characters")]
    public string ExternalId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string SourceSystem { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string TargetSystem { get; set; } = string.Empty;

    [Required]
    public object Payload { get; set; } = new();
}
```

**Validação Automática:**
ASP.NET Core valida automaticamente antes de chamar controller action. Se inválido, retorna `400 Bad Request`.

---

### 4.2. FluentValidation (Futuro)

```csharp
public class CreateIntegrationRequestValidator : AbstractValidator<CreateIntegrationRequestCommand>
{
    public CreateIntegrationRequestValidator()
    {
        RuleFor(x => x.ExternalId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches(@"^[A-Z0-9\-]+$") // Apenas alfanuméricos e hífen
            .WithMessage("ExternalId must contain only uppercase letters, numbers, and hyphens");

        RuleFor(x => x.SourceSystem)
            .NotEmpty()
            .Must(BeValidSystem)
            .WithMessage("Invalid SourceSystem");

        RuleFor(x => x.Payload)
            .NotNull()
            .Must(BeValidJson)
            .WithMessage("Payload must be valid JSON");
    }

    private bool BeValidSystem(string system)
    {
        var validSystems = new[] { "SAP", "Salesforce", "Legacy" };
        return validSystems.Contains(system);
    }

    private bool BeValidJson(object payload)
    {
        try
        {
            JsonSerializer.Serialize(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

---

### 4.3. Sanitização de Entrada

**Anti-XSS (Cross-Site Scripting):**
```csharp
public class SanitizeInputAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is string input)
            {
                // Remove HTML tags
                var sanitized = Regex.Replace(input, @"<[^>]+>", string.Empty);
                
                // Escapa caracteres especiais
                sanitized = HtmlEncoder.Default.Encode(sanitized);
            }
        }
        
        base.OnActionExecuting(context);
    }
}
```

---

## 5. Proteção Contra Ataques

### 5.1. SQL Injection

**Proteção:** Entity Framework Core usa queries parametrizadas automaticamente.

**Exemplo Seguro:**
```csharp
// EF Core gera: SELECT * FROM IntegrationRequests WHERE ExternalId = @p0
var request = await _context.IntegrationRequests
    .FirstOrDefaultAsync(r => r.ExternalId == externalId);
```

**❌ NUNCA FAÇA:**
```csharp
// VULNERÁVEL A SQL INJECTION!
var sql = $"SELECT * FROM IntegrationRequests WHERE ExternalId = '{externalId}'";
var request = _context.IntegrationRequests.FromSqlRaw(sql).FirstOrDefault();
```

---

### 5.2. Cross-Site Request Forgery (CSRF)

**Proteção:** Tokens CSRF para endpoints que modificam estado (não aplicável a APIs REST stateless com JWT).

**CORS Configurado:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowTrustedOrigins", policy =>
    {
        policy.WithOrigins(
                "https://partner.sap.com",
                "https://partner.salesforce.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

app.UseCors("AllowTrustedOrigins");
```

**Política Restritiva:** Apenas origens explicitamente autorizadas podem chamar a API.

---

### 5.3. Denial of Service (DoS)

#### 5.3.1. Rate Limiting (AspNetCoreRateLimit)

```csharp
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 60 // 60 requisições por minuto
        },
        new RateLimitRule
        {
            Endpoint = "POST:/api/integration-requests",
            Period = "1m",
            Limit = 10 // 10 criações por minuto
        }
    };
});

builder.Services.AddInMemoryRateLimiting();
app.UseIpRateLimiting();
```

**Resposta ao Exceder Limite:**
```http
HTTP/1.1 429 Too Many Requests
Retry-After: 60

{
  "error": "Rate limit exceeded. Try again in 60 seconds."
}
```

---

#### 5.3.2. Request Size Limit

```csharp
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 5 * 1024 * 1024; // 5MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024;
});
```

---

### 5.4. XML External Entity (XXE)

**Proteção:** Aplicação usa JSON (não XML). Se necessário processar XML no futuro:

```csharp
var settings = new XmlReaderSettings
{
    DtdProcessing = DtdProcessing.Prohibit, // Bloqueia DTDs
    XmlResolver = null // Bloqueia resoluções externas
};

using var reader = XmlReader.Create(stream, settings);
```

---

### 5.5. Mass Assignment

**Proteção:** DTOs separados de entidades de domínio.

**Exemplo:**
```csharp
// ✅ CORRETO: Cliente envia apenas DTO
public class CreateIntegrationRequestCommand
{
    public string ExternalId { get; set; }
    public string SourceSystem { get; set; }
    public string TargetSystem { get; set; }
    public object Payload { get; set; }
    // Status, CreatedAt, UpdatedAt não são expostos no DTO
}

// ❌ ERRADO: Mapear diretamente para entidade
[HttpPost]
public async Task<IActionResult> Create([FromBody] IntegrationRequest request)
{
    // Cliente poderia enviar Status="Completed" e burlar workflow
    await _repository.AddAsync(request);
}
```

---

## 6. Auditoria e Compliance

### 6.1. Logs de Auditoria

**Eventos Auditados:**
- Autenticação bem-sucedida/falhada
- Criação de requisições de integração
- Alterações de status
- Acessos negados (403 Forbidden)
- Acessos a dados sensíveis

**Formato de Log:**
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "eventType": "AUDIT",
  "action": "CREATE_INTEGRATION_REQUEST",
  "userId": "partner_sap_001",
  "clientId": "sap-integration",
  "ipAddress": "203.0.113.45",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "resourceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "result": "SUCCESS"
}
```

**Retenção:** 7 anos (conformidade com regulamentações financeiras).

---

### 6.2. LGPD / GDPR Compliance

**Dados Pessoais:**
Payloads podem conter PII (Personally Identifiable Information). Estratégias:

1. **Criptografia de Campo:**
   ```csharp
   [Encrypted] // Custom attribute + EF Core Value Converter
   public string CustomerDocument { get; set; }
   ```

2. **Anonimização de Logs:**
   ```csharp
   _logger.LogInformation(
       "Request created | ExternalId={ExternalId} | Customer={Customer}",
       request.ExternalId,
       MaskPII(customer.Name) // "João Silva" → "J*** S***"
   );
   ```

3. **Direito ao Esquecimento:**
   Endpoint para deletar dados de um cliente:
   ```csharp
   [HttpDelete("gdpr/customer/{customerId}")]
   [Authorize(Roles = "Admin")]
   public async Task<IActionResult> DeleteCustomerData(string customerId)
   {
       await _service.AnonymizeCustomerDataAsync(customerId);
       return NoContent();
   }
   ```

---

### 6.3. PCI-DSS (Se Processar Pagamentos)

**Requisitos:**
- ✅ Dados de cartão **nunca** armazenados em texto claro
- ✅ Tokenização via gateway de pagamento (Stripe, Adyen)
- ✅ Logs não contêm números de cartão

**Exemplo de Payload Seguro:**
```json
{
  "orderId": "ORD-2024-001",
  "paymentToken": "tok_1A2B3C4D", // Token do gateway, não o número do cartão
  "amount": 15000.00
}
```

---

## 7. Segurança de Infraestrutura

### 7.1. Network Security

**Azure:**
- ✅ Virtual Network (VNet) com subnets isoladas
- ✅ Network Security Groups (NSG) com regras restritivas
- ✅ Azure Private Link para banco de dados
- ✅ Azure Application Gateway com WAF (Web Application Firewall)

**Regras NSG:**
```
Inbound:
- Porta 443 (HTTPS) → Permitir de Internet
- Porta 5432 (PostgreSQL) → Permitir apenas de subnet da API
- Todas as outras → Negar

Outbound:
- Porta 443 (HTTPS) → Permitir para sistemas externos
- Porta 5671 (RabbitMQ TLS) → Permitir para subnet do Message Bus
- Todas as outras → Negar
```

---

### 7.2. Container Security

**Dockerfile Hardening:**
```dockerfile
# 1. Usar imagem base oficial e minimal
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base

# 2. Criar usuário não-root
RUN adduser -D -u 1000 appuser
USER appuser

# 3. Expor apenas porta necessária
EXPOSE 8080

# 4. Não incluir ferramentas de debug em produção
# (sem vim, curl, wget)

# 5. Verificar vulnerabilidades antes do deploy
# docker scan integrationhub-api:latest
```

**Kubernetes Security:**
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: integration-hub-api
spec:
  securityContext:
    runAsNonRoot: true
    runAsUser: 1000
    fsGroup: 1000
  containers:
    - name: api
      image: integrationhub-api:latest
      securityContext:
        allowPrivilegeEscalation: false
        readOnlyRootFilesystem: true
        capabilities:
          drop:
            - ALL
```

---

### 7.3. Secrets Rotation

**Azure Key Vault Auto-Rotation:**
```csharp
// Configurar política de rotação a cada 90 dias
az keyvault secret set-attributes \
  --vault-name integrationhub-keyvault \
  --name JwtSecretKey \
  --expires $(date -d "+90 days" -u +"%Y-%m-%dT%H:%M:%SZ")
```

**Rotação sem Downtime:**
1. Novo secret é criado com sufixo de versão
2. Aplicação suporta N e N-1 (período de transição)
3. Após validação, secret antigo é desabilitado

---

## 8. Resposta a Incidentes

### 8.1. Plano de Resposta

**Fases:**
1. **Detecção:** Alertas automáticos (ex: 100 falhas de autenticação em 1 minuto)
2. **Contenção:** Bloquear IP atacante via NSG
3. **Erradicação:** Identificar e corrigir vulnerabilidade
4. **Recuperação:** Restaurar serviço normal
5. **Lições Aprendidas:** Post-mortem e melhorias

---

### 8.2. Contatos de Emergência

| Papel | Responsável | Email | Telefone |
|-------|-------------|-------|----------|
| **Security Lead** | João Silva | joao.silva@totvs.com.br | +55 11 99999-0001 |
| **DevOps Lead** | Maria Santos | maria.santos@totvs.com.br | +55 11 99999-0002 |
| **CTO** | Carlos Lima | carlos.lima@totvs.com.br | +55 11 99999-0003 |

---

## 9. Checklist de Segurança

### 9.1. Pré-Deploy

- [ ] Secrets movidos para Azure Key Vault
- [ ] TLS 1.3 configurado
- [ ] Rate limiting testado
- [ ] CORS restritivo configurado
- [ ] Logs de auditoria habilitados
- [ ] Vulnerabilidades do container escaneadas (`docker scan`)
- [ ] Testes de penetração realizados (OWASP ZAP)

---

### 9.2. Pós-Deploy

- [ ] Monitorar logs de autenticação falhada
- [ ] Verificar alertas de segurança (PagerDuty)
- [ ] Revisar dashboards de rate limiting
- [ ] Testar revogação de tokens
- [ ] Simular ataque DoS (controlled load test)

---

## 10. Conclusão

A estratégia de segurança implementada garante:
- ✅ **Autenticação forte** via JWT com Azure AD
- ✅ **Criptografia em trânsito e em repouso** (TLS 1.3 + TDE)
- ✅ **Validação rigorosa de entrada** (FluentValidation + sanitização)
- ✅ **Proteção contra ataques** (SQL Injection, XSS, DoS)
- ✅ **Auditoria completa** para compliance (LGPD, GDPR, PCI-DSS)
- ✅ **Segurança de infraestrutura** (NSG, WAF, Private Link)
