# PROC: Process a bill

[Back to the capabilities index](README.md)

**PROC** When a client requests processing of a bill, the Billing API shall reprice the bill against the fee schedule and store the results with an audit trail.

## PROC-1 Process a bill on request

**PROC-1** When a client requests processing of an existing bill, the Billing API shall price all of the bill's lines and return the updated bill.

- **PROC-1.1** The Billing API shall use the base fees in effect on the bill's date of service. (untested)
- **PROC-1.2** The Billing API shall store each line's allowed amount, pricing status and unpriced reason.
- **PROC-1.3** The Billing API shall store the bill's total allowed.
- **PROC-1.4** The Billing API shall set the bill's status to Processed.
- **PROC-1.5** The Billing API shall set the bill's processed time to the current UTC time.
- **PROC-1.6** The Billing API shall log the bill's identifier, line count and unpriced line count. (untested)
- **PROC-1.7** When the bill was already processed, the Billing API shall replace the previous results and adjustments.
  - **PROC-1.7.1** When the bill and the fee schedule are unchanged, the Billing API shall produce the same allowed amounts, total and number of adjustments.
- **PROC-1.8** When a line has no fee schedule entry, the Billing API shall still process the bill.
- **PROC-1.9** If the bill does not exist, then the Billing API shall respond with status 404.

Evidence: [ProcessBillService.cs](../../src/Billing.Api/ProcessBillService.cs), [BillsController.cs](../../src/Billing.Api/Controllers/BillsController.cs). Tests: `Process_returns_and_persists_the_allowed_amounts_and_total_when_the_bill_has_a_priced_line`, `Process_sets_ProcessedAtUtc_to_the_injected_clock_time`, `Process_produces_identical_results_when_the_bill_is_processed_twice`, `Process_flags_the_line_as_unpriced_and_still_processes_the_bill_when_a_line_has_no_fee_schedule_entry`, `Process_returns_404_when_the_bill_does_not_exist`.

## PROC-2 Reprice each line

**PROC-2** The Repricing engine shall calculate an allowed amount for each line of a bill from the line's base fee.

- **PROC-2.1** The Repricing engine shall start from the base fee supplied for the line's procedure code.
- **PROC-2.2** The Repricing engine shall multiply the amount by the line's units.
- **PROC-2.3** The Repricing engine shall multiply the amount by a factor for each modifier on the line.
  - **PROC-2.3.1** Where a line has modifier 50 (bilateral), the Repricing engine shall multiply the amount by 1.50.
  - **PROC-2.3.2** Where a line has modifier 26 (professional component), the Repricing engine shall multiply the amount by 0.40.
  - **PROC-2.3.3** Where a line has both modifiers, the Repricing engine shall apply both factors.
  - **PROC-2.3.4** When a line lists a modifier twice, the Repricing engine shall apply that modifier once.
- **PROC-2.4** The Repricing engine shall round each line's amount once, to 2 decimals, after all factors. (untested)
  - **PROC-2.4.1** When the amount lands on a half cent, the Repricing engine shall round away from zero.
- **PROC-2.5** The Repricing engine shall mark a line that has a base fee as Priced.
- **PROC-2.6** When the rounded amount exceeds the line's billed amount, the Repricing engine shall set the allowed amount to the billed amount.
  - **PROC-2.6.1** When the billed amount is zero, the Repricing engine shall allow 0 and keep the line's status as Priced.
- **PROC-2.7** If a line's procedure code has no base fee, then the Repricing engine shall mark the line as Unpriced.
  - **PROC-2.7.1** If a line's procedure code has no base fee, then the Repricing engine shall give the line the reason `NoFeeScheduleEntry` and an allowed amount of 0.
  - **PROC-2.7.2** If a line's procedure code has no base fee, then the Repricing engine shall record no audit trail steps for that line. (untested)
  - **PROC-2.7.3** If a line's procedure code has no base fee, then the Repricing engine shall still price the bill's other lines.
- **PROC-2.8** If any line has a modifier other than 50 or 26, then the Repricing engine shall throw an `ArgumentException` naming the line and modifier, and return no partial result.
  - **PROC-2.8.1** If the line with the unknown modifier is also unpriced, then the Repricing engine shall still throw the exception. (untested)
- **PROC-2.9** If the bill or the base fees are null, then the Repricing engine shall throw an `ArgumentNullException`. (untested)

Evidence: [RepricingEngine.cs](../../src/Billing.Domain/Pricing/RepricingEngine.cs). Tests: [RepricingEngineTests.cs](../../tests/Billing.Domain.Tests/RepricingEngineTests.cs), [RepricingEnginePropertyTests.cs](../../tests/Billing.Domain.Tests/RepricingEnginePropertyTests.cs).

## PROC-3 Total the allowed amounts

**PROC-3** The Repricing engine shall set the bill's total allowed to the sum of its lines' allowed amounts.

- **PROC-3.1** When a bill has unpriced lines, the Repricing engine shall count them as 0 in the total.
- **PROC-3.2** When a bill has no lines, the Repricing engine shall return no line results and a total of 0.

Evidence: [RepricingEngine.cs](../../src/Billing.Domain/Pricing/RepricingEngine.cs). Tests: `Reprice_pays_every_line_in_full_and_sums_the_total_allowed_when_a_bill_has_several_priced_lines`, `Reprice_counts_unpriced_lines_as_zero_in_the_total_allowed_when_a_bill_has_priced_and_unpriced_lines`, `Reprice_returns_no_lines_and_a_zero_total_when_the_bill_is_empty`, `Reprice_sets_the_total_allowed_to_the_sum_of_the_line_amounts`.

## PROC-4 Record an audit trail

**PROC-4** The Repricing engine shall record each pricing step of a priced line, in the order applied, with its factor where it has one and the resulting amount.

- **PROC-4.1** The Repricing engine shall record a `BaseFee` step with no factor.
- **PROC-4.2** The Repricing engine shall record a `Units` step whose factor is the line's units.
- **PROC-4.3** The Repricing engine shall record a `Rounding` step with no factor and the rounded amount, after the `Units` step and any modifier steps.
- **PROC-4.4** Where a line has modifier 50, the Repricing engine shall record a `Modifier50` step with factor 1.50.
- **PROC-4.5** Where a line has modifier 26, the Repricing engine shall record a `Modifier26` step with factor 0.40, after any `Modifier50` step. (untested)
- **PROC-4.6** When the billed charges cap applies, the Repricing engine shall record a `BilledChargesCap` step with no factor and the capped amount.
- **PROC-4.7** When a bill is processed, the Billing API shall store the steps as the line's adjustments, numbered in sequence from 1.

Evidence: [RepricingEngine.cs](../../src/Billing.Domain/Pricing/RepricingEngine.cs), [StepNames.cs](../../src/Billing.Domain/Pricing/StepNames.cs), [ProcessBillService.cs](../../src/Billing.Api/ProcessBillService.cs). Tests: `Reprice_records_each_audit_trail_step_in_order`, `Reprice_caps_the_allowed_amount_at_billed_charges_when_the_priced_amount_exceeds_them`, `Process_returns_and_persists_the_allowed_amounts_and_total_when_the_bill_has_a_priced_line`. The factor values of the steps and the sequence numbers are not asserted.

## PROC-5 Keep repricing deterministic

**PROC-5** The Repricing engine shall calculate its result only from the bill and base fees it is given, without using a database, clock or log.

- **PROC-5.1** The Repricing engine shall return line results in line number order, whatever order the lines arrive in.
- **PROC-5.2** The Repricing engine shall leave the bill unchanged. (untested)

Evidence: [RepricingEngine.cs](../../src/Billing.Domain/Pricing/RepricingEngine.cs). Tests: `Reprice_depends_on_no_database_logging_or_time_library` and `Reprice_never_reads_the_system_clock` in [ArchitectureTests.cs](../../tests/Billing.Domain.Tests/ArchitectureTests.cs), which check the whole `Billing.Domain` assembly; `Reprice_returns_lines_in_line_number_order_when_the_input_is_out_of_order`, `Reprice_returns_the_same_result_whatever_order_the_lines_arrive_in`.
