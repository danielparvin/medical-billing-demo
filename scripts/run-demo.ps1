# Starts SQL Server (docker compose), then the API and the Blazor UI.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Test-Path .env)) { throw "Create .env from .env.example and set MSSQL_SA_PASSWORD first." }

Get-Content .env |
    Where-Object { $_ -match '^\s*[^#\s].*=' } |
    ForEach-Object { $k, $v = $_ -split '=', 2; Set-Item -Path "env:$($k.Trim())" -Value $v.Trim() }

docker compose up -d --wait

$env:ConnectionStrings__Billing =
    "Server=localhost,14330;Database=Billing;User Id=sa;Password=$($env:MSSQL_SA_PASSWORD);TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = 'Development'

Start-Process dotnet -ArgumentList 'run --project src/Billing.Api --launch-profile http'
Start-Process dotnet -ArgumentList 'run --project src/Billing.Web --launch-profile http'
Write-Host "API: http://localhost:5101   UI: http://localhost:5102"
