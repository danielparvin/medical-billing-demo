# Medical Billing Testing Demo

A fully synthetic claim-repricing app built to demonstrate unit, component, integration and API
end-to-end testing in .NET, including Testcontainers. **All data is invented; there is no PHI.**

## Prerequisites
- .NET SDK from `global.json` (.NET 10)
- Docker (SQL Server image is **x64-only**)
- `dotnet tool restore` (restores the repo-local `dotnet-ef`; only needed to add migrations)

Open `Billing.slnx` in Visual Studio 2022 17.13+ or Visual Studio 2026.

## Run the tests
```bash
dotnet test --filter "Category=Unit"          # instant, no Docker
dotnet test --filter "Category=Integration"   # starts one SQL Server container for the whole run
dotnet test                                   # everything
```

## Run the app
1. `copy .env.example .env` and set `MSSQL_SA_PASSWORD` (never commit `.env`).
2. `./scripts/run-demo.ps1` then open http://localhost:5102

## Layout
`Domain` (pure engine) · `Data` (EF Core) · `Contracts` (DTOs) · `Api` (Controllers) · `Web` (Blazor).
Tests: `Domain.Tests` (unit) · `Web.Tests` (bUnit) · `Integration.Tests` (Testcontainers + `WebApplicationFactory`).

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
7. Open the UI, edit a bill, click **Process Bill**, and watch the discounted totals appear.

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
