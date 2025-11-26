# Script de Demo Automatizado - Integration Hub
# Demonstra a solução funcionando em poucos comandos

Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   INTEGRATION HUB - Demo Automatizado                    ║" -ForegroundColor Cyan
Write-Host "║   Teste Técnico TOTVS - Tech Lead .NET                   ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Função para verificar se API está rodando
function Test-ApiRunning {
    try {
        $null = Invoke-RestMethod -Uri "https://localhost:7000/api/health" `
            -Method GET -SkipCertificateCheck -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

# Verificar se API está rodando
Write-Host "🔍 Verificando se a API está rodando..." -ForegroundColor Yellow

if (-not (Test-ApiRunning)) {
    Write-Host "❌ API não está rodando!" -ForegroundColor Red
    Write-Host "`n📝 Por favor, execute primeiro:" -ForegroundColor Yellow
    Write-Host "   Terminal 1: dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj" -ForegroundColor White
    Write-Host "   Terminal 2: dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj`n" -ForegroundColor White
    exit 1
}

Write-Host "✅ API está rodando!`n" -ForegroundColor Green

# Criar múltiplas requisições de teste
Write-Host "📋 Criando requisições de integração..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

$requests = @()

# Requisição 1: SAP → Totvs Protheus
Write-Host "[1/3] 🔄 SAP → Totvs Protheus" -ForegroundColor Yellow
$body1 = @{
    externalId = "SAP-ORDER-$(Get-Date -Format 'yyyyMMddHHmmss')"
    sourceSystem = "SAP"
    targetSystem = "TotvsProtheus"
    payload = @{
        orderId = "ORD-SAP-001"
        customer = "Empresa XYZ Ltda"
        amount = 25000.00
        currency = "BRL"
        items = @(
            @{ product = "Produto A"; quantity = 10; unitPrice = 1500.00 },
            @{ product = "Produto B"; quantity = 5; unitPrice = 2000.00 }
        )
    }
} | ConvertTo-Json -Depth 10

try {
    $response1 = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method POST `
        -Body $body1 `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    Write-Host "   ✅ Criada: ID = $($response1.id)" -ForegroundColor Green
    Write-Host "   📊 Status: $($response1.status)" -ForegroundColor White
    Write-Host "   🔗 CorrelationId: $($response1.correlationId)`n" -ForegroundColor Gray
    $requests += $response1
} catch {
    Write-Host "   ❌ Erro: $($_.Exception.Message)`n" -ForegroundColor Red
}

Start-Sleep -Milliseconds 500

# Requisição 2: Salesforce → Totvs
Write-Host "[2/3] 🔄 Salesforce → Totvs" -ForegroundColor Yellow
$body2 = @{
    externalId = "SFDC-LEAD-$(Get-Date -Format 'yyyyMMddHHmmss')"
    sourceSystem = "Salesforce"
    targetSystem = "Totvs"
    payload = @{
        leadId = "LEAD-12345"
        company = "Tech Corp Solutions"
        contact = "João Silva"
        email = "joao.silva@techcorp.com"
        value = 50000.00
    }
} | ConvertTo-Json -Depth 10

try {
    $response2 = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method POST `
        -Body $body2 `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    Write-Host "   ✅ Criada: ID = $($response2.id)" -ForegroundColor Green
    Write-Host "   📊 Status: $($response2.status)" -ForegroundColor White
    Write-Host "   🔗 CorrelationId: $($response2.correlationId)`n" -ForegroundColor Gray
    $requests += $response2
} catch {
    Write-Host "   ❌ Erro: $($_.Exception.Message)`n" -ForegroundColor Red
}

Start-Sleep -Milliseconds 500

# Requisição 3: Legacy System → Totvs
Write-Host "[3/3] 🔄 Legacy System → Totvs" -ForegroundColor Yellow
$body3 = @{
    externalId = "LEGACY-INV-$(Get-Date -Format 'yyyyMMddHHmmss')"
    sourceSystem = "LegacyERP"
    targetSystem = "Totvs"
    payload = @{
        invoiceId = "INV-99999"
        date = (Get-Date -Format "yyyy-MM-dd")
        customer = "Cliente ABC"
        total = 15750.00
    }
} | ConvertTo-Json -Depth 10

try {
    $response3 = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method POST `
        -Body $body3 `
        -ContentType "application/json" `
        -SkipCertificateCheck
    
    Write-Host "   ✅ Criada: ID = $($response3.id)" -ForegroundColor Green
    Write-Host "   📊 Status: $($response3.status)" -ForegroundColor White
    Write-Host "   🔗 CorrelationId: $($response3.correlationId)`n" -ForegroundColor Gray
    $requests += $response3
} catch {
    Write-Host "   ❌ Erro: $($_.Exception.Message)`n" -ForegroundColor Red
}

# Aguardar processamento
Write-Host "⏳ Aguardando processamento (3 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Consultar status atualizado
Write-Host "`n📊 Consultando status atualizado..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

foreach ($req in $requests) {
    try {
        $updated = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests/$($req.id)" `
            -Method GET `
            -SkipCertificateCheck
        
        $statusColor = switch ($updated.status) {
            "Completed" { "Green" }
            "Processing" { "Yellow" }
            "Failed" { "Red" }
            default { "White" }
        }
        
        Write-Host "📄 ExternalId: $($updated.externalId)" -ForegroundColor White
        Write-Host "   Status: $($updated.status)" -ForegroundColor $statusColor
        Write-Host "   Origem: $($updated.sourceSystem) → Destino: $($updated.targetSystem)" -ForegroundColor Gray
        Write-Host "   Criado: $($updated.createdAt)" -ForegroundColor Gray
        Write-Host "   Atualizado: $($updated.updatedAt)`n" -ForegroundColor Gray
    } catch {
        Write-Host "❌ Erro ao consultar $($req.id): $($_.Exception.Message)`n" -ForegroundColor Red
    }
}

# Listar todas as requisições
Write-Host "📋 Listando todas as requisições..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

try {
    $allRequests = Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
        -Method GET `
        -SkipCertificateCheck
    
    Write-Host "Total de requisições: $($allRequests.Count)" -ForegroundColor Green
    
    # Estatísticas por status
    $stats = $allRequests | Group-Object -Property status
    Write-Host "`n📊 Estatísticas por Status:" -ForegroundColor Cyan
    foreach ($stat in $stats) {
        $color = switch ($stat.Name) {
            "Completed" { "Green" }
            "Processing" { "Yellow" }
            "Failed" { "Red" }
            default { "White" }
        }
        Write-Host "   $($stat.Name): $($stat.Count)" -ForegroundColor $color
    }
    
    # Estatísticas por sistema
    $sourceSystems = $allRequests | Group-Object -Property sourceSystem
    Write-Host "`n📊 Estatísticas por Sistema de Origem:" -ForegroundColor Cyan
    foreach ($sys in $sourceSystems) {
        Write-Host "   $($sys.Name): $($sys.Count)" -ForegroundColor White
    }
    
} catch {
    Write-Host "❌ Erro ao listar requisições: $($_.Exception.Message)" -ForegroundColor Red
}

# Health Check
Write-Host "`n🏥 Verificando Health Check..." -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

try {
    $health = Invoke-RestMethod -Uri "https://localhost:7000/api/health" `
        -Method GET `
        -SkipCertificateCheck
    
    Write-Host "✅ API Status: $($health.status)" -ForegroundColor Green
    Write-Host "   Timestamp: $($health.timestamp)`n" -ForegroundColor Gray
} catch {
    Write-Host "❌ Erro ao verificar health: $($_.Exception.Message)`n" -ForegroundColor Red
}

# Resumo final
Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                  ✅ DEMO CONCLUÍDA                        ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝`n" -ForegroundColor Green

Write-Host "📝 Próximos passos:" -ForegroundColor Cyan
Write-Host "   1. Verificar logs no Terminal do Worker" -ForegroundColor White
Write-Host "   2. Acessar Swagger: https://localhost:7000" -ForegroundColor White
Write-Host "   3. Explorar API REST manualmente" -ForegroundColor White
Write-Host "   4. Executar testes: dotnet test`n" -ForegroundColor White

Write-Host "🎯 Conceitos demonstrados:" -ForegroundColor Cyan
Write-Host "   ✅ Clean Architecture (4 camadas)" -ForegroundColor Green
Write-Host "   ✅ Event-Driven Architecture" -ForegroundColor Green
Write-Host "   ✅ Processamento assíncrono" -ForegroundColor Green
Write-Host "   ✅ Rastreabilidade (CorrelationId)" -ForegroundColor Green
Write-Host "   ✅ Máquina de estados (workflow)" -ForegroundColor Green
Write-Host "   ✅ API REST + Swagger" -ForegroundColor Green
Write-Host "   ✅ Observabilidade (logs estruturados)`n" -ForegroundColor Green
