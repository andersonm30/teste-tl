@echo off
echo ===============================================================
echo   INICIANDO INTEGRATION HUB WORKER
echo ===============================================================
echo.
echo Aguarde...
echo.

cd /d %~dp0
dotnet run --project src/IntegrationHub.Worker/IntegrationHub.Worker.csproj

pause
