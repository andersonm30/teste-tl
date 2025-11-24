# Comandos Rápidos - Integration Hub

## 🚀 Executar a Aplicação

### 1. Restaurar dependências e compilar
```powershell
dotnet restore
dotnet build
```

### 2. Executar a API (Terminal 1)
```powershell
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj
```
Acesse: https://localhost:7000/ (Swagger UI na raiz)

### 3. Executar o Worker (Terminal 2)
```powershell
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj
```

### 4. Executar os Testes
```powershell
dotnet test
```

---

## 🧪 Testando a API

### Criar uma requisição de integração
```powershell
# PowerShell
$body = @{
    externalId = "EXT-12345"
    sourceSystem = "PartnerA"
    targetSystem = "Totvs"
    payload = '{"customer": "ACME Corp", "value": 1000.00}'
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck
```

### Consultar status da requisição
```powershell
# Substitua {id} pelo ID retornado
Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests/{id}" `
    -Method GET `
    -SkipCertificateCheck
```

### Listar todas as requisições
```powershell
Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method GET `
    -SkipCertificateCheck
```

### Health Check
```powershell
Invoke-RestMethod -Uri "https://localhost:7000/api/health" `
    -Method GET `
    -SkipCertificateCheck
```

---

## 📊 Estrutura de Status

1. **Received** → Requisição criada
2. **Processing** → Worker iniciou processamento  
3. **WaitingExternal** → Aguardando resposta de sistema externo
4. **Completed** / **Failed** → Finalizada

---

## 🔍 Logs

Os logs são gravados em:
- **Console**: Saída estruturada colorida
- **Arquivo**: `logs/integration-hub-*.log` (API)
- **Arquivo**: `logs/integration-hub-worker-*.log` (Worker)

---

## 🏗️ Estrutura da Solution

```
IntegrationHub/
├── src/
│   ├── IntegrationHub.Api          → Web API REST
│   ├── IntegrationHub.Application  → Serviços de aplicação
│   ├── IntegrationHub.Domain       → Entidades e interfaces core
│   ├── IntegrationHub.Infrastructure → Repositórios, DB, Messaging
│   └── IntegrationHub.Worker       → Processamento assíncrono
└── tests/
    ├── IntegrationHub.Api.Tests
    ├── IntegrationHub.Application.Tests
    ├── IntegrationHub.Domain.Tests
    └── IntegrationHub.Infrastructure.Tests
```

---

## ⚙️ Tecnologias

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core (InMemory)
- Serilog (Logs estruturados)
- OpenTelemetry (Traces e Métricas)
- JWT Bearer (Autenticação)
- Swagger/OpenAPI
- xUnit + FluentAssertions + Moq

---

## 📝 Configuração

### Alterar para SQL Server (appsettings.json)

```json
{
  "Database": {
    "UseInMemory": false  // Mudar para false
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=IntegrationHubDb;..."
  }
}
```

---

## 🎯 Próximos Passos

- [ ] Implementar migrations do EF Core
- [ ] Substituir InMemoryMessageBus por RabbitMQ
- [ ] Adicionar Redis para cache
- [ ] Implementar Polly para retry policies
- [ ] Dockerizar a aplicação
- [ ] Configurar CI/CD

---

**Desenvolvido para TOTVS Tecfin** ❤️
