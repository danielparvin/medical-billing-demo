# UI: Work with bills in the browser

[Back to the capabilities index](README.md)

**UI** The Bill workspace shall let a user list, create, edit and process bills in a browser, using the Billing API.

## UI-1 Browse the bill list

**UI-1** When a user opens `/` or `/bills`, the Bill workspace shall show a table of all bills from the Billing API. (untested)

- **UI-1.1** The Bill workspace shall show each bill's number, patient reference, date of service, status and total allowed. (untested)
- **UI-1.2** The Bill workspace shall link each bill number to that bill's editor. (untested)
- **UI-1.3** The Bill workspace shall offer a New bill link to `/bills/new`. (untested)
- **UI-1.4** While the bills are loading, the Bill workspace shall show a loading message. (untested)
- **UI-1.5** When there are no bills, the Bill workspace shall show "No bills yet." (untested)

Evidence: [Bills.razor](../../src/Billing.Web/Components/Pages/Bills.razor). Tests: none.

## UI-2 Edit a bill

**UI-2** The Bill workspace shall provide an editor at `/bills/new` and `/bills/{id}` for a bill's patient reference, date of service and lines.

- **UI-2.1** The Bill workspace shall offer a checkbox for each of modifiers 50 and 26 on every line. (untested)
- **UI-2.2** The Bill workspace shall offer a Back to bills link. (untested)
- **UI-2.3** When a user opens `/bills/new`, the Bill workspace shall show an empty bill with one line whose units are 1.
- **UI-2.4** When a user opens `/bills/{id}` for an existing bill, the Bill workspace shall show one row per line.
- **UI-2.5** When a user clicks Add line, the Bill workspace shall append a row numbered one above the highest line number.
- **UI-2.6** When a user clicks Remove on a row, the Bill workspace shall remove that row.
- **UI-2.7** While an existing bill is loading, the Bill workspace shall show a loading message. (untested)
- **UI-2.8** If the bill does not exist, then the Bill workspace shall show "Bill N was not found." (untested)

Evidence: [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor), [BillEditModel.cs](../../src/Billing.Web/Models/BillEditModel.cs). Tests: `OnParametersSetAsync_renders_one_row_per_line_when_the_bill_exists`, `AddLine_appends_a_row`, `RemoveLine_drops_the_row`. These tests check the row counts, not the new line's number or units.

## UI-3 Check procedure codes before saving

**UI-3** The Bill workspace shall accept a procedure code only if it is exactly 5 ASCII letters or digits, in either case.

- **UI-3.1** The Bill workspace shall limit the code field to 5 characters.
- **UI-3.2** When a user leaves a code field holding an invalid code, the Bill workspace shall flag that field.
- **UI-3.3** While a code field is empty and no save has been tried, the Bill workspace shall not flag that field.
- **UI-3.4** When a user corrects a flagged code, the Bill workspace shall remove the flag from that field.
- **UI-3.5** If any line has an invalid code when the user clicks Save or Process Bill, then the Bill workspace shall send nothing to the Billing API.
  - **UI-3.5.1** If any line has an invalid code when the user clicks Save or Process Bill, then the Bill workspace shall flag only the invalid lines.
  - **UI-3.5.2** If any line has an invalid code when the user clicks Save or Process Bill, then the Bill workspace shall show an error message asking for 5 letters or digits.

Evidence: [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor), [BillEditModel.cs](../../src/Billing.Web/Models/BillEditModel.cs). Tests: `BuildRenderTree_limits_the_code_field_to_five_characters`, `SaveCoreAsync_blocks_the_api_call_and_shows_an_error_when_the_code_is_invalid`, `SaveCoreAsync_sends_the_code_to_the_api_when_the_code_is_valid`, `ShowCodeError_flags_the_field_when_a_bad_code_is_entered_but_not_when_it_is_untouched_and_empty`, `ShowCodeError_clears_the_error_when_the_code_is_corrected`, `ShowCodeError_flags_only_the_bad_line_when_other_lines_have_valid_codes`.

## UI-4 Save a bill

**UI-4** When a user clicks Save, the Bill workspace shall send the bill to the Billing API and show the stored bill.

- **UI-4.1** When the bill is new, the Bill workspace shall create it.
  - **UI-4.1.1** When a new bill has been created, the Bill workspace shall navigate to that bill's URL. (untested)
- **UI-4.2** When the bill already exists, the Bill workspace shall replace it.
- **UI-4.3** While a save or process request is running, the Bill workspace shall disable the Save and Process Bill buttons. (untested)

Evidence: [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor), [BillingApiClient.cs](../../src/Billing.Web/Services/BillingApiClient.cs). Tests: `SaveCoreAsync_sends_the_code_to_the_api_when_the_code_is_valid` for the create call, and `SaveAsync_shows_the_validation_error_when_the_api_rejects_the_bill` for the replace call.

## UI-5 Process a bill

**UI-5** When a user clicks Process Bill, the Bill workspace shall save the current edits and then ask the Billing API to process the saved bill.

- **UI-5.1** When processing a new bill succeeds, the Bill workspace shall navigate to that bill's URL. (untested)
- **UI-5.2** If the save fails, then the Bill workspace shall not request processing. (untested)
- **UI-5.3** If processing fails after the save succeeded, then the Bill workspace shall show the error and the saved bill. (untested)

Evidence: [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor). Tests: `ProcessAsync_saves_then_processes_the_bill_and_shows_the_allowed_amounts`.

## UI-6 Show processing results

**UI-6** While the loaded bill is Processed, the Bill workspace shall show its status and total allowed. (untested)

- **UI-6.1** While the loaded bill is Processed, the Bill workspace shall show each line's allowed amount to 2 decimals.
- **UI-6.2** While the loaded bill is Processed, the Bill workspace shall show each line's pricing status as a badge. (untested)
- **UI-6.3** When a line is unpriced, the Bill workspace shall show the unpriced reason beside its status. (untested)
- **UI-6.4** When a line has adjustments, the Bill workspace shall offer an expandable audit trail listing each step, its factor where it has one, and its resulting amount. (untested)
- **UI-6.5** When a bill is saved and returns to Draft, the Bill workspace shall remove the processing results. (untested)

Evidence: [BillEditor.razor](../../src/Billing.Web/Components/Pages/BillEditor.razor). Tests: `ProcessAsync_saves_then_processes_the_bill_and_shows_the_allowed_amounts`.

## UI-7 Report failed requests

**UI-7** If a save or process request fails, then the Bill workspace shall show a readable error message at the top of the editor.

- **UI-7.1** If the Billing API returns validation errors, then the Bill workspace shall show all of their messages.
- **UI-7.2** If the failure response has a title and no validation errors, then the Bill workspace shall show the title. (untested)
- **UI-7.3** If the failure response has neither, then the Bill workspace shall show a generic message with the status code. (untested)
- **UI-7.4** If the Billing API cannot be reached, then the Bill workspace shall say that the API is unreachable.

Evidence: [BillingApiClient.cs](../../src/Billing.Web/Services/BillingApiClient.cs), [ProblemMessage.cs](../../src/Billing.Web/Services/ProblemMessage.cs). Tests: `SaveAsync_shows_the_validation_error_when_the_api_rejects_the_bill`, `SaveAsync_shows_a_readable_error_when_the_api_is_unreachable`.

## UI-8 Provide the application shell

**UI-8** The Bill workspace shall show a navigation bar on every page, with a link to the bill list and the label "Synthetic data only". (untested)

- **UI-8.1** If a user requests a page that does not exist, then the Bill workspace shall show a not-found page. (untested)
- **UI-8.2** If an unhandled request error occurs outside the Development environment, then the Bill workspace shall show an error page with a request identifier. (untested)
- **UI-8.3** If the browser loses its connection to the server, then the Bill workspace shall show a dialog that tries to rejoin and offers a retry. (untested)

Evidence: [MainLayout.razor](../../src/Billing.Web/Components/Layout/MainLayout.razor), [NotFound.razor](../../src/Billing.Web/Components/Pages/NotFound.razor), [Error.razor](../../src/Billing.Web/Components/Pages/Error.razor), [ReconnectModal.razor](../../src/Billing.Web/Components/Layout/ReconnectModal.razor), [Program.cs](../../src/Billing.Web/Program.cs). Tests: none.
