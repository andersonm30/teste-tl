# Integration Hub - TOTVS Tecfin

**Prova de conceito de arquitetura de um Hub de Integração e Orquestração**

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![Clean Architecture](https://img.shields.io/badge/Architecture-Clean-green)
![License](https://img.shields.io/badge/License-MIT-yellow)

---

## 📋 Sumário

- [Visão Geral](#-visão-geral)
- [Arquitetura](#-arquitetura)
- [Tecnologias Utilizadas](#-tecnologias-utilizadas)
- [Estrutura de Projetos](#-estrutura-de-projetos)
- [Principais Funcionalidades](#-principais-funcionalidades)
- [Como Executar](#-como-executar)
- [Testando a API](#-testando-a-api)
- [Observabilidade](#-observabilidade)
- [Segurança](#-segurança)
- [Testes](#-testes)
- [Evolução Futura](#-evolução-futura)

---

## 🎯 Visão Geral

O **Integration Hub** é uma solução de **orquestração e integração** entre sistemas, projetada para atuar como um ponto central de comunicação entre sistemas terceiros e a plataforma TOTVS Tecfin.

### Principais Objetivos

- ✅ **Orquestração de requisições** entre sistemas heterogêneos
- ✅ **Processamento assíncrono** com mensageria
- ✅ **Rastreabilidade completa** via CorrelationId
- ✅ **Alta disponibilidade** e escalabilidade horizontal
- ✅ **Observabilidade** com logs estruturados, traces e métricas
- ✅ **Segurança** com autenticação JWT
- ✅ **Resiliência** com tratamento de erros e retry patterns

---

## 🏗️ Arquitetura

O projeto segue os princípios de **Clean Architecture** (Arquitetura Limpa / Hexagonal), separando responsabilidades em camadas bem definidas:

```
┌─────────────────────────────────────────────────────────────┐
│                     API (REST/HTTP)                         │
│  Controllers, Middlewares, Authentication, Swagger          │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                  Application Layer                          │
│  Use Cases, DTOs, Services, Business Logic                  │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer                             │
│  Entities, Enums, Domain Events, Interfaces                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                        │
│  Repositories, DbContext, MessageBus, External Clients      │
│  Logging (Serilog), Observability (OpenTelemetry)           │
└─────────────────────────────────────────────────────────────┘
                        │
                        ▼
        ┌───────────────┴───────────────┐
        ▼                               ▼
  ┌──────────┐                    ┌──────────┐
  │  Worker  │                    │ Database │
  │  Service │                    │ (In-Mem) │
  └──────────┘                    └──────────┘
```

### Fluxo de Processamento

1. **Cliente** envia requisição HTTP para a API
2. **API** valida, adiciona CorrelationId e cria requisição no banco
3. **MessageBus** publica evento `IntegrationRequestCreated`
4. **Worker** consome o evento e inicia orquestração:
   - Muda status para `Processing`
   - Processa regras de negócio
   - Muda status para `WaitingExternal`
   - Chama sistema externo
   - Marca como `Completed` ou `Failed`
5. Cliente pode consultar status via API a qualquer momento

---

## 🚀 Tecnologias Utilizadas

### Frameworks & Bibliotecas

| Tecnologia | Versão | Uso |
|------------|--------|-----|
| **.NET** | 8.0 | Framework principal |
| **ASP.NET Core** | 8.0 | Web API |
| **Entity Framework Core** | 8.0 | ORM / Persistence |
| **Serilog** | 9.0 | Logs estruturados |
| **OpenTelemetry** | 1.14 | Traces e Métricas |
| **Swashbuckle (Swagger)** | 6.8 | Documentação OpenAPI |
| **JWT Bearer** | 8.0 | Autenticação |
| **xUnit** | - | Testes unitários |
| **FluentAssertions** | 8.8 | Assertions nos testes |
| **Moq** | 4.20 | Mocking para testes |

### Padrões e Conceitos

- ✅ **Clean Architecture / Hexagonal Architecture**
- ✅ **SOLID Principles**
- ✅ **Repository Pattern**
- ✅ **Domain Events**
- ✅ **CQRS (preparado para evolução)**
- ✅ **Outbox Pattern (conceitual)**
- ✅ **Correlation ID Pattern**
- ✅ **Saga / Orchestration Pattern**

---

## 📁 Estrutura de Projetos

```
IntegrationHub/
├── src/
│   ├── IntegrationHub.Api/              # ASP.NET Core Web API
│   │   ├── Controllers/                 # REST endpoints
│   │   ├── Middleware/                  # CorrelationId, Exception Handler
│   │   ├── Program.cs                   # Configuração da aplicação
│   │   └── appsettings.json
│   │
│   ├── IntegrationHub.Application/      # Camada de Aplicação
│   │   ├── DTOs/                        # Data Transfer Objects
│   │   ├── Interfaces/                  # Interfaces de serviços
│   │   └── Services/                    # Implementação de serviços
│   │
│   ├── IntegrationHub.Domain/           # Camada de Domínio
│   │   ├── Entities/                    # IntegrationRequest
│   │   ├── Enums/                       # IntegrationStatus
│   │   ├── Events/                      # IntegrationRequestCreated
│   │   └── Interfaces/                  # IIntegrationRequestRepository, IMessageBus
│   │
│   ├── IntegrationHub.Infrastructure/   # Camada de Infraestrutura
│   │   ├── Persistence/                 # DbContext, Repositories
│   │   ├── Messaging/                   # InMemoryMessageBus
│   │   ├── ExternalClients/             # Fake clients para sistemas externos
│   │   └── DependencyInjection.cs       # Configuração de DI
│   │
│   └── IntegrationHub.Worker/           # Background Service
│       ├── IntegrationOrchestrationWorker.cs
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/
│   ├── IntegrationHub.Api.Tests/
│   ├── IntegrationHub.Application.Tests/
│   ├── IntegrationHub.Domain.Tests/
│   └── IntegrationHub.Infrastructure.Tests/
│
├── IntegrationHub.sln
└── README.md
```

---

## ⚙️ Principais Funcionalidades

### 1. API REST

#### POST /api/integration-requests
Cria uma nova requisição de integração.

**Request:**
```json
{
  "externalId": "EXT-12345",
  "sourceSystem": "PartnerA",
  "targetSystem": "Totvs",
  "payload": "{\"customer\": \"ACME Corp\", \"value\": 1000.00}"
}
```

**Response (202 Accepted):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "EXT-12345",
  "sourceSystem": "PartnerA",
  "targetSystem": "Totvs",
  "status": "Received",
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "createdAt": "2024-11-19T20:00:00Z",
  "updatedAt": "2024-11-19T20:00:00Z"
}
```

#### GET /api/integration-requests/{id}
Consulta o status de uma requisição.

#### GET /api/integration-requests
Lista todas as requisições.

#### GET /api/health
Health check público.

#### GET /api/health/secure
Health check protegido por JWT (exemplo de autenticação).

---

## 🚀 Como Executar

### Pré-requisitos

- **.NET 8 SDK** instalado ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Editor de código (Visual Studio, VS Code, Rider, etc.)

### Passo 1: Clonar o Repositório

```bash
git clone <url-do-repositorio>
cd teste-totvs
```

### Passo 2: Restaurar Dependências

```bash
dotnet restore
```

### Passo 3: Compilar a Solução

```bash
dotnet build
```

### Passo 4: Executar a API

```bash
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj
```

A API estará disponível em:
- **HTTPS:** https://localhost:7000 (ou porta configurada)
- **HTTP:** http://localhost:5000
- **Swagger UI:** https://localhost:7000/ (raiz)

### Passo 5: Executar o Worker (em outro terminal)

```bash
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj
```

### Passo 6: Executar os Testes

```bash
dotnet test
```

---

## 🧪 Testando a API

### Usando Swagger UI

1. Acesse: https://localhost:7000/
2. Navegue pelos endpoints documentados
3. Clique em "Try it out" para testar

### Usando cURL

**Criar uma requisição:**
```bash
curl -X POST "https://localhost:7000/api/integration-requests" \
  -H "Content-Type: application/json" \
  -d '{
    "externalId": "EXT-12345",
    "sourceSystem": "PartnerA",
    "targetSystem": "Totvs",
    "payload": "{\"test\": \"data\"}"
  }'
```

**Consultar status:**
```bash
curl -X GET "https://localhost:7000/api/integration-requests/{id}"
```

### Fluxo de Status

1. **Received** → Requisição criada
2. **Processing** → Worker iniciou processamento
3. **WaitingExternal** → Aguardando resposta de sistema externo
4. **Completed** / **Failed** → Finalizada com sucesso ou erro

---

## 📊 Observabilidade

### Logs Estruturados (Serilog)

Os logs são gravados em:
- **Console:** Output estruturado e colorido
- **Arquivo:** `logs/integration-hub-*.log` (rotação diária)

Exemplo de log:
```
[20:15:30 INF] IntegrationHub.Application.Services.IntegrationRequestService - Creating integration request. ExternalId: EXT-12345, Source: PartnerA, Target: Totvs, CorrelationId: abc123
```

### Traces e Métricas (OpenTelemetry)

O projeto está configurado com OpenTelemetry para:
- **Traces:** Rastreamento de requisições HTTP e chamadas a banco de dados
- **Métricas:** Contadores, histogramas, etc.
- **Correlation ID:** Propagado em todos os traces

Para produção, configure exporters para sistemas como:
- **Jaeger** (traces)
- **Prometheus** (métricas)
- **Azure Application Insights**
- **Datadog**, **New Relic**, etc.

---

## 🔒 Segurança

### Autenticação JWT

A API está configurada para aceitar tokens JWT (Bearer).

**Configuração** (`appsettings.json`):
```json
{
  "Jwt": {
    "Key": "MyVerySecretKeyForIntegrationHubTOTVSTecfin2024!@#",
    "Issuer": "IntegrationHub",
    "Audience": "IntegrationHubClients",
    "ExpirationMinutes": 60
  }
}
```

**Endpoint protegido de exemplo:**
- `GET /api/health/secure` requer token JWT

### Evolução para Produção

- Implementar **Identity Server** ou **Azure AD** para geração de tokens
- Configurar **políticas de autorização** (roles, claims)
- Habilitar **HTTPS** obrigatório
- Implementar **rate limiting**
- Adicionar **API Gateway** (Ocelot, Kong, Azure API Management)

---

## ✅ Testes

### Executar Todos os Testes

```bash
dotnet test
```

### Estrutura de Testes

- **Domain.Tests:** Testes de entidades e lógica de domínio
- **Application.Tests:** Testes de serviços de aplicação (com Moq)
- **Infrastructure.Tests:** (preparado para testes de repositórios)
- **Api.Tests:** (preparado para testes de integração)

### Exemplo de Teste

```csharp
[Fact]
public async Task CreateAsync_ShouldCreateRequest_AndPublishEvent()
{
    // Arrange
    var command = new CreateIntegrationRequestCommand { /* ... */ };
    
    // Act
    var result = await _service.CreateAsync(command, correlationId);
    
    // Assert
    result.Should().NotBeNull();
    result.Status.Should().Be("Received");
}
```

---

## 🔮 Evolução Futura

### Curto Prazo

- [ ] **Migrations:** Adicionar EF Core Migrations para SQL Server
- [ ] **RabbitMQ:** Substituir InMemoryMessageBus por RabbitMQ real
- [ ] **Redis:** Cache distribuído para alta performance
- [ ] **Health Checks avançados:** Verificação de dependências externas
- [ ] **Retry Policies:** Implementar Polly para resiliência

### Médio Prazo

- [ ] **CQRS completo:** Separar comandos e queries
- [ ] **Event Sourcing:** Histórico completo de mudanças
- [ ] **API Gateway:** Ocelot ou Azure API Management
- [ ] **Service Mesh:** Istio ou Linkerd para comunicação entre serviços
- [ ] **Container Orchestration:** Docker + Kubernetes
- [ ] **CI/CD:** Pipelines Azure DevOps / GitHub Actions

### Longo Prazo

- [ ] **Microservices:** Separar em múltiplos serviços especializados
- [ ] **Azure Service Bus:** Mensageria cloud-native
- [ ] **Azure Application Insights:** Observabilidade completa
- [ ] **GraphQL:** API adicional para queries flexíveis
- [ ] **gRPC:** Comunicação binária de alta performance entre serviços
- [ ] **Outbox Pattern real:** Garantia de entrega de mensagens

---

## 📝 Configuração

### appsettings.json (API)

```json
{
  "Database": {
    "UseInMemory": true  // Alterar para false para usar SQL Server
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=IntegrationHubDb;..."
  },
  "Jwt": {
    "Key": "sua-chave-secreta",
    "Issuer": "IntegrationHub",
    "Audience": "IntegrationHubClients"
  }
}
```

---

## 👥 Contribuindo

Este é um projeto de **prova de conceito** para avaliação técnica.

---

## 📄 Licença

Este projeto é fornecido para fins de avaliação técnica da **TOTVS Tecfin**.

---

## 📞 Contato

Para dúvidas ou sugestões sobre esta arquitetura, entre em contato com o time de Arquitetura.

---

**Desenvolvido com ❤️ para TOTVS Tecfin**
