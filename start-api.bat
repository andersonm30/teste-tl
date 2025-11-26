@echo off
echo ===============================================================
echo   INICIANDO INTEGRATION HUB API
echo   Porta HTTPS: 7000
echo   Porta HTTP: 5000
echo   Swagger: https://localhost:7000
echo ===============================================================
echo.
echo Aguarde...
echo.

cd /d %~dp0
dotnet run --project src/IntegrationHub.Api/IntegrationHub.Api.csproj

pause
