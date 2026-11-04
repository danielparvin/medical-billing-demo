# RUN: Run and observe the application

[Back to the capabilities index](README.md)

**RUN** The AppHost shall run the database, Billing API and Bill workspace together, and both services shall report their health and telemetry. (untested)

None of these requirements has an automated test: the integration tests start their own SQL Server and do not use the AppHost.

## RUN-1 Orchestrate the application

**RUN-1** When the AppHost starts, the AppHost shall start a SQL Server container with a `Billing` database, the Billing API and the Bill workspace. (untested)

- **RUN-1.1** The AppHost shall hold the Billing API back until the database is ready. (untested)
- **RUN-1.2** The AppHost shall hold the Bill workspace back until the Billing API's health check passes. (untested)
- **RUN-1.3** The AppHost shall keep the SQL Server container and its data volume between runs. (untested)
- **RUN-1.4** The AppHost shall take the SQL Server password from the secret parameter `sql-password`. (untested)
  - **RUN-1.4.1** If the `sql-password` value is missing, then the AppHost shall ask for it instead of generating one. (untested)

Evidence: [AppHost.cs](../../src/Billing.AppHost/AppHost.cs), [README.md](../../README.md).

## RUN-2 Prepare the database on startup

**RUN-2** Where `Database:MigrateAndSeedOnStartup` is true, the Billing API shall apply database migrations and seed the fee schedule when it starts. (untested)

- **RUN-2.1** If the `ConnectionStrings:Billing` setting is missing, then the Billing API shall fail with an error naming the setting when the database is first needed. (untested)

Evidence: [Program.cs](../../src/Billing.Api/Program.cs), [appsettings.Development.json](../../src/Billing.Api/appsettings.Development.json). The setting is on in Development only. The seeding itself is specified in [FEE-3](fee-schedule.md#fee-3-seed-the-synthetic-fee-schedule).

## RUN-3 Report health

**RUN-3** While running in the Development environment, the Billing API and Bill workspace shall expose health endpoints at `/health` and `/alive`. (untested)

- **RUN-3.1** While running in the Development environment, the Billing API and Bill workspace shall report at `/health` that all health checks pass. (untested)
- **RUN-3.2** While running in the Development environment, the Billing API and Bill workspace shall report at `/alive` that the checks tagged `live` pass. (untested)

Evidence: [Extensions.cs](../../src/Billing.ServiceDefaults/Extensions.cs).

## RUN-4 Emit telemetry

**RUN-4** The Billing API and Bill workspace shall produce OpenTelemetry logs, metrics and traces. (untested)

- **RUN-4.1** The Billing API and Bill workspace shall include ASP.NET Core and outgoing HTTP activity in traces, so one trace can span a request from the Bill workspace to the Billing API. (untested)
- **RUN-4.2** The Billing API and Bill workspace shall leave health endpoint requests out of traces. (untested)
- **RUN-4.3** Where `OTEL_EXPORTER_OTLP_ENDPOINT` is set, the Billing API and Bill workspace shall export telemetry over OTLP. (untested)

Evidence: [Extensions.cs](../../src/Billing.ServiceDefaults/Extensions.cs), [README.md](../../README.md).

## RUN-5 Connect the services reliably

**RUN-5** The Bill workspace shall find the Billing API by the service name in `Api:BaseUrl`. (untested)

- **RUN-5.1** When an outgoing HTTP request fails transiently, the Bill workspace shall apply the standard resilience handler and retry only safe HTTP methods, so that a retried POST cannot create a duplicate bill. (untested)
- **RUN-5.2** If the `Api:BaseUrl` setting is missing, then the Bill workspace shall fail at startup with an error naming the setting. (untested)

Evidence: [Program.cs](../../src/Billing.Web/Program.cs), [appsettings.json](../../src/Billing.Web/appsettings.json), [Extensions.cs](../../src/Billing.ServiceDefaults/Extensions.cs).
