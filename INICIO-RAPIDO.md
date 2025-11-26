# 🚀 INÍCIO RÁPIDO

## ⚡ Como Executar (3 passos)

### **Método 1: Arquivos .BAT (Mais Fácil)**

1. **Duplo clique em:** `start-api.bat`
2. **Duplo clique em:** `start-worker.bat` (nova janela)
3. **Abrir navegador em:** https://localhost:7000

---

### **Método 2: PowerShell**

**Terminal 1 (API):**
```powershell
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj
```
**NÃO feche! Aguarde ver:** `Now listening on: https://localhost:7000`

**Terminal 2 (Worker):**
```powershell
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj
```
**NÃO feche! Deixe rodando**

**Terminal 3 (Demo):**
```powershell
.\demo.ps1
```

---

## ✅ Verificar se Está Funcionando

**Abrir navegador:**
- Swagger: https://localhost:7000
- Health Check: https://localhost:7000/api/health

---

## 🧪 Testar via PowerShell

```powershell
# Criar requisição
$body = @{
    externalId = "ORDER-$(Get-Date -Format 'yyyyMMddHHmmss')"
    sourceSystem = "SAP"
    targetSystem = "TotvsProtheus"
    payload = @{ orderId = "ORD-001" }
} | ConvertTo-Json

Invoke-RestMethod -Uri "https://localhost:7000/api/integration-requests" `
    -Method POST -Body $body -ContentType "application/json"
```

---

## ⚠️ IMPORTANTE

- **NÃO** pressione Ctrl+C nos terminais da API/Worker
- Se fechar por acidente, execute novamente
- Para parar: Ctrl+C em cada terminal

---

## 📝 Documentação Completa

Ver arquivo: `DEMO-RAPIDO.md`
