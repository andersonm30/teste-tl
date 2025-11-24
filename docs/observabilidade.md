# Observabilidade - Integration Hub

## 1. Pilares da Observabilidade

A estratégia de observabilidade do Integration Hub é baseada nos **três pilares**:

1. **Logs:** Eventos estruturados para auditoria e debugging
2. **Traces:** Rastreamento distribuído de requisições end-to-end
3. **Métricas:** Indicadores de performance e saúde do sistema

---

## 2. Logging Estruturado

### 2.1. Framework: Serilog

**Configuração:**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "IntegrationHub")
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.File(
        "logs/integration-hub-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30
    )
    .CreateLogger();
```

**Sinks Configurados:**
- **Console:** Formato JSON compacto para containers (stdout)
- **File:** Rotação diária, retenção de 30 dias
- **Futuro:** Seq, Elasticsearch, Azure Application Insights

---

### 2.2. Enriquecimento de Logs

Cada log entry contém automaticamente:

```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information",
  "messageTemplate": "Integration request received",
  "message": "Integration request received",
  "properties": {
    "CorrelationId": "550e8400-e29b-41d4-a716-446655440000",
    "RequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "ExternalId": "PARTNER-12345",
    "SourceSystem": "SAP",
    "TargetSystem": "TotvsProtheus",
    "MachineName": "integrationhub-api-pod-001",
    "Environment": "Production",
    "Application": "IntegrationHub"
  }
}
```

**Campos Padronizados:**
- `CorrelationId`: Rastreamento end-to-end (propagado via header `X-Correlation-ID`)
- `RequestId`: Identificador único da requisição de integração
- `ExternalId`: Identificador fornecido pelo parceiro
- `MachineName`: Nome do pod/container/VM
- `Environment`: Development, Staging, Production

---

### 2.3. Níveis de Log

| Nível | Uso | Exemplo |
|-------|-----|---------|
| **Trace** | Debugging detalhado (desabilitado em produção) | `Entering method CreateAsync` |
| **Debug** | Informações de desenvolvimento | `Payload: {"orderId":"ORD-123"}` |
| **Information** | Eventos de negócio normais | `Integration request created` |
| **Warning** | Situações anormais recuperáveis | `External system slow response (3s)` |
| **Error** | Falhas que requerem atenção | `External system returned 503` |
| **Critical** | Falhas catastróficas | `Database connection lost` |

---

### 2.4. Log Scopes

**Implementação:**
```csharp
using var scope = _logger.BeginScope(new Dictionary<string, object>
{
    ["CorrelationId"] = correlationId,
    ["RequestId"] = requestId
});

_logger.LogInformation("Starting validation");
_logger.LogInformation("Calling external system");
_logger.LogInformation("Integration completed");
// Todos os logs dentro do scope herdam CorrelationId e RequestId
```

**Benefício:** Contexto automático em todos os logs do bloco.

---

### 2.5. Exemplos de Logs

#### Log: Requisição Recebida
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information",
  "message": "Integration request received",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "externalId": "PARTNER-12345",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus"
}
```

#### Log: Validação Falhou
```json
{
  "timestamp": "2024-01-15T10:30:05.456Z",
  "level": "Warning",
  "message": "Payload validation failed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "validationErrors": [
    {
      "field": "orderId",
      "error": "Field is required"
    }
  ]
}
```

#### Log: Erro de Sistema Externo
```json
{
  "timestamp": "2024-01-15T10:30:15.789Z",
  "level": "Error",
  "message": "External system call failed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "targetSystem": "TotvsProtheus",
  "url": "https://api.totvs.com.br/protheus/v1/orders",
  "statusCode": 503,
  "retryAttempt": 3,
  "exception": {
    "type": "HttpRequestException",
    "message": "Service unavailable",
    "stackTrace": "..."
  }
}
```

---

## 3. Distributed Tracing

### 3.1. Framework: OpenTelemetry

**Configuração:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("IntegrationHub")
            .ConfigureResource(resource => resource
                .AddService("IntegrationHub.Api")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"
                }))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddConsoleExporter();
    });
```

**Instrumentação Automática:**
- ✅ ASP.NET Core (requests HTTP)
- ✅ HttpClient (chamadas externas)
- ✅ Entity Framework Core (queries SQL)

**Exportação:**
- **Desenvolvimento:** Console
- **Produção:** Jaeger / Zipkin / Azure Application Insights

---

### 3.2. Anatomia de um Trace

**Estrutura:**
```
TraceId: a1b2c3d4-e5f6-7890-abcd-ef1234567890
├─ Span: api.integration-requests.create (150ms)
│  ├─ Span: db.insert.integration_requests (20ms)
│  └─ Span: messagebus.publish (5ms)
├─ Span: worker.process_integration (2000ms)
│  ├─ Span: worker.validate_payload (50ms)
│  ├─ Span: db.update.integration_requests (15ms)
│  └─ Span: http.external.totvs_protheus (1800ms)
│     └─ Span: external.response (1800ms)
└─ Span: db.update.integration_requests (10ms)
```

**Atributos de Span:**
```json
{
  "traceId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "spanId": "span-001",
  "parentSpanId": null,
  "name": "api.integration-requests.create",
  "kind": "SERVER",
  "startTime": "2024-01-15T10:30:00.000Z",
  "endTime": "2024-01-15T10:30:00.150Z",
  "duration_ms": 150,
  "attributes": {
    "http.method": "POST",
    "http.url": "/api/integration-requests",
    "http.status_code": 202,
    "correlation_id": "550e8400-e29b-41d4-a716-446655440000",
    "external_id": "PARTNER-12345"
  },
  "status": {
    "code": "OK"
  }
}
```

---

### 3.3. Propagação de Contexto

**W3C Trace Context:**
O `CorrelationId` é propagado como header HTTP:

```http
POST /api/integration-requests
traceparent: 00-a1b2c3d4e5f67890abcdef1234567890-span001-01
X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000
```

**Propagação:**
1. API → Banco de Dados (via EF Core instrumentation)
2. API → Message Bus (header customizado)
3. Worker → Sistema Externo (header HTTP)

**Benefício:** Um único `TraceId` permite visualizar toda a jornada da requisição no Jaeger.

---

### 3.4. Jaeger UI (Exemplo)

**Visualização no Jaeger:**
```
Timeline:
|-- api.integration-requests.create (150ms)
    |-- db.insert (20ms)
    |-- messagebus.publish (5ms)
|-- worker.process_integration (2000ms)
    |-- worker.validate (50ms)
    |-- http.external.totvs (1800ms)
|-- db.update.completed (10ms)

Total Duration: 2160ms
Spans: 7
Errors: 0
```

**Drill-Down:**
Clicar em um span mostra logs correlacionados automaticamente.

---

## 4. Métricas

### 4.1. Framework: OpenTelemetry Metrics

**Configuração:**
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(meterProviderBuilder =>
    {
        meterProviderBuilder
            .AddMeter("IntegrationHub")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddConsoleExporter();
    });
```

**Exportação:**
- **Desenvolvimento:** Console
- **Produção:** Prometheus / Azure Monitor

---

### 4.2. Métricas Implementadas

#### 4.2.1. Métricas de Negócio

| Métrica | Tipo | Descrição | Labels |
|---------|------|-----------|--------|
| `integration_requests_total` | Counter | Total de requisições recebidas | `source_system`, `target_system` |
| `integration_requests_completed_total` | Counter | Total de integrações concluídas | `source_system`, `target_system` |
| `integration_requests_failed_total` | Counter | Total de integrações falhadas | `source_system`, `target_system`, `reason` |
| `integration_duration_seconds` | Histogram | Duração end-to-end (Received → Completed) | `source_system`, `target_system` |

**Exemplo de Consulta (PromQL):**
```promql
# Taxa de sucesso
rate(integration_requests_completed_total[5m]) / rate(integration_requests_total[5m])

# Latência p95
histogram_quantile(0.95, rate(integration_duration_seconds_bucket[5m]))
```

---

#### 4.2.2. Métricas de Sistema

| Métrica | Tipo | Descrição |
|---------|------|-----------|
| `http_requests_total` | Counter | Total de requests HTTP (ASP.NET Core) |
| `http_request_duration_seconds` | Histogram | Latência de requests HTTP |
| `process_cpu_seconds_total` | Counter | Uso de CPU do processo |
| `dotnet_gc_heap_size_bytes` | Gauge | Tamanho do heap do GC |
| `worker_active_tasks` | Gauge | Número de tasks ativas no worker |
| `messagebus_queue_depth` | Gauge | Profundidade da fila de mensagens |

---

#### 4.2.3. Métricas de Infraestrutura (Futuro)

| Métrica | Tipo | Descrição |
|---------|------|-----------|
| `db_connections_active` | Gauge | Conexões ativas no pool do EF Core |
| `db_query_duration_seconds` | Histogram | Latência de queries SQL |
| `external_system_call_duration_seconds` | Histogram | Latência de chamadas externas |
| `external_system_errors_total` | Counter | Erros em sistemas externos |

---

### 4.3. Dashboards Grafana

#### Dashboard 1: Visão Geral de Negócio

**Painéis:**
1. **Taxa de Requisições (req/s)**
   ```promql
   rate(integration_requests_total[5m])
   ```

2. **Taxa de Sucesso (%)**
   ```promql
   100 * rate(integration_requests_completed_total[5m]) / rate(integration_requests_total[5m])
   ```

3. **Taxa de Falha (%)**
   ```promql
   100 * rate(integration_requests_failed_total[5m]) / rate(integration_requests_total[5m])
   ```

4. **Latência (p50, p95, p99)**
   ```promql
   histogram_quantile(0.50, rate(integration_duration_seconds_bucket[5m]))
   histogram_quantile(0.95, rate(integration_duration_seconds_bucket[5m]))
   histogram_quantile(0.99, rate(integration_duration_seconds_bucket[5m]))
   ```

5. **Requisições por Sistema de Origem**
   ```promql
   sum by (source_system) (rate(integration_requests_total[5m]))
   ```

---

#### Dashboard 2: Saúde do Sistema

**Painéis:**
1. **CPU Usage (%)**
   ```promql
   rate(process_cpu_seconds_total[5m]) * 100
   ```

2. **Memory Usage (MB)**
   ```promql
   dotnet_gc_heap_size_bytes / 1024 / 1024
   ```

3. **HTTP Request Rate (req/s)**
   ```promql
   rate(http_requests_total[5m])
   ```

4. **HTTP Error Rate (5xx)**
   ```promql
   rate(http_requests_total{status=~"5.."}[5m])
   ```

5. **Worker Active Tasks**
   ```promql
   worker_active_tasks
   ```

6. **Message Queue Depth**
   ```promql
   messagebus_queue_depth
   ```

---

#### Dashboard 3: Sistemas Externos

**Painéis:**
1. **Latência por Sistema Externo (p95)**
   ```promql
   histogram_quantile(0.95, rate(external_system_call_duration_seconds_bucket[5m])) by (target_system)
   ```

2. **Taxa de Erro por Sistema Externo**
   ```promql
   rate(external_system_errors_total[5m]) by (target_system)
   ```

3. **Disponibilidade por Sistema Externo (%)**
   ```promql
   100 * (1 - rate(external_system_errors_total[5m]) / rate(external_system_call_total[5m]))
   ```

---

## 5. Alertas

### 5.1. Alertas Críticos

#### Alerta 1: Alta Taxa de Falha
```yaml
- alert: HighIntegrationFailureRate
  expr: |
    (
      rate(integration_requests_failed_total[5m]) 
      / 
      rate(integration_requests_total[5m])
    ) > 0.05
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "Taxa de falha acima de 5% nos últimos 5 minutos"
    description: "{{ $value | humanizePercentage }} das integrações estão falhando"
```

---

#### Alerta 2: Worker Não Está Consumindo
```yaml
- alert: WorkerNotConsuming
  expr: |
    increase(integration_requests_total{status="Received"}[5m]) > 0
    and
    increase(integration_requests_total{status="Processing"}[5m]) == 0
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "Worker não está processando requisições"
    description: "Há requisições aguardando processamento há mais de 5 minutos"
```

---

#### Alerta 3: Latência Alta
```yaml
- alert: HighIntegrationLatency
  expr: |
    histogram_quantile(0.95, rate(integration_duration_seconds_bucket[5m])) > 180
  for: 10m
  labels:
    severity: warning
  annotations:
    summary: "Latência p95 acima de 3 minutos"
    description: "95% das integrações estão demorando mais de 3 minutos"
```

---

#### Alerta 4: Sistema Externo Indisponível
```yaml
- alert: ExternalSystemDown
  expr: |
    (
      rate(external_system_errors_total[5m]) 
      / 
      rate(external_system_call_total[5m])
    ) > 0.5
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "Sistema externo {{ $labels.target_system }} com alta taxa de erro"
    description: "{{ $value | humanizePercentage }} das chamadas estão falhando"
```

---

### 5.2. Alertas de Infraestrutura

#### Alerta 5: Fila de Mensagens Cheia
```yaml
- alert: MessageQueueBacklog
  expr: messagebus_queue_depth > 1000
  for: 10m
  labels:
    severity: warning
  annotations:
    summary: "Fila de mensagens com backlog acima de 1000"
    description: "Há {{ $value }} mensagens aguardando processamento"
```

---

#### Alerta 6: Alto Uso de Memória
```yaml
- alert: HighMemoryUsage
  expr: dotnet_gc_heap_size_bytes / 1024 / 1024 > 2048
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "Uso de memória acima de 2GB"
    description: "Heap do GC está em {{ $value }}MB"
```

---

## 6. Rastreabilidade End-to-End

### 6.1. Fluxo de CorrelationId

```
1. Parceiro envia requisição com X-Correlation-ID (ou API gera um)
   ↓
2. API persiste CorrelationId no banco
   ↓
3. API publica evento com CorrelationId
   ↓
4. Worker consome evento e usa CorrelationId em logs
   ↓
5. Worker chama sistema externo com X-Correlation-ID
   ↓
6. Sistema externo responde (pode incluir CorrelationId)
   ↓
7. Todos os logs, traces e métricas incluem CorrelationId
```

### 6.2. Consulta por CorrelationId

**Exemplo: Buscar todos os logs de uma requisição:**

**Seq/Elasticsearch:**
```
CorrelationId = "550e8400-e29b-41d4-a716-446655440000"
```

**Jaeger:**
```
trace.correlation_id = "550e8400-e29b-41d4-a716-446655440000"
```

**Resultado:** Visão completa da jornada da requisição, incluindo:
- Tempo de resposta da API
- Latência do banco de dados
- Tempo de processamento do worker
- Latência do sistema externo
- Todos os erros ocorridos

---

## 7. Integração com Ferramentas

### 7.1. Stack de Observabilidade (Produção)

| Pilar | Ferramenta | Finalidade |
|-------|-----------|-----------|
| **Logs** | Seq / Elasticsearch + Kibana | Centralização e busca de logs |
| **Traces** | Jaeger / Zipkin | Rastreamento distribuído |
| **Métricas** | Prometheus + Grafana | Monitoramento e dashboards |
| **Alertas** | Prometheus AlertManager + PagerDuty | Notificações de incidentes |
| **APM** | Azure Application Insights | Visão unificada (logs + traces + métricas) |

---

### 7.2. Azure Application Insights (Recomendado para TOTVS)

**Vantagens:**
- ✅ Integração nativa com Azure
- ✅ Coleta automática de logs, traces e métricas
- ✅ Application Map (mapa de dependências)
- ✅ Live Metrics (métricas em tempo real)
- ✅ Alertas integrados

**Configuração:**
```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});
```

---

## 8. Melhores Práticas

### 8.1. Logs
- ✅ Use logging estruturado (Serilog)
- ✅ Sempre inclua `CorrelationId` e `RequestId`
- ✅ Evite logs excessivos (não logue em loops)
- ✅ Use níveis de log apropriados (Info, Warning, Error)
- ✅ Nunca logue dados sensíveis (senhas, tokens, PII)

### 8.2. Traces
- ✅ Use instrumentação automática (OpenTelemetry)
- ✅ Crie spans customizados para operações críticas
- ✅ Propague contexto entre serviços (W3C Trace Context)
- ✅ Inclua atributos relevantes (external_id, target_system)

### 8.3. Métricas
- ✅ Prefira contadores e histogramas sobre gauges
- ✅ Use labels consistentes (source_system, target_system)
- ✅ Evite cardinalidade alta (não use IDs únicos como labels)
- ✅ Documente todas as métricas customizadas

---

## 9. Conclusão

A estratégia de observabilidade implementada garante:
- ✅ **Rastreabilidade completa** via CorrelationId
- ✅ **Debugging facilitado** com logs estruturados
- ✅ **Performance monitoring** com métricas detalhadas
- ✅ **Alertas proativos** para incidentes
- ✅ **Visibilidade end-to-end** com distributed tracing
