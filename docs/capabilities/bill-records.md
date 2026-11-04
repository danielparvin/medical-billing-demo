# BILL: Maintain bills

[Back to the capabilities index](README.md)

**BILL** The Billing API shall maintain bills, each made of a header and one or more lines, for clients to create, replace, retrieve and list.

## BILL-1 Create a bill

**BILL-1** When a client submits a valid new bill, the Billing API shall store the bill with its lines.

- **BILL-1.1** The Billing API shall assign the bill an identifier and return the stored bill.
- **BILL-1.2** The Billing API shall respond with status 201 and the location of the new bill. (untested)
- **BILL-1.3** The Billing API shall give the new bill the status Draft. (untested)

Evidence: [BillsController.cs](../../src/Billing.Api/Controllers/BillsController.cs), [Bill.cs](../../src/Billing.Domain/Bill.cs). Tests: every test in [BillsApiTests.cs](../../tests/Billing.Integration.Tests/BillsApiTests.cs) that creates a bill through its `CreateAsync` helper.

## BILL-2 Replace a bill

**BILL-2** When a client submits a valid replacement for an existing bill, the Billing API shall replace the bill's header and all of its lines and return the stored bill.

- **BILL-2.1** When the replaced bill was already processed, the Billing API shall return the bill to Draft.
  - **BILL-2.1.1** When the replaced bill was already processed, the Billing API shall clear its total allowed and processed time.
  - **BILL-2.1.2** When the replaced bill was already processed, the Billing API shall clear each line's allowed amount, pricing status, unpriced reason and adjustments.
- **BILL-2.2** If the bill does not exist, then the Billing API shall respond with status 404. (untested)

Evidence: [BillsController.cs](../../src/Billing.Api/Controllers/BillsController.cs), [BillMapper.cs](../../src/Billing.Api/BillMapper.cs). Tests: `Update_returns_the_bill_to_draft_and_clears_the_results_when_the_bill_was_processed`.

## BILL-3 Retrieve a bill

**BILL-3** When a client requests a bill by its identifier, the Billing API shall return the bill's header, lines, modifiers, pricing results and adjustments.

- **BILL-3.1** The Billing API shall return the lines in line number order.
- **BILL-3.2** The Billing API shall return each line's adjustments in sequence order. (untested)
- **BILL-3.3** The Billing API shall return the processed time as a UTC time.
- **BILL-3.4** If the bill does not exist, then the Billing API shall respond with status 404.

Evidence: [BillRepository.cs](../../src/Billing.Data/BillRepository.cs), [BillConfiguration.cs](../../src/Billing.Data/Configurations/BillConfiguration.cs), [BillMapper.cs](../../src/Billing.Api/BillMapper.cs). Tests: `GetAsync_returns_the_saved_values_when_a_bill_has_lines_modifiers_and_adjustments`, `GetAsync_returns_lines_in_line_number_order_when_they_were_saved_out_of_order`, `Get_returns_404_when_the_bill_does_not_exist`.

## BILL-4 List bills

**BILL-4** When a client requests the list of bills, the Billing API shall return a summary of every bill. (untested)

- **BILL-4.1** The Billing API shall include each bill's identifier, patient reference, date of service, status and total allowed in its summary. (untested)
- **BILL-4.2** The Billing API shall order the summaries newest first, by descending identifier. (untested)

Evidence: [BillsController.cs](../../src/Billing.Api/Controllers/BillsController.cs), [BillRepository.cs](../../src/Billing.Data/BillRepository.cs). Tests: none.

## BILL-5 Validate submitted bills

**BILL-5** If a submitted bill breaks a validation rule, then the Billing API shall reject it with status 400 and problem details, and shall store nothing.

- **BILL-5.1** If the patient reference is blank or longer than 50 characters, then the Billing API shall reject the bill. (untested)
- **BILL-5.2** If the date of service is missing or is the minimum date value, then the Billing API shall reject the bill. (untested)
- **BILL-5.3** If the bill has no lines, then the Billing API shall reject the bill.
- **BILL-5.4** If a line number is less than 1, then the Billing API shall reject the bill. (untested)
- **BILL-5.5** If two lines share a line number, then the Billing API shall reject the bill and name the repeated number.
- **BILL-5.6** If a procedure code is not exactly 5 characters long, then the Billing API shall reject the bill. (untested)
- **BILL-5.7** If a line's units are outside 1 to 999, then the Billing API shall reject the bill.
- **BILL-5.8** If a line's billed amount is outside 0 to 9,999,999.99, then the Billing API shall reject the bill. (untested)
- **BILL-5.9** If a line has a modifier other than 50 or 26, then the Billing API shall reject the bill and name the line and modifier.
- **BILL-5.10** If a line lists the same modifier more than once, then the Billing API shall reject the bill and name the line.

Evidence: [BillRequest.cs](../../src/Billing.Contracts/BillRequest.cs), [LineItemRequest.cs](../../src/Billing.Contracts/LineItemRequest.cs). Tests: `Create_returns_400_problem_details_when_the_bill_has_no_lines`, `Create_returns_400_when_a_line_has_an_unknown_modifier`, `Create_returns_400_when_a_line_repeats_a_modifier`, `Create_returns_400_when_line_numbers_are_duplicated`, `Create_returns_400_instead_of_overflowing_the_database_when_the_units_are_too_large`. These tests check the status codes through the create endpoint only, not the message text and not the replace endpoint.

## BILL-6 Normalize procedure codes

**BILL-6** When the Billing API stores a bill, the Billing API shall convert each procedure code to uppercase.

- **BILL-6.1** When a procedure code is submitted in lowercase, the Billing API shall return it in uppercase and match it to its fee schedule entry.

Evidence: [BillMapper.cs](../../src/Billing.Api/BillMapper.cs). Tests: `Process_matches_the_fee_schedule_entry_when_the_procedure_code_is_lowercase`.
