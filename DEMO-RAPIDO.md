# 🚀 DEMO RÁPIDO - Integration Hub

Guia prático para demonstrar a solução funcionando em **5 minutos**.

---

## ⚡ Setup Rápido (30 segundos)

### 1. Abrir 2 terminais no VS Code

**Terminal 1:** API  
**Terminal 2:** Worker

### 2. Restaurar e compilar (apenas na primeira vez)

```powershell
dotnet restore
dotnet build
```

---

## 🎬 DEMO - Passo a Passo

### **Terminal 1: Iniciar a API**

```powershell
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj
```

✅ Aguarde até ver:
```
Now listening on: https://localhost:7000
Now listening on: http://localhost:5000
```

### **Terminal 2: Iniciar o Worker**

```powershell
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj
```

✅ Aguarde até ver:
```
Application started. Press Ctrl+C to shut down.
```

---

## 🧪 Testar a Solução

### **Opção 1: Usar o Swagger (Visual)**

1. Abrir navegador em: **https://localhost:7000**
2. Clicar em `POST /api/integration-requests`
3. Clicar em **"Try it out"**
4. Usar este JSON de exemplo:

```json
{
  "externalId": "ORDER-2024-001",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus",
  "payload": {
    "orderId": "ORD-12345",
    "customer": "Empresa ABC Ltda",
    "amount": 15000.00,
    "items": [
      {
        "product": "Produto A",
        "quantity": 10,
        "price": 1500.00
      }
    ]
  }
}
```

5. Clicar em **"Execute"**
6. Ver resposta **202 Accepted** com ID gerado

---

### **Opção 2: Usar PowerShell (Terminal 3)**

```powershell
# Criar uma requisição de integração
$body = @{
    externalId = "ORDER-2024-001"
    sourceSystem = "SAP"
    targetSystem = "TotvsProtheus"
    payload = @{
        orderId = "ORD-12345"
        customer = "Empresa ABC Ltda"
        amount = 15000.00
    }
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck

# Mostrar resposta
$response | ConvertTo-Json

# Guardar o ID para consultar depois
$requestId = $response.id
Write-Host "Request ID: $requestId" -ForegroundColor Green
```

---

### **Verificar o Processamento**

#### No Terminal do Worker, você verá os logs:

```
[INFO] Integration request received | ExternalId=ORDER-2024-001
[INFO] Status updated | OldStatus=Received | NewStatus=Processing
[INFO] Payload validated successfully
[INFO] Status updated | OldStatus=Processing | NewStatus=WaitingExternal
[INFO] Calling external system | TargetSystem=TotvsProtheus
[INFO] External system responded successfully
[INFO] Status updated | OldStatus=WaitingExternal | NewStatus=Completed
```

---

### **Consultar Status da Requisição**

#### Via Swagger:
1. Usar `GET /api/integration-requests/{id}`
2. Colar o ID recebido
3. Ver status atualizado

#### Via PowerShell:
```powershell
# Consultar status (use o $requestId do passo anterior)
$status = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests/$requestId" `
    -Method GET `
    -SkipCertificateCheck

$status | ConvertTo-Json
```

#### Resposta esperada:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "externalId": "ORDER-2024-001",
  "sourceSystem": "SAP",
  "targetSystem": "TotvsProtheus",
  "status": "Completed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2024-11-26T10:30:00Z",
  "updatedAt": "2024-11-26T10:30:05Z"
}
```

---

### **Listar Todas as Requisições**

```powershell
# Listar todas
$all = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method GET `
    -SkipCertificateCheck

$all | ConvertTo-Json
```

---

## 🎯 Demonstração dos Conceitos-Chave

### 1️⃣ **Processamento Assíncrono**

✅ **Mostrar:** API responde **imediatamente** com `202 Accepted`  
✅ **Mostrar:** Worker processa em **background** (logs no Terminal 2)  
✅ **Explicar:** Cliente não espera processamento completo

### 2️⃣ **Rastreabilidade (CorrelationId)**

✅ **Mostrar:** Cada requisição tem um `correlationId` único  
✅ **Mostrar:** CorrelationId aparece em **todos os logs**  
✅ **Explicar:** Permite rastrear requisição end-to-end

### 3️⃣ **Máquina de Estados**

✅ **Mostrar nos logs:** Transições de estado
```
Received → Processing → WaitingExternal → Completed
```

✅ **Explicar:** Cada estado representa uma etapa do workflow

### 4️⃣ **Clean Architecture**

✅ **Mostrar estrutura de pastas:**
```
src/
├── IntegrationHub.Api         (Apresentação)
├── IntegrationHub.Application (Casos de Uso)
├── IntegrationHub.Domain      (Regras de Negócio)
└── IntegrationHub.Infrastructure (Persistência/External)
```

✅ **Explicar:** Domínio não depende de infraestrutura

### 5️⃣ **Event-Driven**

✅ **Mostrar:** API publica evento → Worker consome  
✅ **Explicar:** Desacoplamento via mensageria (InMemory → RabbitMQ futuro)

### 6️⃣ **Observabilidade**

✅ **Mostrar logs estruturados** no console  
✅ **Explicar:** Logs em JSON prontos para Elasticsearch/Seq

---

## 🧪 Cenários de Teste Avançados

### **Teste 1: Múltiplas Requisições**

```powershell
# Criar 5 requisições rapidamente
1..5 | ForEach-Object {
    $body = @{
        externalId = "ORDER-2024-00$_"
        sourceSystem = "SAP"
        targetSystem = "TotvsProtheus"
        payload = @{ orderId = "ORD-00$_" }
    } | ConvertTo-Json
    
    Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method POST -Body $body -ContentType "application/json" `
        -SkipCertificateCheck
    
    Write-Host "Criada requisição $_" -ForegroundColor Cyan
}
```

✅ **Observar:** Worker processa todas em sequência  
✅ **Explicar:** Fila absorve picos de carga

### **Teste 2: Consultar Health Check**

```powershell
# Health check público
Invoke-RestMethod -Uri "https://localhost:7000/api/health" `
    -Method GET -SkipCertificateCheck

# Health check seguro (requer JWT - demonstração)
# Invoke-RestMethod -Uri "https://localhost:7000/api/health/secure" -Method GET
```

### **Teste 3: Duplicidade (Idempotência)**

```powershell
# Tentar criar requisição com mesmo ExternalId
$body = @{
    externalId = "ORDER-2024-001"  # Mesmo ID anterior
    sourceSystem = "SAP"
    targetSystem = "TotvsProtheus"
    payload = @{ orderId = "ORD-12345" }
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method POST -Body $body -ContentType "application/json" `
        -SkipCertificateCheck
} catch {
    Write-Host "Erro esperado: ExternalId já existe (idempotência)" -ForegroundColor Yellow
    $_.ErrorDetails.Message
}
```

---

## 📊 Executar Testes Unitários

```powershell
# Rodar todos os testes
dotnet test

# Resultado esperado:
# Test summary: total: 17; failed: 0; succeeded: 17
```

---

## 🎨 Demonstração Visual (Swagger)

### **Fluxo Completo no Swagger:**

1. **Abrir:** https://localhost:7000
2. **Expandir:** `POST /api/integration-requests`
3. **Criar** uma requisição
4. **Copiar** o `id` retornado
5. **Expandir:** `GET /api/integration-requests/{id}`
6. **Consultar** com o ID copiado
7. **Ver** status `Completed`
8. **Expandir:** `GET /api/integration-requests`
9. **Listar** todas as requisições

---

## 💡 Pontos-Chave para Destacar

### **Arquitetura:**
- ✅ Clean Architecture (4 camadas desacopladas)
- ✅ Event-Driven (mensageria)
- ✅ Repository Pattern
- ✅ Domain Events

### **Funcionalidades:**
- ✅ API REST com Swagger
- ✅ Processamento assíncrono
- ✅ Rastreabilidade via CorrelationId
- ✅ Máquina de estados (workflow)
- ✅ Idempotência (ExternalId único)

### **Qualidade:**
- ✅ 17 testes unitários (100% passando)
- ✅ Logs estruturados (Serilog)
- ✅ OpenTelemetry preparado
- ✅ Tratamento de exceções global

### **Produção Ready:**
- ✅ Health checks
- ✅ CORS configurável
- ✅ JWT authentication (preparado)
- ✅ InMemory → SQL Server/RabbitMQ (evolução)

---

## 🛑 Parar a Demo

**Terminal 1 e 2:**
```
Ctrl + C
```

---

## 📝 Script Completo para Demo Rápida

```powershell
# 1. Abrir primeiro terminal e executar:
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj

# 2. Abrir segundo terminal e executar:
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj

# 3. Abrir terceiro terminal e criar requisição:
$body = @{
    externalId = "DEMO-$(Get-Date -Format 'yyyyMMddHHmmss')"
    sourceSystem = "SAP"
    targetSystem = "TotvsProtheus"
    payload = @{
        orderId = "ORD-DEMO-001"
        customer = "Cliente Demo"
        amount = 5000.00
    }
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method POST `
    -Body $body `
    -ContentType "application/json" `
    -SkipCertificateCheck

Write-Host "`n✅ Requisição criada com sucesso!" -ForegroundColor Green
Write-Host "ID: $($response.id)" -ForegroundColor Cyan
Write-Host "Status: $($response.status)" -ForegroundColor Yellow
Write-Host "CorrelationId: $($response.correlationId)" -ForegroundColor Magenta

# 4. Aguardar 2 segundos e consultar status
Start-Sleep -Seconds 2

$status = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests/$($response.id)" `
    -Method GET `
    -SkipCertificateCheck

Write-Host "`n✅ Status atualizado:" -ForegroundColor Green
$status | ConvertTo-Json
```

---

## 🎯 Tempo Estimado da Demo

- **Setup:** 30 segundos
- **Executar API + Worker:** 20 segundos
- **Criar requisição via Swagger:** 1 minuto
- **Consultar status:** 30 segundos
- **Mostrar logs:** 30 segundos
- **Executar testes:** 30 segundos

**TOTAL:** ~5 minutos ⏱️

---

**Pronto! Agora você tem um roteiro completo para demonstrar a solução de forma profissional! 🚀**
