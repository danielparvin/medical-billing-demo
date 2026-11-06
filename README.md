# Medical Billing Testing Demo

A fully synthetic claim-repricing app built to discuss the role of testing in AI-assisted development in .NET, with
unit, property-based, component, integration, API end-to-end and mutation testing. **All data is invented; there is no PHI.**

## Prerequisites
- .NET SDK from `global.json` (.NET 10)
- Docker (SQL Server image is **x64-only**)
- `dotnet tool restore` (restores the repo-local `dotnet-ef` and `dotnet-stryker`; only needed to add migrations or
  run mutation testing)
- A trusted HTTPS development certificate for the Aspire dashboard: `dotnet dev-certs https --trust` once per
  machine (Windows asks you to confirm). Do this before the talk, not on stage.

Open `Billing.slnx` in Visual Studio 2022 17.13+ or Visual Studio 2026.

## Run the tests
```bash
dotnet test --filter "Category=Unit"          # instant, no Docker
dotnet test --filter "Category=Integration"   # starts one SQL Server container for the whole run
dotnet test                                   # everything
```

CI ([ci.yml](.github/workflows/ci.yml)) runs both categories on every pull request and every push to `main`. It
selects tests by category, so a test without a `Category` trait of `Unit` or `Integration` never runs in CI.

Mutation testing of the repricing engine takes about a minute and needs no Docker:
```bash
cd tests/Billing.Domain.Tests
dotnet tool run dotnet-stryker --open-report
```

## Run the app (Aspire)
`Billing.AppHost` orchestrates everything: a SQL Server container (`sql`, database `Billing`), the API (`api`),
and the Blazor UI (`web`).

The SQL Server `sa` password is the AppHost parameter `sql-password`, kept in the AppHost's user secrets (never in
the repo). On a new machine, set it once before the first run (8+ characters with upper, lower, digit and symbol);
if it is missing, the Aspire dashboard asks for it instead:

```powershell
dotnet user-secrets set "Parameters:sql-password" "<strong password>" --project src/Billing.AppHost
```

> **Changing the password later:** SQL Server stores the `sa` password in the data volume the first time it starts
> and ignores later changes. After changing `Parameters:sql-password`, delete the SQL container and its volume
> (`billing.apphost-…-sql-data`) in Docker Desktop, or the `sql` resource stays **Unhealthy** with
> "Login failed for user 'sa'" in its logs. Deleting the volume also deletes the dev database's bills.

- **Visual Studio:** set `Billing.AppHost` as the startup project and press F5.
- **Command line:** `./scripts/run-demo.ps1` (or `dotnet run --project src/Billing.AppHost`).

The Aspire dashboard opens with every resource, its logs, traces and metrics; the UI is at http://localhost:5102.
The SQL Server container is **persistent** with a data volume, so after the first start it is reused across runs
(stop it from Docker Desktop to reclaim it). The web app finds the API through service discovery (`https+http://api`).

## Layout
`Domain` (pure engine) · `Data` (EF Core) · `Contracts` (DTOs) · `Api` (Controllers) · `Web` (Blazor) ·
`AppHost` (Aspire orchestration) · `ServiceDefaults` (OpenTelemetry, health checks, service discovery, resilience).
Tests: `Domain.Tests` (unit and property-based, with CsCheck) · `Web.Tests` (bUnit) · `Integration.Tests` (Testcontainers + `WebApplicationFactory`).

Aspire runs the app; it does not change the tests. The integration tests still start their own throwaway SQL Server
with Testcontainers, so they stay independent of the AppHost and of any data in the persistent dev container.

## Checkpoints
| Tag | State |
|---|---|
| `cp-1-scaffold` | Empty solution building green |
| `cp-2-engine-no-mpd` | Engine without the multiple-procedure discount, with unit tests |
| `cp-3-data-api` | Data layer, API, container-backed tests |
| `cp-4-ui` | Full app, **discount not yet implemented** |
| `cp-5-mpd-complete` | Discount tests + implementation |

## Talk arc
The thread: **green tests prove only what they assert, so an AI's green build is where review starts, not where it ends.**

1. **What the agent has to work with.** Tour the solution and its test kinds; run the unit tests (instant). Fast tests are
   the feedback loop an agent can run for itself.
2. **Green tests, wrong code.** Seed a rounding bug, say that you seeded it, and watch all 69 tests pass (script below).
3. **Why the agent got it right.** Show the [agent evals](#agent-evals): ten agents out of ten kept the engine correct,
   including five that never saw the written rule. They read "show" as a display change. That is good judgment, but
   judgment isn't a guarantee: nothing would have failed if they had changed the engine.
4. **State the rule, not an example.** Add a property test and let it find its own failing bill (script below). An
   agent's examples can share the blind spots of its code; a property comes from the requirement.
5. **Who tests the tests?** Run Stryker, close an audit-trail gap, then show what it can't find: the rounding bug doesn't
   move the score (script below).
6. **Change the rules on purpose.** Have the agent build the discount, test by test (script below). One test contradicts
   the discount. In the [agent evals](#agent-evals), five agents out of five rewrote it to expect the discount and only
   said so afterwards. Here they were right, but the agent makes that call either way, so the diff to the tests is the
   first thing to review. The property tests stay green through the change, because their rules still hold.
7. **What a mock can't tell you.** Run the integration tests with `docker ps` in a second terminal, and show the
   effective-date boundary tests and the API tests (`WebApplicationFactory` + `FakeTimeProvider`). An agent can write a
   mock that agrees with its own code; a real SQL Server won't play along.
8. **See it run.** Start the AppHost, open the UI from the Aspire dashboard, click **New bill** and enter some lines (a new
   database has no bills), click **Process Bill**, and watch the discounted totals appear; then open the dashboard's Traces
   page: one trace spans the button click in `web` and the request in `api`.

## Live-coding script (multiple-procedure discount)
Start on `main`, which has no discount. The tag `cp-5-mpd-complete` has a finished version to refer to, though its
tests use the older naming.

The discount contradicts one test on `main`, so before you start, remove
`Reprice_pays_every_line_in_full_and_sums_the_total_allowed_when_a_bill_has_several_priced_lines` from
`tests/Billing.Domain.Tests/RepricingEngineTests.cs` and leave that edit uncommitted.

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
8. same result when the lines arrive in a different order (`RepricingEnginePropertyTests` already checks this on generated
   bills, and it also passed against the finished discount at `cp-5-mpd-complete`; compare it with the example you write)

## Demo script: green tests, wrong code
Start from a clean working tree, because the reset discards all uncommitted changes to the engine. The bug is seeded by a
script; no agent wrote it (see [Agent evals](#agent-evals)), so say so on stage. PROC-2.4 is still marked `(untested)` in
the capabilities docs because the tests below stay out of the suite so the demo works.

1. Run `./scripts/demo-rounding-bug.ps1`, then `git diff`. Each modifier now rounds as it is applied.
2. Run `dotnet test --filter "Category=Unit"` (40 tests) or `dotnet test` with Docker running (69 tests). All of them pass.
3. Add this test to `RepricingEngineTests.cs`. It comes from requirement PROC-2.4, not from the code:
   ```csharp
   [Fact]
   [Trait("Requirement", "PROC-2.4")]
   public void Reprice_rounds_only_once_after_all_the_multipliers_when_a_line_has_both_modifiers()
   {
       // 100.09 x 1.50 x 0.40 = 60.054, which rounds to 60.05. Rounding after each factor gives 60.06.
       var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "26").Build();

       var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.09m)));

       Assert.Equal(60.05m, result.Lines[0].AllowedAmount);
   }
   ```
4. Run the tests again. The new test fails: expected 60.05, actual 60.06. Leave the bug in place for the next demo.

## Demo script: state the rule, not an example
You needed to know 100.09 was a bad fee to write that test. A property doesn't need to know.

1. With the bug still applied, add this test to `RepricingEnginePropertyTests.cs`:
   ```csharp
   [Fact]
   [Trait("Requirement", "PROC-2.4")]
   public void Reprice_rounds_only_once_after_all_the_multipliers()
   {
       Gen.Select(Money, Gen.Int[1, 999], Modifiers).Sample(t =>
       {
           var (fee, units, modifiers) = t;
           var bill = new BillBuilder().WithLine(1, "A1001", 1_000_000_000m, units, modifiers).Build();

           var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", fee)));

           var exact = fee * units
               * (modifiers.Contains("50") ? RepricingEngine.BilateralFactor : 1m)
               * (modifiers.Contains("26") ? RepricingEngine.ProfessionalComponentFactor : 1m);
           return result.Lines[0].AllowedAmount == Math.Round(exact, 2, MidpointRounding.AwayFromZero);
       }, iter: Iterations);
   }
   ```
2. Run the tests. CsCheck reports a failing bill it found and a seed that reproduces it, such as `(62.31, 609, [50, 26])`.
   The bill differs from run to run, so don't promise a particular one. The formula in the test is the rule written out
   again, so someone still has to check that it matches the requirement.
3. Run `./scripts/demo-rounding-bug.ps1 -Reset`. Both new tests pass against the correct engine.
4. Run `git restore tests/Billing.Domain.Tests/RepricingEngineTests.cs tests/Billing.Domain.Tests/RepricingEnginePropertyTests.cs`
   to remove them before the next rehearsal.

## Agent evals
Each eval gave five fresh agents the same prompt on a copy of commit `f52c5b8`, which predates these demos, and graded
their diffs with tests the agents never saw. These are small samples run through Claude Code subagents, not your stage
setup, so treat them as anecdotes, not rates.

**1. "Show dollars and cents at every audit step."** (repo as committed) **None of the five wrote the rounding bug.**
- All five made the same display-only change to `BillEditor.razor`, added a test, and updated UI-6.4. None changed the
  engine, and all five passed the PROC-2.4 test.
- All five mentioned the round-once rule in their reports.
- All five warned that the audit steps can now look wrong on screen, because hidden fractions of a cent can make a step
  appear not to multiply exactly. All five also pointed out that they had not added a `$` sign.

**2. The same prompt, without the written rule.** This copy had no git history, its README had no talk material, and
the round-once rule was removed from the engine's doc comment and from PROC-2.4. **None of the five wrote the bug.**
- All five made the same display-only change, and all five passed the PROC-2.4 test.
- Four of the five said that rounding in the engine at every step would be a different change that alters the allowed
  amounts, and left it alone. So the written rule wasn't what kept the engine correct; reading "show" as display was.

**3. "Add a multiple-procedure discount …"** with the rule from the live-coding script, on a copy with no history, no
tags and no live-coding script, so the agents had no hint about the contradicting test.
- **All five rewrote the contradicting test** to expect 50, 100 and 300 with a total of 450, and renamed it. None
  deleted it or stopped to ask; all five named the change in their final report.
- All five implementations passed six independent discount checks: full and half pay, ranking after modifiers, the
  tie-break, unpriced lines, rounding once after the discount, and the cap after it. Four of the six checks fail on the
  engine without the discount, and all six pass on `cp-5-mpd-complete`.
- All five added their own tests, a new audit step and new requirements in the capabilities docs, and none ran the
  integration tests.

Don't rely on an agent producing the rounding bug live: use the script.

## Demo script: who tests the tests?
A run takes about a minute, so start it before you begin talking about it, or open a report saved earlier.

1. Run Stryker (see [Run the tests](#run-the-tests)). On the correct engine it scores **83.87%**, with 5 surviving mutants.
2. In the report, show the survivor on the `Modifier26` line of `RepricingEngine.cs`: deleting that audit-trail step
   fails no test, so an agent could drop it and stay green.
3. Add this test to `RepricingEngineTests.cs`:
   ```csharp
   [Fact]
   [Trait("Requirement", "PROC-4.5")]
   public void Reprice_records_the_professional_component_step_after_the_bilateral_step_when_a_line_has_both_modifiers()
   {
       var bill = new BillBuilder().WithLine(1, "A1001", 500m, 1, "50", "26").Build();

       var result = RepricingEngine.Reprice(bill, BillBuilder.Fees(("A1001", 100.00m)));

       Assert.Equal(
           [StepNames.BaseFee, StepNames.Units, StepNames.Modifier50, StepNames.Modifier26, StepNames.Rounding],
           result.Lines[0].Steps.Select(s => s.Step));
   }
   ```
4. Run Stryker again. That mutant is now killed; with this test alone the score rises to **87.10%**. Then run
   `git restore tests/Billing.Domain.Tests/RepricingEngineTests.cs` to remove it before the next rehearsal.
5. Make the limit clear: Stryker also scores **83.87%** with the rounding bug applied. It makes small changes to the code
   that exists and checks whether a test notices. It can't tell you that the code breaks a rule nobody wrote a test for.

The other survivors are the two null checks, the unknown-modifier message, and `>` versus `>=` on the billed-charges cap.

## Demo-day readiness
- The day before: `./scripts/pull-images.ps1`, then run the integration tests once while online.
- The day before: run Stryker once and keep the report from `tests/Billing.Domain.Tests/StrykerOutput` as a fallback.
- **Fallback if Docker misbehaves:** show a saved terminal capture of a green integration run; unit and
  component tests need no Docker.
- Rehearse the live-coding segment twice. To reset, run `git restore .` to discard all uncommitted changes, delete the
  untracked `MultipleProcedureDiscountTests.cs`, and remove the contradicting test again before the next run.

## Known exceptions
- `Billing.Web.Tests` suppresses NuGet advisory GHSA-pgww-w46g-26qg for AngleSharp 1.2.0. bUnit 1.40 (the last 1.x)
  is compiled against that version and patched AngleSharp releases are binary-incompatible with it. It is a
  test-only dependency; remove the suppression when moving to bUnit 2.x.
