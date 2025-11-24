# Pontos de Atenção - Integration Hub

## 1. Riscos Técnicos e Mitigações

### 1.1. Latência de Sistemas Externos

**Risco:**
Sistemas externos podem responder lentamente (> 5s) ou não responder, bloqueando o processamento de requisições.

**Impacto:**
- ⚠️ Acúmulo de mensagens na fila
- ⚠️ Timeout de requisições
- ⚠️ Degradação da experiência do usuário

**Mitigação:**

1. **Timeout Configurável:**
   ```csharp
   var httpClient = new HttpClient
   {
       Timeout = TimeSpan.FromSeconds(60) // Timeout agressivo
   };
   ```

2. **Circuit Breaker (Polly):**
   ```csharp
   var circuitBreakerPolicy = Policy
       .Handle<HttpRequestException>()
       .CircuitBreakerAsync(
           handledEventsAllowedBeforeBreaking: 5, // Abre circuito após 5 falhas
           durationOfBreak: TimeSpan.FromMinutes(1) // Mantém aberto por 1 minuto
       );
   ```

3. **Fallback para Modo Degradado:**
   - Se sistema externo indisponível > 10 minutos, aciona alertas
   - Requisições são marcadas como `WaitingExternal` e reprocessadas quando serviço volta

4. **SLA com Fornecedores:**
   - Definir SLA de latência (p95 < 2s)
   - Monitorar via métricas: `external_system_call_duration_seconds`

**Monitoramento:**
```promql
# Alerta se p95 > 5s
histogram_quantile(0.95, rate(external_system_call_duration_seconds_bucket[5m])) > 5
```

---

### 1.2. Versionamento de Contratos

**Risco:**
Mudanças em schemas de payload (breaking changes) quebram integrações existentes.

**Impacto:**
- ❌ Parceiros não conseguem enviar requisições
- ❌ Validações falham inesperadamente
- ❌ Dados corrompidos

**Mitigação:**

1. **Versionamento de API:**
   ```csharp
   [ApiVersion("1.0")]
   [ApiVersion("2.0")]
   [Route("api/v{version:apiVersion}/integration-requests")]
   public class IntegrationRequestsController : ControllerBase
   {
       [HttpPost]
       [MapToApiVersion("1.0")]
       public async Task<IActionResult> CreateV1([FromBody] CreateIntegrationRequestCommandV1 command)
       {
           // Implementação V1
       }

       [HttpPost]
       [MapToApiVersion("2.0")]
       public async Task<IActionResult> CreateV2([FromBody] CreateIntegrationRequestCommandV2 command)
       {
           // Implementação V2 com novos campos
       }
   }
   ```

2. **Backward Compatibility:**
   - Novos campos devem ser **opcionais** (nullable)
   - Nunca remover campos existentes
   - Deprecate antes de remover:
     ```csharp
     [Obsolete("Use 'NewFieldName' instead. Will be removed in v3.0")]
     public string OldFieldName { get; set; }
     ```

3. **Schema Validation com JSON Schema:**
   ```csharp
   public class PayloadValidator
   {
       public bool Validate(string payload, string schemaVersion)
       {
           var schema = GetSchema(schemaVersion); // Busca schema da versão
           var jsonSchema = JsonSchema.FromText(schema);
           var errors = jsonSchema.Validate(payload);
           return !errors.Any();
       }
   }
   ```

4. **Comunicação Proativa:**
   - Notificar parceiros com **90 dias de antecedência** de breaking changes
   - Manter versões antigas por **mínimo 6 meses** após deprecação
   - Documentar changelogs: `/docs/api-changelog.md`

**Monitoramento:**
```promql
# Alerta se uso de versão antiga > 10% após período de deprecação
sum(rate(http_requests_total{api_version="1.0"}[5m])) / sum(rate(http_requests_total[5m])) > 0.1
```

---

### 1.3. Falhas Intermitentes

**Risco:**
Falhas transientes em rede, banco de dados ou sistemas externos causam erros esporádicos.

**Impacto:**
- ⚠️ Requisições marcadas como `Failed` indevidamente
- ⚠️ Experiência inconsistente para parceiros
- ⚠️ Aumento de tickets de suporte

**Mitigação:**

1. **Retry com Exponential Backoff (Polly):**
   ```csharp
   var retryPolicy = Policy
       .Handle<HttpRequestException>()
       .Or<TimeoutException>()
       .WaitAndRetryAsync(
           retryCount: 3,
           sleepDurationProvider: retryAttempt => 
               TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
           onRetry: (exception, timeSpan, retryCount, context) =>
           {
               _logger.LogWarning(
                   "Retry {RetryCount} after {Duration}ms due to {Exception}",
                   retryCount, timeSpan.TotalMilliseconds, exception.GetType().Name
               );
           }
       );
   ```

2. **Jitter para Evitar Thundering Herd:**
   ```csharp
   var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
   var delay = baseDelay + jitter;
   await Task.Delay(delay);
   ```

3. **Idempotência:**
   - Toda operação deve ser **idempotente**
   - Se retry da mesma requisição, resultado deve ser idêntico
   - `ExternalId` como chave de idempotência:
     ```csharp
     // Se já existe, retorna 409 Conflict com link para recurso existente
     var existing = await _repository.GetByExternalIdAsync(command.ExternalId);
     if (existing != null)
     {
         return Conflict(new 
         { 
             message = "Request already exists",
             location = $"/api/integration-requests/{existing.Id}"
         });
     }
     ```

4. **Health Checks de Dependências:**
   ```csharp
   builder.Services.AddHealthChecks()
       .AddSqlServer(connectionString, name: "database")
       .AddRabbitMQ(rabbitConnectionString, name: "messagebus")
       .AddUrlGroup(new Uri("https://api.totvs.com.br/health"), name: "external_system");
   ```

**Monitoramento:**
```promql
# Alerta se taxa de retry > 10%
rate(http_client_retries_total[5m]) / rate(http_client_requests_total[5m]) > 0.1
```

---

### 1.4. Reprocessamento e Dead-Letter Queue (DLQ)

**Risco:**
Mensagens falhadas ficam presas na fila principal, bloqueando consumo de novas mensagens.

**Impacto:**
- ❌ Fila principal congestionada
- ❌ Mensagens válidas não são processadas
- ❌ SLA de latência violado

**Mitigação:**

1. **Dead-Letter Queue (DLQ):**
   ```csharp
   // RabbitMQ: Configurar DLX (Dead Letter Exchange)
   channel.ExchangeDeclare(
       exchange: "integration.events.dlq",
       type: ExchangeType.Fanout,
       durable: true
   );

   var queueArgs = new Dictionary<string, object>
   {
       { "x-dead-letter-exchange", "integration.events.dlq" },
       { "x-message-ttl", 86400000 }, // 24 horas
       { "x-max-retries", 3 }
   };

   channel.QueueDeclare(
       queue: "integration-orchestration-queue",
       durable: true,
       arguments: queueArgs
   );
   ```

2. **Worker de DLQ (Análise Manual):**
   ```csharp
   // Job separado que processa DLQ overnight ou sob demanda
   public class DlqProcessorWorker : BackgroundService
   {
       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
           await foreach (var message in _dlqChannel.ReadAllAsync(stoppingToken))
           {
               _logger.LogWarning("DLQ message | Id={Id} | Error={Error}", 
                   message.RequestId, message.ErrorMessage);
               
               // Notificar equipe de operações
               await _alertService.SendAlert($"DLQ message requires manual review: {message.RequestId}");
           }
       }
   }
   ```

3. **Reprocessamento Manual:**
   ```csharp
   [HttpPost("admin/retry/{id}")]
   [Authorize(Roles = "Admin")]
   public async Task<IActionResult> RetryFailedRequest(Guid id)
   {
       var request = await _repository.GetByIdAsync(id);
       if (request.Status != IntegrationStatus.Failed)
       {
           return BadRequest("Only failed requests can be retried");
       }

       // Reset status e republica evento
       await _service.UpdateStatusAsync(id, IntegrationStatus.Received);
       await _messageBus.PublishAsync(new IntegrationRequestCreated
       {
           RequestId = id,
           CorrelationId = request.CorrelationId,
           ExternalId = request.ExternalId
       });

       return Ok();
   }
   ```

**Monitoramento:**
```promql
# Alerta se DLQ > 100 mensagens
messagebus_dlq_depth > 100
```

---

### 1.5. Monitoramento de Fila

**Risco:**
Fila cresce descontroladamente devido a processamento lento ou falhas no worker.

**Impacto:**
- ⚠️ Latência aumenta drasticamente
- ⚠️ Out of Memory (OOM) no RabbitMQ
- ⚠️ SLA violado

**Mitigação:**

1. **Métricas de Fila:**
   ```csharp
   // Expor métrica Prometheus
   private static readonly Gauge QueueDepth = Metrics
       .CreateGauge("messagebus_queue_depth", "Depth of message queue");

   public async Task<int> GetQueueDepthAsync()
   {
       var management = new ManagementClient("http://rabbitmq:15672", "guest", "guest");
       var queue = await management.GetQueueAsync("integration-orchestration-queue");
       QueueDepth.Set(queue.MessagesReady);
       return queue.MessagesReady;
   }
   ```

2. **Auto-Scaling de Workers (KEDA):**
   ```yaml
   apiVersion: keda.sh/v1alpha1
   kind: ScaledObject
   metadata:
     name: integration-worker-scaler
   spec:
     scaleTargetRef:
       name: integration-worker
     minReplicaCount: 2
     maxReplicaCount: 10
     triggers:
       - type: rabbitmq
         metadata:
           queueName: integration-orchestration-queue
           queueLength: "50" # Escala se fila > 50 mensagens
   ```

3. **Alertas de Fila:**
   ```promql
   # Alerta se fila > 500 mensagens por 5 minutos
   messagebus_queue_depth > 500
   ```

4. **Backpressure:**
   ```csharp
   // Limitar concorrência do worker
   var semaphore = new SemaphoreSlim(10); // Máximo 10 mensagens simultâneas

   await semaphore.WaitAsync();
   try
   {
       await ProcessMessageAsync(message);
   }
   finally
   {
       semaphore.Release();
   }
   ```

---

## 2. Riscos de Negócio

### 2.1. Indisponibilidade de Parceiros

**Risco:**
Parceiro crítico (ex: SAP) fica indisponível, impedindo integrações.

**Mitigação:**
- Modo de contingência: Armazenar requisições e reprocessar quando parceiro voltar
- SLA claro com parceiros (uptime mínimo: 99.5%)
- Monitoramento proativo de sistemas parceiros

---

### 2.2. Mudanças Regulatórias

**Risco:**
Novas regulamentações (LGPD, BACEN) exigem mudanças rápidas.

**Mitigação:**
- Arquitetura flexível (fácil adicionar validações)
- Logs de auditoria completos (compliance desde o início)
- Consultor jurídico revisando contratos de integração

---

### 2.3. Crescimento Exponencial

**Risco:**
Adoção maior que esperada (10x volume de requisições em 6 meses).

**Mitigação:**
- Arquitetura preparada para escala horizontal
- Load testing regular (simular 10x carga atual)
- Monitoramento de capacidade (CPU, memória, IOPS)

---

## 3. Riscos Operacionais

### 3.1. Falta de Documentação

**Risco:**
Equipe nova não consegue manter/evoluir o sistema.

**Mitigação:**
- ✅ Documentação completa em `/docs`
- ✅ Diagramas de arquitetura (Mermaid)
- ✅ Runbooks para incidentes comuns
- ✅ README.md detalhado

---

### 3.2. Falta de Observabilidade

**Risco:**
Incidentes demoram horas para serem diagnosticados.

**Mitigação:**
- ✅ Logs estruturados com Serilog
- ✅ Distributed tracing com OpenTelemetry
- ✅ Dashboards Grafana com métricas críticas
- ✅ Alertas em PagerDuty/OpsGenie

---

### 3.3. Deploy Manual

**Risco:**
Erro humano durante deploy causa downtime.

**Mitigação:**
- CI/CD automatizado (GitHub Actions / Azure DevOps)
- Blue-Green Deployment
- Testes automatizados (unit + integration + E2E)
- Rollback automático se health check falha

---

## 4. Priorização de Riscos

| Risco | Probabilidade | Impacto | Prioridade | Mitigação |
|-------|---------------|---------|------------|-----------|
| **Latência Externa** | Alta | Alto | 🔴 Crítico | Circuit Breaker + Timeout |
| **Breaking Changes** | Média | Alto | 🟠 Alto | Versionamento de API |
| **Falhas Intermitentes** | Alta | Médio | 🟠 Alto | Retry + Idempotência |
| **Fila Cheia** | Média | Alto | 🟠 Alto | Auto-Scaling + DLQ |
| **Falta de Docs** | Baixa | Médio | 🟡 Médio | Já mitigado (docs criados) |
| **Crescimento 10x** | Baixa | Alto | 🟡 Médio | Load Testing + Escala Horizontal |

---

## 5. Checklist de Produção

### 5.1. Antes do Go-Live

- [ ] Load testing com 10x carga esperada
- [ ] Chaos engineering (simular falhas)
- [ ] Runbooks criados para top 5 incidentes
- [ ] Alertas testados (PagerDuty)
- [ ] Dashboards Grafana configurados
- [ ] SLAs definidos com parceiros
- [ ] Plano de rollback testado
- [ ] Backup/restore testado

---

### 5.2. Primeiros 30 Dias

- [ ] Monitorar métricas diariamente
- [ ] Revisar DLQ semanalmente
- [ ] Post-mortem de incidentes
- [ ] Ajustar alertas (reduzir falsos positivos)
- [ ] Coletar feedback de parceiros
- [ ] Otimizar queries lentas

---

## 6. Conclusão

Os principais pontos de atenção identificados são:
- ✅ **Mitigados:** Latência externa (circuit breaker), falhas intermitentes (retry)
- ⚠️ **Monitorar:** Profundidade da fila, taxa de erro de sistemas externos
- 🔄 **Melhorar:** Auto-scaling, observabilidade avançada, chaos engineering
