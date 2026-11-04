# FEE: Provide effective-dated fees

[Back to the capabilities index](README.md)

**FEE** The Billing API shall keep a fee schedule of base fees by procedure code and effective date range, and shall provide the fee in effect on a given date of service.

## FEE-1 Resolve the fee in effect

**FEE-1** When a fee is needed for a procedure code and date of service, the Billing API shall select the entry whose effective date range includes that date.

- **FEE-1.1** The Billing API shall treat the effective-from and effective-to dates as inclusive.
- **FEE-1.2** When an entry has no effective-to date, the Billing API shall treat the entry as in effect indefinitely.
- **FEE-1.3** When a code has several versions, the Billing API shall select the version in effect on the date of service.
- **FEE-1.4** When several entries for a code are in effect on the same date, the Billing API shall select the one with the latest effective-from date. (untested)
- **FEE-1.5** When fees are needed for several codes at once, the Billing API shall return only the codes with an entry in effect.
- **FEE-1.6** If no entry is in effect, then the Billing API shall return no fee for that code.

Evidence: [FeeScheduleRepository.cs](../../src/Billing.Data/FeeScheduleRepository.cs). Tests: [FeeScheduleRepositoryTests.cs](../../tests/Billing.Integration.Tests/FeeScheduleRepositoryTests.cs). The date boundaries and versions are tested on `FindAsync` only. `GetBaseFeesAsync`, which processing uses, repeats the same query and is tested only for leaving out unknown codes.

## FEE-2 Look up a fee over HTTP

**FEE-2** When a client requests a fee by procedure code and date of service, the Billing API shall return the entry's procedure code, description, base amount and effective dates.

- **FEE-2.1** When the procedure code is in lowercase, the Billing API shall match it as uppercase. (untested)
- **FEE-2.2** If the date of service is missing, then the Billing API shall respond with status 400 and problem details.
- **FEE-2.3** If no entry is in effect for the code on that date, then the Billing API shall respond with status 404.

Evidence: [FeeScheduleController.cs](../../src/Billing.Api/Controllers/FeeScheduleController.cs). Tests: `Get_returns_the_fee_in_effect_when_the_procedure_code_has_an_entry_on_the_date_of_service`, `Get_returns_404_when_the_fee_procedure_code_is_unknown`, `Get_returns_400_when_the_fee_lookup_has_no_date_of_service`. These tests check the base amount and status codes, not the other fields or the problem details body.

## FEE-3 Seed the synthetic fee schedule

**FEE-3** When seeding runs, the Billing API shall load a fixed set of 20 invented fee schedule entries, including price changes on 1 January 2026 for codes A1002 and B1236.

- **FEE-3.1** When seeding runs again, the Billing API shall add no duplicate entries.
- **FEE-3.2** When a schedule already exists, the Billing API shall add only the entries whose code and effective-from date are missing.
  - **FEE-3.2.1** When a seeded code and effective-from date already exist, the Billing API shall keep the existing amount.

Evidence: [FeeScheduleSeeder.cs](../../src/Billing.Data/FeeScheduleSeeder.cs). Tests: [SeederTests.cs](../../tests/Billing.Integration.Tests/SeederTests.cs). Seeding at startup is covered by [RUN-2](application-runtime.md#run-2-prepare-the-database-on-startup).

## FEE-4 Keep fee entries unique

**FEE-4** If an entry is saved with the same procedure code and effective-from date as an existing entry, then the database shall reject the save.

Evidence: [FeeScheduleEntryConfiguration.cs](../../src/Billing.Data/Configurations/FeeScheduleEntryConfiguration.cs). Tests: `SaveChangesAsync_throws_a_DbUpdateException_when_two_fee_entries_have_the_same_code_and_effective_from`.
