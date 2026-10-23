# Run the day before the talk, while online.
# The same SQL Server image serves both the Aspire AppHost and the Testcontainers integration tests.
docker pull mcr.microsoft.com/mssql/server:2022-latest
Write-Host "Now run 'dotnet test --filter Category=Integration' once so the Testcontainers helper image (Ryuk) is cached too."
