# Fluxo de Orquestração - Integration Hub

## 1. Visão Geral

Este documento detalha o fluxo completo de uma requisição de integração — desde quando o parceiro envia a requisição até receber a resposta final. O processo passa por persistência, mensageria, processamento assíncrono e integração com sistemas externos.

---

## 2. Fluxo End-to-End

### 2.1. Diagrama de Sequência

Ver arquivo `fluxo.mmd` para representação visual.

### 2.2. Etapas Detalhadas

#### **Etapa 1: Recepção da Requisição (API Gateway)**

**Ator:** Parceiro Externo  
**Componente:** `IntegrationHub.Api` → `IntegrationRequestsController`

```http
POST /api/integration-requests
Authorization: Bearer {jwt_token}
Content-Type: application/json
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000

{
  "externalId": "PARTNER-12345",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus",
  "payload": {
    "orderId": "ORD-2024-001",
    "customer": "Empresa ABC",
    "amount": 15000.00
  }
}
```

**Processamento:**
1. Middleware `CorrelationIdMiddleware` captura ou gera `X-Correlation-ID`
2. Middleware `GlobalExceptionHandlerMiddleware` envolve pipeline
3. Autenticação JWT valida token
4. Controller valida modelo (Data Annotations)
5. Chama `IIntegrationRequestService.CreateAsync()`

**Resposta:**
```http
HTTP/1.1 202 Accepted
Location: /api/integration-requests/3fa85f64-5717-4562-b3fc-2c963f66afa6
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000

{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "PARTNER-12345",
  "status": "Received",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Observabilidade:**
- Log: `[INFO] Integration request received | CorrelationId={correlationId} | ExternalId={externalId}`
- Trace: Span `api.integration-requests.create` iniciado
- Métrica: Incremento em `integration_requests_received_total`

---

#### **Etapa 2: Persistência (Application Layer)**

**Componente:** `IntegrationRequestService`

**Fluxo:**
```csharp
public async Task<IntegrationRequestDto> CreateAsync(CreateIntegrationRequestCommand command)
{
    // 1. Cria entidade de domínio
    var request = new IntegrationRequest(
        command.ExternalId,
        command.SourceSystem,
        command.TargetSystem,
        JsonSerializer.Serialize(command.Payload)
    );

    // 2. Gera CorrelationId se não fornecido
    if (string.IsNullOrEmpty(request.CorrelationId))
    {
        request.CorrelationId = Guid.NewGuid().ToString();
    }

    // 3. Persiste no banco (transação)
    await _repository.AddAsync(request);

    // 4. Publica evento de domínio
    await _messageBus.PublishAsync(new IntegrationRequestCreated
    {
        RequestId = request.Id,
        CorrelationId = request.CorrelationId,
        ExternalId = request.ExternalId,
        Timestamp = DateTime.UtcNow
    });

    return MapToDto(request);
}
```

**Banco de Dados (SQL Server):**
```sql
INSERT INTO IntegrationRequests (
    Id, ExternalId, SourceSystem, TargetSystem, 
    Payload, Status, CorrelationId, CreatedAt, UpdatedAt
) VALUES (
    '3fa85f64-5717-4562-b3fc-2c963f66afa6',
    'PARTNER-12345',
    'SAP',
    'TotvsProtheus',
    '{"orderId":"ORD-2024-001",...}',
    'Received',
    '550e8400-e29b-41d4-a716-446655440000',
    '2024-01-15 10:30:00',
    '2024-01-15 10:30:00'
);
```

**Observabilidade:**
- Log: `[INFO] Integration request persisted | Id={id} | CorrelationId={correlationId}`
- Trace: Span `db.insert.integration_requests`
- Métrica: Histogram `integration_requests_persist_duration_ms`

---

#### **Etapa 3: Publicação de Evento (Message Bus)**

**Componente:** `InMemoryMessageBus` (produção: RabbitMQ)

**Evento Publicado:**
```json
{
  "eventType": "IntegrationRequestCreated",
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "externalId": "PARTNER-12345",
  "timestamp": "2024-01-15T10:30:00.123Z"
}
```

**Fila RabbitMQ (Futuro):**
- **Exchange:** `integration.events` (topic)
- **Routing Key:** `integration.request.created`
- **Queue:** `integration-orchestration-queue`
- **TTL:** 24 horas
- **DLX:** `integration.events.dlq`

**Observabilidade:**
- Log: `[INFO] Event published to queue | EventType={eventType} | CorrelationId={correlationId}`
- Trace: Span `messagebus.publish`
- Métrica: Counter `events_published_total{type="IntegrationRequestCreated"}`

---

#### **Etapa 4: Consumo pelo Worker (Background Service)**

**Componente:** `IntegrationOrchestrationWorker`

**Fluxo:**
```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _messageBus.SubscribeAsync<IntegrationRequestCreated>(async evt =>
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = evt.CorrelationId,
            ["RequestId"] = evt.RequestId
        });

        try
        {
            // 4.1. Atualiza status para Processing
            await UpdateStatusAsync(evt.RequestId, IntegrationStatus.Processing);

            // 4.2. Valida payload (business rules)
            var isValid = await ValidatePayloadAsync(evt.RequestId);
            if (!isValid)
            {
                await UpdateStatusAsync(evt.RequestId, IntegrationStatus.Failed);
                return;
            }

            // 4.3. Chama sistema externo
            await CallExternalSystemAsync(evt.RequestId);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing integration request");
            await UpdateStatusAsync(evt.RequestId, IntegrationStatus.Failed);
        }
    }, stoppingToken);
}
```

**Observabilidade:**
- Log: `[INFO] Worker processing integration | CorrelationId={correlationId}`
- Trace: Span `worker.process_integration`
- Métrica: Gauge `worker_active_tasks`

---

#### **Etapa 5: Atualização de Status → Processing**

**Componente:** `IntegrationRequestService.UpdateStatusAsync()`

**Query Executada:**
```sql
UPDATE IntegrationRequests
SET Status = 'Processing', UpdatedAt = '2024-01-15 10:30:05'
WHERE Id = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

**Observabilidade:**
- Log: `[INFO] Status updated | Id={id} | OldStatus=Received | NewStatus=Processing`
- Trace: Span `db.update.integration_requests`

---

#### **Etapa 6: Validação de Payload (Business Rules)**

**Componente:** Worker → `ValidatePayloadAsync()`

**Validações:**
1. Schema do payload é válido?
2. Campos obrigatórios estão presentes?
3. Sistema de destino está disponível?
4. Duplicidade de `ExternalId`?

**Exemplo de Validação:**
```csharp
private async Task<bool> ValidatePayloadAsync(Guid requestId)
{
    var request = await _repository.GetByIdAsync(requestId);
    var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(request.Payload);

    // Validação customizada por SourceSystem
    if (request.SourceSystem == "SAP" && !payload.ContainsKey("orderId"))
    {
        _logger.LogWarning("Missing required field: orderId");
        return false;
    }

    return true;
}
```

**Observabilidade:**
- Log: `[WARN] Validation failed | Field=orderId | CorrelationId={correlationId}`
- Métrica: Counter `integration_validation_failures_total{field="orderId"}`

---

#### **Etapa 7: Atualização de Status → WaitingExternal**

**Query Executada:**
```sql
UPDATE IntegrationRequests
SET Status = 'WaitingExternal', UpdatedAt = '2024-01-15 10:30:10'
WHERE Id = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

**Observabilidade:**
- Log: `[INFO] Status updated | NewStatus=WaitingExternal`

---

#### **Etapa 8: Chamada ao Sistema Externo (Adapter)**

**Componente:** `FakeExternalSystemClient` (produção: `TotvsProtheusClient`)

**Requisição HTTP:**
```http
POST https://api.totvs.com.br/protheus/v1/orders
Authorization: Bearer {protheus_token}
Content-Type: application/json
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000

{
  "orderId": "ORD-2024-001",
  "customer": "Empresa ABC",
  "amount": 15000.00,
  "originSystem": "SAP"
}
```

**Resposta do Sistema Externo:**
```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "protheusOrderId": "PRO-XYZ-789",
  "status": "Created",
  "timestamp": "2024-01-15T10:30:15Z"
}
```

**Tratamento de Erros:**
```csharp
try
{
    var response = await _httpClient.PostAsync(url, content);
    response.EnsureSuccessStatusCode();
}
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
{
    // Retry com Polly (exponential backoff)
    _logger.LogWarning("External system unavailable, will retry");
    throw; // Polly intercepta
}
```

**Observabilidade:**
- Log: `[INFO] External system called | Url={url} | Duration={duration}ms`
- Trace: Span `http.external.totvs_protheus`
- Métrica: Histogram `external_system_call_duration_ms{system="TotvsProtheus"}`

---

#### **Etapa 9: Atualização de Status → Completed**

**Query Executada:**
```sql
UPDATE IntegrationRequests
SET 
    Status = 'Completed', 
    UpdatedAt = '2024-01-15 10:30:20',
    ExternalResponse = '{"protheusOrderId":"PRO-XYZ-789",...}'
WHERE Id = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

**Evento de Auditoria (Futuro):**
```json
{
  "eventType": "IntegrationRequestCompleted",
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "completedAt": "2024-01-15T10:30:20Z",
  "externalResponse": {...}
}
```

**Observabilidade:**
- Log: `[INFO] Integration completed successfully | CorrelationId={correlationId}`
- Métrica: Counter `integration_requests_completed_total`

---

#### **Etapa 10: Consulta de Status pelo Parceiro**

**Requisição:**
```http
GET /api/integration-requests/3fa85f64-5717-4562-b3fc-2c963f66afa6
Authorization: Bearer {jwt_token}
```

**Resposta:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "PARTNER-12345",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus",
  "status": "Completed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-15T10:30:20Z",
  "externalResponse": {
    "protheusOrderId": "PRO-XYZ-789"
  }
}
```

---

## 3. Fluxo de Erro

### 3.1. Cenário: Sistema Externo Indisponível

**Etapa 8 (Falha):**
```
Worker → FakeExternalSystemClient
  ↓
HTTP 503 Service Unavailable
  ↓
Polly Retry Policy: Tentativa 1/3 (aguarda 2s)
  ↓
HTTP 503 Service Unavailable
  ↓
Polly Retry Policy: Tentativa 2/3 (aguarda 4s)
  ↓
HTTP 503 Service Unavailable
  ↓
Polly Retry Policy: Tentativa 3/3 (aguarda 8s)
  ↓
HTTP 503 Service Unavailable
  ↓
Atualiza Status → Failed
  ↓
Move mensagem para DLQ
```

**Observabilidade:**
- Log: `[ERROR] Max retries exceeded | System=TotvsProtheus | CorrelationId={correlationId}`
- Métrica: Counter `integration_requests_failed_total{reason="external_unavailable"}`
- Alert: PagerDuty dispara se taxa de falha > 5% em 5 minutos

---

### 3.2. Cenário: Payload Inválido

**Etapa 6 (Falha):**
```
Worker → ValidatePayloadAsync()
  ↓
Campo obrigatório "orderId" ausente
  ↓
Atualiza Status → Failed
  ↓
Log: [WARN] Validation failed
  ↓
Não faz retry (erro não transiente)
```

**Observabilidade:**
- Log: `[WARN] Payload validation failed | Field=orderId | CorrelationId={correlationId}`
- Métrica: Counter `integration_validation_failures_total{field="orderId"}`

---

## 4. Pontos de Atenção

### 4.1. Idempotência

**Problema:** Parceiro reenvia mesma requisição (retry do lado dele).

**Solução:**
- `ExternalId` é **unique constraint** no banco
- Se já existe, retorna `409 Conflict` com link para recurso existente

### 4.2. Timeout

**Problema:** Sistema externo demora > 30 segundos.

**Solução:**
- Worker tem timeout configurável (padrão: 60s)
- Após timeout, marca como `Failed` e move para DLQ
- DLQ é processada por job manual/overnight

### 4.3. Transações Distribuídas

**Problema:** Banco commitou, mas publicação na fila falhou.

**Solução (Futuro):**
- Outbox Pattern:
  1. Transação única: `INSERT IntegrationRequest + INSERT OutboxEvent`
  2. Worker secundário lê OutboxTable e publica eventos
  3. Marca evento como processado

### 4.4. CorrelationId Tracking

**Garantia:** CorrelationId é propagado em:
- Headers HTTP (`X-Correlation-ID`)
- Mensagens da fila
- Logs estruturados
- Traces distribuídos

**Benefício:** Rastreabilidade end-to-end de qualquer requisição.

---

## 5. Métricas de Performance

| Etapa | Latência Esperada (p95) | SLO |
|-------|-------------------------|-----|
| API → Persistência | < 50ms | 99.9% |
| Persistência → Publicação Evento | < 10ms | 99.9% |
| Evento → Consumo Worker | < 100ms | 99% |
| Worker → Sistema Externo | < 2s | 95% |
| **Total (Assíncrono)** | **< 3s** | **95%** |

**SLA Proposto:** 99.9% de disponibilidade (8.76h downtime/ano).

---

## 6. Conclusão

O fluxo de orquestração implementado garante:
- ✅ **Resposta imediata** ao parceiro (202 Accepted)
- ✅ **Processamento assíncrono** resiliente
- ✅ **Rastreabilidade completa** via CorrelationId
- ✅ **Tratamento de erros** com retry e DLQ
- ✅ **Observabilidade** em todas as etapas
