# Starts the whole demo through the Aspire AppHost: SQL Server container, API, and Blazor UI.
# The Aspire dashboard opens in the browser; the UI is the "web" resource's endpoint (http://localhost:5102).
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet run --project src/Billing.AppHost --launch-profile https
