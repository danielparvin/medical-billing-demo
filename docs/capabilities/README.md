# Capabilities index

The functional requirements of the medical billing demo, reverse engineered from the code and tests on `main`. They state what the app does today, not what it should do. All of its data is synthetic.

## Contents

### [BILL: Maintain bills](bill-records.md)

| ID | Capability |
|---|---|
| [BILL-1](bill-records.md#bill-1-create-a-bill) | Create a bill |
| [BILL-2](bill-records.md#bill-2-replace-a-bill) | Replace a bill |
| [BILL-3](bill-records.md#bill-3-retrieve-a-bill) | Retrieve a bill |
| [BILL-4](bill-records.md#bill-4-list-bills) | List bills |
| [BILL-5](bill-records.md#bill-5-validate-submitted-bills) | Validate submitted bills |
| [BILL-6](bill-records.md#bill-6-normalize-procedure-codes) | Normalize procedure codes |

### [PROC: Process a bill](bill-processing.md)

| ID | Capability |
|---|---|
| [PROC-1](bill-processing.md#proc-1-process-a-bill-on-request) | Process a bill on request |
| [PROC-2](bill-processing.md#proc-2-reprice-each-line) | Reprice each line |
| [PROC-3](bill-processing.md#proc-3-total-the-allowed-amounts) | Total the allowed amounts |
| [PROC-4](bill-processing.md#proc-4-record-an-audit-trail) | Record an audit trail |
| [PROC-5](bill-processing.md#proc-5-keep-repricing-deterministic) | Keep repricing deterministic |

### [FEE: Provide effective-dated fees](fee-schedule.md)

| ID | Capability |
|---|---|
| [FEE-1](fee-schedule.md#fee-1-resolve-the-fee-in-effect) | Resolve the fee in effect |
| [FEE-2](fee-schedule.md#fee-2-look-up-a-fee-over-http) | Look up a fee over HTTP |
| [FEE-3](fee-schedule.md#fee-3-seed-the-synthetic-fee-schedule) | Seed the synthetic fee schedule |
| [FEE-4](fee-schedule.md#fee-4-keep-fee-entries-unique) | Keep fee entries unique |

### [UI: Work with bills in the browser](bill-workspace.md)

| ID | Capability |
|---|---|
| [UI-1](bill-workspace.md#ui-1-browse-the-bill-list) | Browse the bill list |
| [UI-2](bill-workspace.md#ui-2-edit-a-bill) | Edit a bill |
| [UI-3](bill-workspace.md#ui-3-check-procedure-codes-before-saving) | Check procedure codes before saving |
| [UI-4](bill-workspace.md#ui-4-save-a-bill) | Save a bill |
| [UI-5](bill-workspace.md#ui-5-process-a-bill) | Process a bill |
| [UI-6](bill-workspace.md#ui-6-show-processing-results) | Show processing results |
| [UI-7](bill-workspace.md#ui-7-report-failed-requests) | Report failed requests |
| [UI-8](bill-workspace.md#ui-8-provide-the-application-shell) | Provide the application shell |

### [RUN: Run and observe the application](application-runtime.md)

| ID | Capability |
|---|---|
| [RUN-1](application-runtime.md#run-1-orchestrate-the-application) | Orchestrate the application |
| [RUN-2](application-runtime.md#run-2-prepare-the-database-on-startup) | Prepare the database on startup |
| [RUN-3](application-runtime.md#run-3-report-health) | Report health |
| [RUN-4](application-runtime.md#run-4-emit-telemetry) | Emit telemetry |
| [RUN-5](application-runtime.md#run-5-connect-the-services-reliably) | Connect the services reliably |

## How to read the requirements

**Nesting.** Each file opens with its general capability. Sections narrow it, and nested bullets narrow it further. A child requirement applies only within its parent's scope, and its ID extends its parent's: `PROC-2` > `PROC-2.3` > `PROC-2.3.1`. Among siblings, unconditional requirements come first, then conditional ones, then unwanted behaviour.

**EARS patterns.**

| Pattern | Form | Used for |
|---|---|---|
| Ubiquitous | The *system* shall *response* | Always true |
| Event | When *trigger*, the *system* shall *response* | A trigger or a condition on the data |
| State | While *state*, the *system* shall *response* | As long as a state holds |
| Optional | Where *feature*, the *system* shall *response* | A modifier or setting is present |
| Unwanted | If *condition*, then the *system* shall *response* | Errors and invalid input |

**Systems.**

| Name | What it is |
|---|---|
| Billing API | `Billing.Api`, with its data layer in `Billing.Data` |
| Repricing engine | `RepricingEngine` in `Billing.Domain` |
| Bill workspace | The Blazor UI in `Billing.Web` |
| AppHost | The Aspire orchestrator in `Billing.AppHost` |
| Database | The SQL Server schema, where a rule is enforced there (FEE-4 only) |

**Traceability.** Each section ends with an `Evidence:` line that links its source and names its tests. `(untested)` means that no automated test asserts the requirement; the behaviour was read from the code and was not run.

## Not capabilities on `main`

These might be expected, but the app does not do them:

- **Multiple-procedure discount.** It exists only as the live-coding exercise in the [project README](../../README.md); on `main`, every line pays in full.
- **Deleting bills.** There is no endpoint or UI for it.
- **Authentication or authorization.** Every endpoint and page is open.
- **Editing the fee schedule.** Over HTTP it is read-only; entries come only from the seeder.

## Observed gaps

These look like defects or inconsistencies. They are recorded here, not fixed, and are not written as requirements so that the index doesn't present them as intended behaviour. They were found by reading the code and were not reproduced in the running app.

| Gap | Where | Related |
|---|---|---|
| Loading the bill list or an existing bill doesn't catch an unreachable API, unlike save and process. | [BillingApiClient.cs](../../src/Billing.Web/Services/BillingApiClient.cs) | UI-1, UI-2, UI-7.4 |
| The editor stays editable after "Bill N was not found", and saving then gets a 404 from the replace call. | [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor) | UI-2.8, BILL-2.2 |
| After a processed bill is edited, the old results stay on screen until the next save. | [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor) | UI-6.5 |
| The error banner for invalid codes stays after the code is corrected, until the next save or process. | [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor) | UI-3.4, UI-3.5.2 |
| The engine's unknown-modifier exception can only be reached by data that bypassed API validation, and nothing handles it specifically, so it would surface as a 500. | [ProcessBillService.cs](../../src/Billing.Api/ProcessBillService.cs) | PROC-2.8, BILL-5.9 |
