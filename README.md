# Medical Billing Testing Demo

A fully synthetic claim-repricing app built to demonstrate unit, component, integration and API
end-to-end testing in .NET, including Testcontainers. **All data is invented; there is no PHI.**

## Prerequisites
- .NET SDK from `global.json` (.NET 10)
- Docker (SQL Server image is **x64-only**)
- `dotnet tool restore` (restores the repo-local `dotnet-ef`; only needed to add migrations)
- A trusted HTTPS development certificate for the Aspire dashboard: `dotnet dev-certs https --trust` once per
  machine (Windows asks you to confirm). Do this before the talk, not on stage.

Open `Billing.slnx` in Visual Studio 2022 17.13+ or Visual Studio 2026.

## Run the tests
```bash
dotnet test --filter "Category=Unit"          # instant, no Docker
dotnet test --filter "Category=Integration"   # starts one SQL Server container for the whole run
dotnet test                                   # everything
```

## Run the app (Aspire)
`Billing.AppHost` orchestrates everything: a SQL Server container (`sql`, database `Billing`), the API (`api`),
and the Blazor UI (`web`). No `.env` or password setup: Aspire generates the SQL Server password and stores it
in the AppHost's user secrets.

- **Visual Studio:** set `Billing.AppHost` as the startup project and press F5.
- **Command line:** `./scripts/run-demo.ps1` (or `dotnet run --project src/Billing.AppHost`).

The Aspire dashboard opens with every resource, its logs, traces and metrics; the UI is at http://localhost:5102.
The SQL Server container is **persistent** with a data volume, so after the first start it is reused across runs
(stop it from Docker Desktop to reclaim it). The web app finds the API through service discovery (`https+http://api`).

## Layout
`Domain` (pure engine) · `Data` (EF Core) · `Contracts` (DTOs) · `Api` (Controllers) · `Web` (Blazor) ·
`AppHost` (Aspire orchestration) · `ServiceDefaults` (OpenTelemetry, health checks, service discovery, resilience).
Tests: `Domain.Tests` (unit) · `Web.Tests` (bUnit) · `Integration.Tests` (Testcontainers + `WebApplicationFactory`).

Aspire runs the app; it does not change the tests. The integration tests still start their own throwaway SQL Server
with Testcontainers, so they stay independent of the AppHost and of any data in the persistent dev container.

## Checkpoints
| Tag | State |
|---|---|
| `cp-1-scaffold` | Empty solution building green |
| `cp-2-engine-no-mpd` | Engine without the multiple-procedure discount, with unit tests |
| `cp-3-data-api` | Data layer, API, container-backed tests |
| `cp-4-ui` / branch `demo-start` | Full app, **discount not yet implemented** |
| `cp-5-mpd-complete` / branch `demo-finished` | Discount tests + implementation |

## Talk arc
1. Tour the solution and the test pyramid.
2. Run the unit tests (instant).
3. **Live-code the discount** from `demo-start` (script below).
4. Run the integration tests; show `docker ps` in a second terminal while the container starts.
5. Show the effective-date boundary tests: why a mock could not verify them.
6. Run the API tests (`WebApplicationFactory` + `FakeTimeProvider`).
7. Start the AppHost, open the UI from the Aspire dashboard, edit a bill, click **Process Bill**, and watch the
   discounted totals appear; then open the dashboard's Traces page: one trace spans the button click in `web` and the request in `api`.

## Live-coding script (multiple-procedure discount)
Rule: rank priced lines by amount after units and modifiers (highest first, ties by lowest line number);
rank 1 pays 100%, every other rank pays 50%. Round once per line half-up; cap at billed charges.
Write the tests in `tests/Billing.Domain.Tests/MultipleProcedureDiscountTests.cs`, one at a time, red then green:
1. single line is not discounted (already green: say why)
2. highest-priced line pays full, the other pays half
3. three lines: every non-top rank pays half
4. ties rank by lowest line number
5. unpriced lines are excluded from ranking
6. half-cent after discount rounds up
7. discounted amount is still capped at billed charges
8. same result when the lines arrive in a different order

## Demo-day readiness
- The day before: `./scripts/pull-images.ps1`, then run the integration tests once while online.
- **Fallback if Docker misbehaves:** show a saved terminal capture of a green integration run; unit and
  component tests need no Docker.
- Rehearse the live-coding segment from `demo-start` twice. `git checkout demo-finished` resets.

## Known exceptions
- `Billing.Web.Tests` suppresses NuGet advisory GHSA-pgww-w46g-26qg for AngleSharp 1.2.0. bUnit 1.40 (the last 1.x)
  is compiled against that version and patched AngleSharp releases are binary-incompatible with it. It is a
  test-only dependency; remove the suppression when moving to bUnit 2.x.
