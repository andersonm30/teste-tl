# Workflow de Estados - Integration Hub

## 1. Visão Geral

Este documento descreve a **máquina de estados** de uma requisição de integração, incluindo estados válidos, transições permitidas, gatilhos de transição e tratamento de erros.

---

## 2. Estados do Workflow

### 2.1. Enum: `IntegrationStatus`

```csharp
public enum IntegrationStatus
{
    Received = 0,         // Requisição recebida e persistida
    Processing = 1,       // Worker iniciou processamento
    WaitingExternal = 2,  // Aguardando resposta do sistema externo
    Completed = 3,        // Integração concluída com sucesso
    Failed = 4            // Falha irrecuperável
}
```

---

## 3. Diagrama de Estados

Ver arquivo `workflow.mmd` para representação visual (stateDiagram-v2).

**Representação Textual:**

```
[START]
   ↓
Received
   ↓ (Worker consome evento)
Processing
   ↓ (Validação OK)
WaitingExternal
   ↓ (Sistema externo responde)
Completed
   ↓
[END]

Qualquer estado → Failed (em caso de erro)
```

---

## 4. Matriz de Transições

| Estado Atual | Transição Válida | Gatilho | Responsável |
|--------------|------------------|---------|-------------|
| **Received** | → Processing | Worker consome `IntegrationRequestCreated` | `IntegrationOrchestrationWorker` |
| **Received** | → Failed | Timeout (mensagem não consumida em 24h) | Dead-Letter Queue Monitor |
| **Processing** | → WaitingExternal | Validação de payload bem-sucedida | `IntegrationOrchestrationWorker` |
| **Processing** | → Failed | Validação de payload falha | `IntegrationOrchestrationWorker` |
| **Processing** | → Failed | Exceção não tratada no worker | `IntegrationOrchestrationWorker` |
| **WaitingExternal** | → Completed | Sistema externo retorna 200/201 | `FakeExternalSystemClient` |
| **WaitingExternal** | → Failed | Sistema externo retorna 4xx/5xx após retries | `FakeExternalSystemClient` + Polly |
| **WaitingExternal** | → Failed | Timeout da chamada externa (> 60s) | `FakeExternalSystemClient` |
| **Completed** | — | **Estado final** (nenhuma transição permitida) | — |
| **Failed** | — | **Estado final** (nenhuma transição permitida) | — |

---

## 5. Descrição Detalhada dos Estados

### 5.1. Received

**Definição:**
Requisição foi recebida pela API, validada estruturalmente e persistida no banco de dados.

**Invariantes:**
- ✅ Registro existe na tabela `IntegrationRequests`
- ✅ `ExternalId` é único (constraint)
- ✅ Evento `IntegrationRequestCreated` foi publicado na fila
- ✅ `CorrelationId` foi gerado/capturado

**Duração Típica:**
< 100ms (até worker consumir o evento)

**Próximos Passos:**
- Worker consome evento da fila
- Transição automática para `Processing`

**Observabilidade:**
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "INFO",
  "message": "Integration request received",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Received",
  "externalId": "PARTNER-12345"
}
```

---

### 5.2. Processing

**Definição:**
Worker iniciou o processamento. Validações de negócio estão em execução.

**Invariantes:**
- ✅ Worker tem lock da mensagem (RabbitMQ ack pendente)
- ✅ Status no banco foi atualizado para `Processing`
- ✅ Thread/Task está ativa no worker

**Ações Realizadas:**
1. **Validação de Payload:**
   - Schema JSON válido?
   - Campos obrigatórios presentes?
   - Tipos de dados corretos?

2. **Validações de Negócio:**
   - `SourceSystem` é reconhecido?
   - `TargetSystem` está disponível? (health check)
   - Duplicidade de `ExternalId`?

3. **Transformações:**
   - Mapeamento de campos (ex: SAP → Protheus)
   - Enriquecimento de dados (ex: buscar código de cliente)

**Duração Típica:**
200-500ms (validações + transformações)

**Próximos Passos:**
- **Se validação OK:** Transição para `WaitingExternal`
- **Se validação falha:** Transição para `Failed`

**Observabilidade:**
```json
{
  "timestamp": "2024-01-15T10:30:05.456Z",
  "level": "INFO",
  "message": "Processing integration request",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Processing",
  "validationResult": "Success"
}
```

---

### 5.3. WaitingExternal

**Definição:**
Requisição HTTP está em trânsito para o sistema externo. Aguardando resposta.

**Invariantes:**
- ✅ Requisição HTTP foi enviada
- ✅ Timeout configurado (padrão: 60s)
- ✅ Polly Retry Policy ativo (até 3 tentativas)

**Ações Realizadas:**
1. **Chamada HTTP:**
   ```csharp
   var response = await _httpClient.PostAsync(
       "https://api.totvs.com.br/protheus/v1/orders",
       content,
       cancellationToken
   );
   ```

2. **Tratamento de Respostas:**
   - **2xx:** Transição para `Completed`
   - **4xx (client error):** Transição para `Failed` (não faz retry)
   - **5xx (server error):** Retry com backoff exponencial
   - **Timeout:** Retry com backoff exponencial

**Duração Típica:**
1-5 segundos (depende da latência do sistema externo)

**Próximos Passos:**
- **Se sucesso:** Transição para `Completed` + armazenar resposta
- **Se falha após retries:** Transição para `Failed` + mover para DLQ

**Observabilidade:**
```json
{
  "timestamp": "2024-01-15T10:30:10.789Z",
  "level": "INFO",
  "message": "Calling external system",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "WaitingExternal",
  "targetSystem": "TotvsProtheus",
  "url": "https://api.totvs.com.br/protheus/v1/orders"
}
```

---

### 5.4. Completed

**Definição:**
Integração concluída com sucesso. Sistema externo retornou resposta positiva.

**Invariantes:**
- ✅ Status final (imutável)
- ✅ `ExternalResponse` armazenado (JSON da resposta)
- ✅ `UpdatedAt` reflete timestamp da conclusão
- ✅ Worker fez ACK da mensagem (removida da fila)

**Dados Armazenados:**
```sql
SELECT 
    Id, 
    ExternalId, 
    Status, -- 'Completed'
    ExternalResponse, -- '{"protheusOrderId":"PRO-XYZ-789"}'
    CorrelationId,
    CreatedAt,
    UpdatedAt -- timestamp da conclusão
FROM IntegrationRequests
WHERE Id = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

**Próximos Passos:**
- ✅ Nenhuma transição permitida (estado terminal)
- 📊 Métricas de SLA calculadas (`UpdatedAt - CreatedAt`)
- 📧 Notificação opcional para parceiro (webhook callback)

**Observabilidade:**
```json
{
  "timestamp": "2024-01-15T10:30:20.123Z",
  "level": "INFO",
  "message": "Integration completed successfully",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "duration_ms": 20000,
  "externalResponse": {
    "protheusOrderId": "PRO-XYZ-789"
  }
}
```

---

### 5.5. Failed

**Definição:**
Falha irrecuperável detectada. Integração não será reprocessada automaticamente.

**Invariantes:**
- ✅ Status final (imutável)
- ✅ `ErrorMessage` armazenado (detalhes da falha)
- ✅ Mensagem movida para Dead-Letter Queue (DLQ)
- ✅ Alert disparado para equipe de operações

**Causas de Falha:**

| Causa | Exemplo | Retriável? |
|-------|---------|-----------|
| **Validação de Payload** | Campo obrigatório ausente | ❌ Não |
| **Sistema Externo 4xx** | 404 Not Found, 400 Bad Request | ❌ Não |
| **Sistema Externo 5xx (após retries)** | 503 Service Unavailable (3 tentativas) | ⚠️ Manual |
| **Timeout** | Sistema externo não responde em 60s | ⚠️ Manual |
| **Exceção Não Tratada** | NullReferenceException no worker | ❌ Não (bug) |
| **Duplicidade** | `ExternalId` já existe | ❌ Não |

**Dados Armazenados:**
```sql
SELECT 
    Id, 
    ExternalId, 
    Status, -- 'Failed'
    ErrorMessage, -- 'External system returned 503 after 3 retries'
    CorrelationId,
    CreatedAt,
    UpdatedAt
FROM IntegrationRequests
WHERE Id = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
```

**Próximos Passos:**
- 🚨 Alert para PagerDuty/OpsGenie
- 📋 Ticket criado no Jira
- 🔍 Análise da DLQ por engenheiro
- 🔄 Reprocessamento manual (se aplicável)

**Observabilidade:**
```json
{
  "timestamp": "2024-01-15T10:30:25.999Z",
  "level": "ERROR",
  "message": "Integration failed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Failed",
  "errorMessage": "External system returned 503 after 3 retries",
  "retryCount": 3,
  "exception": {
    "type": "HttpRequestException",
    "stackTrace": "..."
  }
}
```

---

## 6. Regras de Negócio para Transições

### 6.1. Transição: Received → Processing

**Pré-condições:**
- ✅ Evento `IntegrationRequestCreated` consumido pelo worker
- ✅ Worker tem capacidade disponível (não está no limite de concorrência)

**Pós-condições:**
- ✅ Status no banco atualizado para `Processing`
- ✅ Log de início de processamento emitido

**Rollback:**
Se falha ao atualizar status, worker faz NACK da mensagem (volta para fila).

---

### 6.2. Transição: Processing → WaitingExternal

**Pré-condições:**
- ✅ Payload validado com sucesso
- ✅ Sistema de destino está disponível (health check)

**Pós-condições:**
- ✅ Status no banco atualizado para `WaitingExternal`
- ✅ Requisição HTTP iniciada

**Rollback:**
Se falha ao atualizar status, worker faz NACK e reprocessa desde `Processing`.

---

### 6.3. Transição: WaitingExternal → Completed

**Pré-condições:**
- ✅ Sistema externo retornou 2xx
- ✅ Resposta contém dados esperados (validação de schema)

**Pós-condições:**
- ✅ Status no banco atualizado para `Completed`
- ✅ `ExternalResponse` armazenado
- ✅ Worker faz ACK da mensagem
- ✅ Métrica de sucesso incrementada

**Rollback:**
Não aplicável (transação do banco commit = sucesso definitivo).

---

### 6.4. Transição: Qualquer Estado → Failed

**Pré-condições:**
- ✅ Erro irrecuperável detectado
- ✅ Retries (se aplicável) esgotados

**Pós-condições:**
- ✅ Status no banco atualizado para `Failed`
- ✅ `ErrorMessage` armazenado
- ✅ Mensagem movida para DLQ
- ✅ Alert disparado

**Rollback:**
Não aplicável (falha é definitiva, requer intervenção manual).

---

## 7. Estados Temporários (Não Persistidos)

Além dos 5 estados persistidos no banco, existem estados **transitórios** apenas na memória do worker:

| Estado Transitório | Descrição | Duração |
|--------------------|-----------|---------|
| **Validating** | Validando payload e business rules | 50-200ms |
| **Transforming** | Mapeando campos entre sistemas | 10-50ms |
| **CallingExternal** | HTTP request em trânsito | 1-5s |
| **AwaitingRetry** | Aguardando backoff antes de retry | 2-8s |

Esses estados não são persistidos, mas são visíveis nos **logs estruturados**.

---

## 8. SLA por Estado

| Estado | Duração Máxima (p95) | Ação se Excedido |
|--------|----------------------|------------------|
| **Received** | 1 minuto | Alert: Worker não está consumindo fila |
| **Processing** | 10 segundos | Alert: Validação muito lenta |
| **WaitingExternal** | 60 segundos | Timeout + transição para `Failed` |
| **Completed** | N/A (final) | — |
| **Failed** | N/A (final) | — |

**SLA Total (Received → Completed):** < 3 minutos para 95% das requisições.

---

## 9. Consulta de Status via API

Parceiros podem consultar o status atual via endpoint:

```http
GET /api/integration-requests/{id}
Authorization: Bearer {jwt_token}
```

**Resposta (exemplo em WaitingExternal):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "PARTNER-12345",
  "status": "WaitingExternal",
  "statusDescription": "Aguardando resposta do sistema externo",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-01-15T10:30:00Z",
  "updatedAt": "2024-01-15T10:30:10Z",
  "estimatedCompletionTime": "2024-01-15T10:31:00Z"
}
```

---

## 10. Webhook de Notificação (Futuro)

Quando requisição atinge estado `Completed` ou `Failed`, sistema pode notificar parceiro via webhook:

```http
POST https://partner.com/webhooks/integration-status
Content-Type: application/json
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000

{
  "eventType": "IntegrationStatusChanged",
  "requestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "PARTNER-12345",
  "status": "Completed",
  "timestamp": "2024-01-15T10:30:20Z",
  "externalResponse": {
    "protheusOrderId": "PRO-XYZ-789"
  }
}
```

---

## 11. Métricas de Workflow

### 11.1. Métricas de Transição

```prometheus
# Contador de transições por estado
integration_status_transitions_total{from="Received", to="Processing"} 1234
integration_status_transitions_total{from="Processing", to="WaitingExternal"} 1200
integration_status_transitions_total{from="WaitingExternal", to="Completed"} 1150
integration_status_transitions_total{from="WaitingExternal", to="Failed"} 50

# Duração por estado (histograma)
integration_state_duration_seconds_bucket{state="Processing", le="0.5"} 800
integration_state_duration_seconds_bucket{state="Processing", le="1.0"} 1100
integration_state_duration_seconds_bucket{state="WaitingExternal", le="2.0"} 900
```

### 11.2. Alertas

```yaml
# Prometheus AlertManager
groups:
  - name: integration_workflow
    rules:
      - alert: HighFailureRate
        expr: rate(integration_status_transitions_total{to="Failed"}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Taxa de falha acima de 5% nos últimos 5 minutos"

      - alert: WorkerNotConsuming
        expr: increase(integration_status_transitions_total{from="Received"}[5m]) == 0
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Worker não está consumindo mensagens da fila"
```

---

## 12. Conclusão

O workflow de estados implementado garante:
- ✅ **Rastreabilidade** completa de cada requisição
- ✅ **Transições atômicas** (banco de dados transacional)
- ✅ **Tratamento de erros** com estados finais claros
- ✅ **Observabilidade** em cada transição
- ✅ **SLA** definido por estado
